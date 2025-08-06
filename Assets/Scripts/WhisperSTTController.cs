using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Whisper;

public class WhisperSTTController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WhisperManager whisperManager;
    [SerializeField] private CoquiTTSController speaker;

    // C# Events for code subscribers
    public event Action<string> OnCommandTranscribed;
    public event Action OnCommandListenTimeout;

    // UnityEvents for Inspector binding
    [Header("Event Callbacks")]
    [SerializeField] private UnityEvent onListeningStartEvent;
    [SerializeField] private UnityEvent<string> onTranscriptionSuccessEvent;
    [SerializeField] private UnityEvent onTranscriptionFailedEvent;

    [FormerlySerializedAs("SampleRate")]
    [Header("Microphone Settings")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordingLengthSeconds = 10;

    [Header("Voice Activity Detection (VAD)")]
    [SerializeField] private bool useVoiceActivityDetection = true;
    [Range(0.0f, 0.1f)] [SerializeField] private float silenceThreshold = 0.01f;
    [Range(0.0f, 3f)] [SerializeField] private float requiredSilenceDuration = 1.5f;
    
    private const float VadCheckInterval = 0.1f;
    
    private AudioClip _recordingClip;
    private string _microphoneDevice;
    private bool _isRecordingCommand = false;
    private Coroutine _voiceActivityCoroutine;

    private void Awake()
    {
        if (Microphone.devices.Length > 0)
        {
            _microphoneDevice = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("WhisperSTTController: No microphone devices found!");
            enabled = false;
        }
    }

    public void StartListeningForCommand(float duration)
    {
        if (_isRecordingCommand)
        {
            Debug.LogWarning("WhisperSTTController: Already recording a command. Ignoring new request.");
            return;
        }

        Debug.Log($"WhisperSTTController: Starting to record command...");
        _isRecordingCommand = true;
        onListeningStartEvent?.Invoke();

        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
        
        _recordingClip = Microphone.Start(_microphoneDevice, true, maxRecordingLengthSeconds, sampleRate);

        if (useVoiceActivityDetection)
        {
            _voiceActivityCoroutine = StartCoroutine(MonitorVoiceActivity());
        }
        
        Invoke(nameof(StopListeningForCommand), duration);
    }

    public async void StopListeningForCommand()
    {
        if (!_isRecordingCommand) return; 

        _isRecordingCommand = false;
        CancelInvoke(nameof(StopListeningForCommand)); 

        if (_voiceActivityCoroutine != null)
        {
            StopCoroutine(_voiceActivityCoroutine);
            _voiceActivityCoroutine = null;
        }

        Debug.Log("WhisperSTTController: Stopping command recording.");
        var micPosition = Microphone.GetPosition(_microphoneDevice);
        Microphone.End(_microphoneDevice);

        if (_recordingClip == null || micPosition <= 0)
        {
            Debug.LogWarning("WhisperSTTController: Recorded audio clip is empty. Not transcribing.");
            onTranscriptionFailedEvent?.Invoke();
            // This path should also signal the timeout to prevent a stuck state
            OnCommandListenTimeout?.Invoke(); 
            return;
        }
        
        var samples = new float[_recordingClip.samples * _recordingClip.channels];
        _recordingClip.GetData(samples, 0);
        var trimmedSamples = new float[micPosition * _recordingClip.channels];
        Array.Copy(samples, trimmedSamples, trimmedSamples.Length);
        var trimmedClip = AudioClip.Create("TrimmedRecording", micPosition, _recordingClip.channels, _recordingClip.frequency, false);
        trimmedClip.SetData(trimmedSamples, 0);

        try
        {
            Debug.Log("WhisperSTTController: Passing audio to WhisperManager for transcription.");
            var result = await whisperManager.GetTextAsync(trimmedClip);

            if (result != null && !string.IsNullOrEmpty(result.Result))
            {
                var cleanedText = result.Result.Replace("[BLANK_AUDIO]", "").Trim();

                if (!string.IsNullOrEmpty(cleanedText))
                {
                    Debug.Log($"Whisper Transcribed (Cleaned): \"{cleanedText}\"");
                    OnCommandTranscribed?.Invoke(cleanedText);
                    onTranscriptionSuccessEvent?.Invoke(cleanedText);
                }
                else
                {
                    Debug.LogWarning($"Transcription result contained only non-speech tokens. Raw: \"{result.Result}\"");
                    onTranscriptionFailedEvent?.Invoke();
                }
            }
            else
            {
                var rawResult = (result == null || string.IsNullOrEmpty(result.Result)) ? "NULL_OR_EMPTY" : result.Result;
                Debug.LogWarning($"WhisperSTTController: Transcription failed. Raw result from Whisper: \"{rawResult}\"");
                onTranscriptionFailedEvent?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"WhisperSTTController: Transcription failed: {e.Message}");
            onTranscriptionFailedEvent?.Invoke();
        }
        finally
        {
            OnCommandListenTimeout?.Invoke();
            Destroy(trimmedClip); 
        }
    }
    
    private IEnumerator MonitorVoiceActivity()
    {
        float silentTime = 0;
        var lastSamplePosition = 0;
        var sampleChunk = new float[Mathf.CeilToInt(sampleRate * VadCheckInterval)];

        yield return new WaitForSeconds(0.5f); 

        while (_isRecordingCommand)
        {
            var currentSamplePosition = Microphone.GetPosition(_microphoneDevice);
            
            _recordingClip.GetData(sampleChunk, lastSamplePosition);
            
            var sum = sampleChunk.Sum(Mathf.Abs);
            var averageVolume = sum / sampleChunk.Length;

            if (averageVolume < silenceThreshold)
            {
                silentTime += VadCheckInterval;
            }
            else
            {
                silentTime = 0; 
            }

            if (silentTime >= requiredSilenceDuration)
            {
                Debug.Log($"VAD: Silence detected for {requiredSilenceDuration}s. Stopping recording.");
                StopListeningForCommand();
                yield break;
            }

            lastSamplePosition = currentSamplePosition;
            yield return new WaitForSeconds(VadCheckInterval);
        }
    }

    private void OnDestroy()
    {
        if (_microphoneDevice != null && Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
        CancelInvoke();
    }

    public bool IsListeningForCommand()
    {
        return _isRecordingCommand;
    }
}