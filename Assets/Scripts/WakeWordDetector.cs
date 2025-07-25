using UnityEngine;
using Pv.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Events; // Required for UnityEvents

public class WakeWordDetector : MonoBehaviour
{
    [Tooltip("AccessKey obtained from Picovoice Console (https://console.picovoice.ai/)")]
    [SerializeField]
    private string _picovoiceAccessKey = "YOUR_PICOVOICE_ACCESS_KEY"; 

    [Tooltip("Filename of your custom wake word .ppn model within StreamingAssets.")]
    [SerializeField]
    private string _wakeWordModelFilename;

    private PorcupineManager _porcupineManager;
    private bool _isListeningForWakeWord = false;

    // C# event for code-based subscriptions
    public event Action OnWakeWordDetected; 

    // UnityEvent for Inspector binding
    [Header("Event Callbacks")]
    [Tooltip("Fires when the wake word is successfully detected. Bind custom actions here in the Inspector.")]
    public UnityEvent OnWakeWordDetectedEvent; 

    [Tooltip("Assign your Whisper STT Handler script here (e.g., the GameObject with WhisperSTTController).")]
    public MonoBehaviour whisperSTTHandler; 

    [Tooltip("Duration in seconds to listen for a command after the wake word is detected.")]
    [SerializeField]
    private float _commandListenDuration = 5.0f;

    void Awake()
    {
        if (string.IsNullOrEmpty(_picovoiceAccessKey) || _picovoiceAccessKey == "YOUR_PICOVOICE_ACCESS_KEY")
        {
            Debug.LogError("Picovoice AccessKey is not set! Please get one from console.picovoice.ai and set it in the Inspector.");
            enabled = false;
            return;
        }

        RequestMicrophonePermission();
    }

    void Start()
    {
        InitializePorcupine();
    }

    private void RequestMicrophonePermission()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("Requesting microphone permission for the first time...");
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }
        else
        {
            Debug.Log("Microphone permission already granted.");
        }
    }

    private void InitializePorcupine()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Invoke(nameof(InitializePorcupine), 0.5f);
            return;
        }

        // This path logic loads directly from StreamingAssets/filename.
        // It does not include platform-specific subfolders.
        string wakeWordPath = Path.Combine(Application.streamingAssetsPath, _wakeWordModelFilename);
        
        if (!File.Exists(wakeWordPath))
        {
            Debug.LogError($"Wake word model file not found at: {wakeWordPath}. Please ensure it's in StreamingAssets.");
            enabled = false;
            return;
        }

        try
        {
            _porcupineManager = PorcupineManager.FromKeywordPaths(
                _picovoiceAccessKey,
                new List<string> { wakeWordPath },
                WakeWordCallback
            );

            Debug.Log("Porcupine manager initialized successfully.");

            StartWakeWordListening();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Porcupine: {e.Message}");
        }
    }

    public void StartWakeWordListening()
    {
        if (_porcupineManager == null)
        {
            Debug.LogWarning("PorcupineManager is not initialized. Cannot start listening.");
            return; 
        }

        if (!_isListeningForWakeWord)
        {
            try
            {
                _porcupineManager.Start();
                _isListeningForWakeWord = true;
                Debug.Log("Porcupine started listening for 'Hey Fridge'...");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start Porcupine: {e.Message}. Is microphone already in use?");
            }
        }
    }

    public void StopWakeWordListening()
    {
        if (_porcupineManager != null && _isListeningForWakeWord)
        {
            try
            {
                _porcupineManager.Stop();
                _isListeningForWakeWord = false;
                Debug.Log("Porcupine stopped listening for 'Hey Fridge'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to stop Porcupine: {e.Message}");
            }
        }
    }

    private void WakeWordCallback(int keywordIndex)
    {
        if (keywordIndex == 0) // Assuming 'Hey Fridge' is the first (and only) keyword
        {
            Debug.Log("Wake word 'Hey Fridge' detected!");
            
            OnWakeWordDetectedEvent?.Invoke(); // Invoke the UnityEvent for Inspector binding

            StopWakeWordListening(); 
            
            OnWakeWordDetected?.Invoke(); // Invoke the C# event for code subscribers

            // Signal your Whisper STT Handler to start listening for the command
            if (whisperSTTHandler != null)
            {
                ICommandListener commandListener = whisperSTTHandler as ICommandListener; 

                if (commandListener != null) 
                {
                    Debug.Log("Signaling Whisper STT to start listening for command...");
                    commandListener.StartListeningForCommand(_commandListenDuration);
                }
                else
                {
                    Debug.LogWarning("Whisper STT Handler assigned but does not implement ICommandListener. Cannot start command listening.");
                }
            }
            else
            {
                Debug.LogWarning("Whisper STT Handler not assigned. Voice assistant will not respond to commands.");
            }

            // Schedule Porcupine to restart listening for the wake word after the command duration
            CancelInvoke(nameof(RestartWakeWordListening)); 
            Invoke(nameof(RestartWakeWordListening), _commandListenDuration);
        }
    }

    private void RestartWakeWordListening()
    {
        StartWakeWordListening();
    }

    void OnApplicationQuit()
    {
        if (_porcupineManager != null)
        {
            _porcupineManager.Stop(); 
            _porcupineManager = null; // Dereference to aid garbage collection
            Debug.Log("Porcupine manager stopped and dereferenced on application quit.");
        }
    }
}

// Interface for Whisper STT script to implement, making WakeWordDetector more flexible.
public interface ICommandListener
{
    void StartListeningForCommand(float duration);
}