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
    [SerializeField] private string picovoiceAccessKey;
    [SerializeField] private string wakeWordModelFilename;
    [SerializeField] private WhisperSTTController sttController;
    [SerializeField] private float commandListenDuration = 10.0f;

    [Header("Event Callbacks")]
    [SerializeField] private UnityEvent onWakeWordDetectedEvent;

    private PorcupineManager _porcupineManager;
    private bool _isListeningForWakeWord;
    private IEnumerator _listeningCoroutine;

    private void Awake()
    {
        if (string.IsNullOrEmpty(picovoiceAccessKey) || picovoiceAccessKey.Length < 10)
        {
            Debug.LogError("Picovoice AccessKey is not set!"); enabled = false; return;
        }

        if (sttController != null) return;
        Debug.LogError("WhisperSTTController is not assigned in the Inspector!", this); enabled = false; return;
    }

    private void Start()
    {
        _listeningCoroutine = TransitionToCommandListening();
        sttController.OnCommandListenTimeout += RestartWakeWordListening;
        InitializePorcupine();
    }
    
    private void InitializePorcupine()
    {
        Debug.Log($"[WakeWordDetector] Checking Mic Permission. Has Permission: {Application.HasUserAuthorization(UserAuthorization.Microphone)}");

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("[WakeWordDetector] Microphone permission not granted. Requesting...");
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
            Invoke(nameof(InitializePorcupine), 1f); // This gives a 1s delay for the user to respond with mic permissions
            return;
        }

        try
        {
            Debug.Log("[WakeWordDetector] Permission granted. Attempting to create PorcupineManager...");
            _porcupineManager = PorcupineManager.FromKeywordPaths(
                picovoiceAccessKey,
                new List<string> { Path.Combine(Application.streamingAssetsPath, wakeWordModelFilename) },
                WakeWordCallback);
            Debug.Log("[WakeWordDetector] PorcupineManager created successfully!");
            StartWakeWordListening();
        }
        catch (Exception e) 
        {
            Debug.LogError($"[WakeWordDetector] Failed to create Porcupine manager: {e}"); 
        }
    }
    
    private void WakeWordCallback(int keywordIndex)
    {
        if (keywordIndex != 0) return;
        onWakeWordDetectedEvent?.Invoke();
        StartCoroutine(_listeningCoroutine);
    }

    private IEnumerator TransitionToCommandListening()
    {
        Debug.Log("Wake word detected! Stopping Porcupine...");
        StopWakeWordListening();
        
        // Wait one frame to allow the audio driver to release the microphone
        yield return null; 
        
        Debug.Log("Transitioning to Whisper STT...");
        sttController.StartListeningForCommand(commandListenDuration);
    }

    private void RestartWakeWordListening()
    {
        Debug.Log("Event received: STT session ended. Restarting wake word listening.");
        StartWakeWordListening();
    }

    private void StartWakeWordListening()
    {
        if (_porcupineManager == null || _isListeningForWakeWord) return;
        try
        {
            _porcupineManager.Start();
            _isListeningForWakeWord = true;
            Debug.Log(">>> Porcupine started listening for wake word. <<<");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start Porcupine: {e.Message}");
        }
    }

    public void StopWakeWordListening()
    {
        if (_porcupineManager == null || !_isListeningForWakeWord) return;
        try
        {
            _porcupineManager.Stop();
            _isListeningForWakeWord = false;
            Debug.Log("Porcupine stopped listening.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to stop Porcupine: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (sttController != null) sttController.OnCommandListenTimeout -= RestartWakeWordListening;
        _porcupineManager?.Delete();
    }
}