using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Events; // Required for UnityEvents
using Whisper; // From your installed 'com.whisper.unity' package

// Implement the interface required by WakeWordDetector
public class WhisperSTTController : MonoBehaviour, ICommandListener
{
    // Reference to the WhisperManager component from your package.
    [Tooltip("Assign the WhisperManager component from your 'com.whisper.unity' package here. If left blank, it will try to FindObjectOfType.")]
    public WhisperManager whisperManager;

    [Tooltip("Filename of the Whisper model (e.g., ggml-tiny.bin) in StreamingAssets. This is configured directly on the WhisperManager component.")]
    public string whisperModelFilename = "ggml-tiny.bin"; 

    // Event to send transcribed text out to your NLU/TTS logic (C# event for code subscribers)
    public event Action<string> OnCommandTranscribed; 
    public event Action OnCommandListenTimeout; // Signifies when listening period has ended

    // UnityEvents for Inspector binding
    [Header("Event Callbacks")]
    [Tooltip("Fires when the system starts actively listening for a command.")]
    public UnityEvent OnListeningStartEvent;
    // *** MODIFIED: OnTranscriptionSuccessEvent now passes the transcribed string ***
    [Tooltip("Fires when transcription is successful and text is obtained. Passes the transcribed string as an argument.")]
    public UnityEvent<string> OnTranscriptionSuccessEvent; 
    [Tooltip("Fires when transcription fails (e.g., no speech, error during processing).")]
    public UnityEvent OnTranscriptionFailedEvent;

    private AudioClip _recordingClip;
    private string _microphoneDevice;
    private bool _isRecordingCommand = false;
    private Task<WhisperResult> _transcriptionTask; // To hold the async transcription task

    [Header("Microphone Settings for Whisper")]
    public int SampleRate = 16000; // Whisper models typically expect 16kHz
    public int MaxRecordingLengthSeconds = 10; // Max duration for a single command capture
    
    void Awake()
    {
        if (Microphone.devices.Length > 0)
        {
            _microphoneDevice = Microphone.devices[0]; // Use the first available microphone
        }
        else
        {
            Debug.LogError("WhisperSTTController: No microphone devices found! Please ensure your Quest has a working mic.");
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

        Debug.Log($"WhisperSTTController: Starting to record for a command for {duration} seconds...");
        _isRecordingCommand = true;
        
        OnListeningStartEvent?.Invoke(); // Invoke UnityEvent when listening starts

        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }

        _recordingClip = Microphone.Start(_microphoneDevice, false, MaxRecordingLengthSeconds, SampleRate);
        
        CancelInvoke(nameof(StopListeningForCommand)); 
        Invoke(nameof(StopListeningForCommand), duration);
    }

    public async void StopListeningForCommand() // Made async
    {
        if (!_isRecordingCommand)
        {
            Debug.LogWarning("WhisperSTTController: Not currently recording a command. Ignoring stop request.");
            return;
        }

        Debug.Log("WhisperSTTController: Stopping command recording.");
        _isRecordingCommand = false;
        CancelInvoke(nameof(StopListeningForCommand)); 

        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice); 

            if (_recordingClip != null && _recordingClip.samples > 0)
            {
                if (whisperManager != null)
                {
                    Debug.Log("WhisperSTTController: Passing audio to WhisperManager for transcription.");
                    
                    try
                    {
                        _transcriptionTask = whisperManager.GetTextAsync(_recordingClip);
                        WhisperResult result = await _transcriptionTask; 

                        if (result != null && !string.IsNullOrEmpty(result.Result)) // Access result.Result
                        {
                            Debug.Log($"Whisper Transcribed: \"{result.Result}\"");
                            OnCommandTranscribed?.Invoke(result.Result); 
                            OnTranscriptionSuccessEvent?.Invoke(result.Result); // *** MODIFIED: Invoke with result.Result ***
                        }
                        else
                        {
                            Debug.LogWarning("WhisperSTTController: Transcription result was null or empty (no speech detected or empty transcription).");
                            OnCommandTranscribed?.Invoke(""); 
                            OnTranscriptionFailedEvent?.Invoke(); // Invoke UnityEvent on failure
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"WhisperSTTController: Transcription failed: {e.Message}");
                        OnCommandTranscribed?.Invoke(""); 
                        OnTranscriptionFailedEvent?.Invoke(); // Invoke UnityEvent on error
                    }
                }
                else
                {
                    Debug.LogError("WhisperSTTController: WhisperManager is null. Cannot transcribe.");
                    OnCommandTranscribed?.Invoke(""); 
                    OnTranscriptionFailedEvent?.Invoke(); // Invoke UnityEvent on failure
                }
            }
            else
            {
                Debug.LogWarning("WhisperSTTController: Recorded audio clip is empty or null. Not transcribing (no audio input).");
                OnCommandTranscribed?.Invoke(""); 
                OnTranscriptionFailedEvent?.Invoke(); // Invoke UnityEvent on failure
            }
        }
        else
        {
            Debug.LogWarning("WhisperSTTController: Microphone was not recording when StopListeningForCommand was called.");
            OnCommandTranscribed?.Invoke(""); 
            OnTranscriptionFailedEvent?.Invoke(); // Invoke UnityEvent on failure
        }

        OnCommandListenTimeout?.Invoke(); 
    }
    
    void OnDestroy()
    {
        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
        CancelInvoke(); 
    }
}