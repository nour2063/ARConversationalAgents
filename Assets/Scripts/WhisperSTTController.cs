using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

public class WhisperSTTController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string serverIPAddress = "localhost";
    [SerializeField] private int serverPort = 5000;
    [SerializeField] private string sttApiEndpoint = "/transcribe";

    [Header("References")]
    [SerializeField] private CoquiTTSController speaker;

    // C# Events for code subscribers
    public event Action<string> OnCommandTranscribed;
    public event Action OnCommandListenTimeout;

    // UnityEvents for Inspector binding
    [Header("Event Callbacks")]
    [SerializeField] private UnityEvent onListeningStartEvent;
    [SerializeField] private UnityEvent<string> onTranscriptionSuccessEvent;
    [SerializeField] private UnityEvent onTranscriptionFailedEvent;

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

    public void StopListeningForCommand()
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
            OnCommandListenTimeout?.Invoke(); 
            return;
        }
        
        var samples = new float[micPosition * _recordingClip.channels];
        _recordingClip.GetData(samples, 0);
        
        var wavData = WavUtility.ConvertToWav(samples, _recordingClip.channels, _recordingClip.frequency);
        
        StartCoroutine(TranscribeAudioWithAPI(wavData));
        
        Destroy(_recordingClip);
        _recordingClip = null;
    }
    
    private IEnumerator TranscribeAudioWithAPI(byte[] audioData)
    {
        var url = $"http://{serverIPAddress}:{serverPort}{sttApiEndpoint}";
        
        var form = new List<IMultipartFormSection>();
        form.Add(new MultipartFormFileSection("file", audioData, "audio.wav", "audio/wav"));

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            Debug.Log($"WhisperSTTController: Uploading audio to {url} for transcription...");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"STT Request failed: {www.error}");
                Debug.LogError($"Response: {www.downloadHandler.text}");
                onTranscriptionFailedEvent?.Invoke();
            }
            else
            {
                var response = JsonUtility.FromJson<TranscriptionResponse>(www.downloadHandler.text);
                var transcribedText = response.text?.Trim();

                if (!string.IsNullOrEmpty(transcribedText))
                {
                    Debug.Log($"Server Transcribed: \"{transcribedText}\"");
                    OnCommandTranscribed?.Invoke(transcribedText);
                    onTranscriptionSuccessEvent?.Invoke(transcribedText);
                }
                else
                {
                    Debug.LogWarning("Server transcription result was empty.");
                    onTranscriptionFailedEvent?.Invoke();
                }
            }
        }
        
        OnCommandListenTimeout?.Invoke();
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
            
            var readPos = (lastSamplePosition < currentSamplePosition) ? lastSamplePosition : 0;
            _recordingClip.GetData(sampleChunk, readPos);
            
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
    
    [Serializable]
    private class TranscriptionResponse
    {
        public string text;
    }
}
