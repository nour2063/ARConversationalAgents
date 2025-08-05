using UnityEngine;
using Pv.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Events;

public class WakeWordDetector : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string _picovoiceAccessKey;
    [SerializeField] private string _wakeWordModelFilename;
    [SerializeField] private WhisperSTTController _sttController;
    [SerializeField] private float _commandListenDuration = 10.0f;

    [Header("Event Callbacks")]
    public UnityEvent OnWakeWordDetectedEvent;

    private PorcupineManager _porcupineManager;
    private bool _isListeningForWakeWord = false; // Using your correct flag

    void Awake()
    {
        if (string.IsNullOrEmpty(_picovoiceAccessKey) || _picovoiceAccessKey.Length < 10) { Debug.LogError("Picovoice AccessKey is not set!"); enabled = false; return; }
        if (_sttController == null) { Debug.LogError("WhisperSTTController is not assigned in the Inspector!", this); enabled = false; return; }
    }

    void Start()
    {
        _sttController.OnCommandListenTimeout += RestartWakeWordListening;
        InitializePorcupine();
    }
    
    private void InitializePorcupine()
    {
        Debug.Log($"[WakeWordDetector] Checking Mic Permission. Has Permission: {Application.HasUserAuthorization(UserAuthorization.Microphone)}");

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("[WakeWordDetector] Microphone permission not granted. Requesting...");
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
            Invoke(nameof(InitializePorcupine), 1f); // This gives a 1s delay for the user to respond
            return;
        }

        try
        {
            Debug.Log("[WakeWordDetector] Permission granted. Attempting to create PorcupineManager...");
            _porcupineManager = PorcupineManager.FromKeywordPaths(
                _picovoiceAccessKey,
                new List<string> { Path.Combine(Application.streamingAssetsPath, _wakeWordModelFilename) },
                WakeWordCallback);
            Debug.Log("[WakeWordDetector] PorcupineManager created successfully!");
            StartWakeWordListening();
        }
        catch (Exception e) 
        {
            // This will give you more detail in the log
            Debug.LogError($"[WakeWordDetector] Failed to create Porcupine manager: {e.ToString()}"); 
        }
    }
    
    private void WakeWordCallback(int keywordIndex)
    {
        if (keywordIndex == 0)
        {
            OnWakeWordDetectedEvent?.Invoke();
            StartCoroutine(TransitionToCommandListening());
        }
    }

    private IEnumerator TransitionToCommandListening()
    {
        Debug.Log("Wake word detected! Stopping Porcupine...");
        StopWakeWordListening();
        
        // Wait one frame to allow the audio driver to release the microphone
        yield return null; 
        
        Debug.Log("Transitioning to Whisper STT...");
        _sttController.StartListeningForCommand(_commandListenDuration);
    }

    private void RestartWakeWordListening()
    {
        Debug.Log("Event received: STT session ended. Restarting wake word listening.");
        StartWakeWordListening();
    }

    public void StartWakeWordListening()
    {
        // CORRECTED: Uses your boolean flag
        if (_porcupineManager == null || _isListeningForWakeWord) return;
        try
        {
            _porcupineManager.Start();
            _isListeningForWakeWord = true; // CORRECTED: Sets your flag
            Debug.Log(">>> Porcupine started listening for wake word. <<<");
        }
        catch (Exception e) { Debug.LogError($"Failed to start Porcupine: {e.Message}"); }
    }

    public void StopWakeWordListening()
    {
        // CORRECTED: Uses your boolean flag
        if (_porcupineManager == null || !_isListeningForWakeWord) return;
        try
        {
            _porcupineManager.Stop();
            _isListeningForWakeWord = false; // CORRECTED: Sets your flag
            Debug.Log("Porcupine stopped listening.");
        }
        catch (Exception e) { Debug.LogError($"Failed to stop Porcupine: {e.Message}"); }
    }

    void OnDestroy()
    {
        if (_sttController != null) _sttController.OnCommandListenTimeout -= RestartWakeWordListening;
        if (_porcupineManager != null) _porcupineManager.Delete();
    }
}