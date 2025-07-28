using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Events;
using Whisper;

public class WhisperSTTController : MonoBehaviour
{
    [Tooltip("Assign the WhisperManager component from your 'com.whisper.unity' package here.")]
    public WhisperManager whisperManager;
    [SerializeField] private LocalNetworkTTS speaker;

    // C# Events for code subscribers
    public event Action<string> OnCommandTranscribed;
    public event Action OnCommandListenTimeout;

    // UnityEvents for Inspector binding
    [Header("Event Callbacks")]
    [Tooltip("Fires when the system starts actively listening for a command.")]
    public UnityEvent OnListeningStartEvent;
    [Tooltip("Fires when transcription is successful. Passes the transcribed string as an argument.")]
    public UnityEvent<string> OnTranscriptionSuccessEvent;
    [Tooltip("Fires when transcription fails (e.g., no speech, error).")]
    public UnityEvent OnTranscriptionFailedEvent;

    [Header("Microphone Settings")]
    public int SampleRate = 16000;
    public int MaxRecordingLengthSeconds = 10;

    [Header("Voice Activity Detection (VAD)")]
    public bool useVoiceActivityDetection = true;
    [Range(0.0f, 0.1f)]
    public float silenceThreshold = 0.01f;
    public float requiredSilenceDuration = 1.5f;
    private const float VAD_CHECK_INTERVAL = 0.1f;


    private AudioClip _recordingClip;
    private string _microphoneDevice;
    private bool _isRecordingCommand = false;
    private Coroutine _voiceActivityCoroutine;

    void Awake()
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
        OnListeningStartEvent?.Invoke();

        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
        
        _recordingClip = Microphone.Start(_microphoneDevice, true, MaxRecordingLengthSeconds, SampleRate);

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
        int micPosition = Microphone.GetPosition(_microphoneDevice);
        Microphone.End(_microphoneDevice);

        if (_recordingClip == null || micPosition <= 0)
        {
            Debug.LogWarning("WhisperSTTController: Recorded audio clip is empty. Not transcribing.");
            OnTranscriptionFailedEvent?.Invoke();
            // This path should also signal the timeout to prevent a stuck state
            OnCommandListenTimeout?.Invoke(); 
            return;
        }
        
        float[] samples = new float[_recordingClip.samples * _recordingClip.channels];
        _recordingClip.GetData(samples, 0);
        float[] trimmedSamples = new float[micPosition * _recordingClip.channels];
        Array.Copy(samples, trimmedSamples, trimmedSamples.Length);
        AudioClip trimmedClip = AudioClip.Create("TrimmedRecording", micPosition, _recordingClip.channels, _recordingClip.frequency, false);
        trimmedClip.SetData(trimmedSamples, 0);

        try
        {
            Debug.Log("WhisperSTTController: Passing audio to WhisperManager for transcription.");
            var result = await whisperManager.GetTextAsync(trimmedClip);

            // --- EXCLUSIVE CHANGE IS HERE ---
            if (result != null && !string.IsNullOrEmpty(result.Result))
            {
                // Remove the noise token and any surrounding whitespace
                string cleanedText = result.Result.Replace("[BLANK_AUDIO]", "").Trim();

                // Check if any actual text remains after cleaning
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    Debug.Log($"Whisper Transcribed (Cleaned): \"{cleanedText}\"");
                    OnCommandTranscribed?.Invoke(cleanedText);
                    OnTranscriptionSuccessEvent?.Invoke(cleanedText);
                }
                else
                {
                    // This handles cases where the original text was only "[BLANK_AUDIO]"
                    Debug.LogWarning($"Transcription result contained only non-speech tokens. Raw: \"{result.Result}\"");
                    OnTranscriptionFailedEvent?.Invoke();
                }
            }
            else
            {
                var rawResult = (result == null || string.IsNullOrEmpty(result.Result)) ? "NULL_OR_EMPTY" : result.Result;
                Debug.LogWarning($"WhisperSTTController: Transcription failed. Raw result from Whisper: \"{rawResult}\"");
                OnTranscriptionFailedEvent?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"WhisperSTTController: Transcription failed: {e.Message}");
            OnTranscriptionFailedEvent?.Invoke();
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
        int lastSamplePosition = 0;
        float[] sampleChunk = new float[Mathf.CeilToInt(SampleRate * VAD_CHECK_INTERVAL)];

        yield return new WaitForSeconds(0.5f); 

        while (_isRecordingCommand)
        {
            int currentSamplePosition = Microphone.GetPosition(_microphoneDevice);
            
            _recordingClip.GetData(sampleChunk, lastSamplePosition);
            
            float sum = 0;
            for (int i = 0; i < sampleChunk.Length; i++)
            {
                sum += Mathf.Abs(sampleChunk[i]); 
            }
            float averageVolume = sum / sampleChunk.Length;

            if (averageVolume < silenceThreshold)
            {
                silentTime += VAD_CHECK_INTERVAL;
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
            yield return new WaitForSeconds(VAD_CHECK_INTERVAL);
        }
    }
    
    void OnDestroy()
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