using Meta.WitAi.CallbackHandlers;
using Oculus.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Reflection;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Utilities;

public class VoiceManager : MonoBehaviour
{
    [SerializeField] private PassthroughCameraTTS llmManager;
    
    [Header("Wit Configuration")]
    [SerializeField] private AppVoiceExperience appVoiceExperience;
    [SerializeField] private WitResponseMatcher responseMatcher;
    [SerializeField] private TextMeshProUGUI transcriptionText;

    [Header("Voice Events")]
    [SerializeField] private UnityEvent wakeWordDetected;
    [SerializeField] private UnityEvent<string> completeTranscription;

    [Header("Grace Period Settings")]
    [SerializeField] private float listeningDuration = 5.0f;

    public bool listening;
        
    private bool _voiceCommandReady;
    private Coroutine _currentGracePeriodCoroutine;

    private void Start()
    {
        appVoiceExperience.VoiceEvents.OnRequestCompleted.AddListener(ReactivateVoice);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
        appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);

        var eventField = typeof(WitResponseMatcher).GetField("onMultiValueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        if (eventField != null && eventField.GetValue(responseMatcher) is MultiValueEvent onMultiValueEvent)
        {
            onMultiValueEvent.AddListener(WakeWordDetected);
        }
        if (llmManager != null && llmManager.speaker != null)
        {
            llmManager.speaker.Events.OnPlaybackComplete.AddListener(HandleSpeechFinished);
        }
        
        appVoiceExperience.Activate();
    }

    private void OnDestroy()
    {
        appVoiceExperience.VoiceEvents.OnRequestCompleted.RemoveListener(ReactivateVoice);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
        appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);

        var eventField = typeof(WitResponseMatcher).GetField("onMultiValueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        if (eventField != null && eventField.GetValue(responseMatcher) is MultiValueEvent onMultiValueEvent)
        {
            onMultiValueEvent.RemoveListener(WakeWordDetected);
        }

        if (_currentGracePeriodCoroutine != null)
        {
            StopCoroutine(_currentGracePeriodCoroutine);
        }
    }
    
    private void ReactivateVoice() => appVoiceExperience.Activate();

    private void WakeWordDetected(string[] arg0)
    {
        _voiceCommandReady = true;
        wakeWordDetected?.Invoke();

        if (_currentGracePeriodCoroutine == null) return;
        
        StopCoroutine(_currentGracePeriodCoroutine);
        _currentGracePeriodCoroutine = null;
    }

    private void OnPartialTranscription(string transcription)
    {
        if (_voiceCommandReady) 
        {
            transcriptionText.text = transcription;
        }
    }

    private void OnFullTranscription(string transcription)
    {
        if (!_voiceCommandReady) return;
        
        Debug.Log(transcription);
        completeTranscription?.Invoke(transcription);
        
        llmManager.SubmitImage(transcription);

        _voiceCommandReady = false; 

        if (_currentGracePeriodCoroutine != null)
        {
            StopCoroutine(_currentGracePeriodCoroutine);
        }
    }

    private IEnumerator GracePeriodCountdown()
    {
        listening = true;
        yield return new WaitForSeconds(listeningDuration);
        _currentGracePeriodCoroutine = null;
        _voiceCommandReady = false; 
        Debug.Log("TIMES UP");
    }

    private void HandleSpeechFinished(TTSSpeaker speaker, TTSClipData clip)
    {
        Debug.Log("ALL DONE!");
        _voiceCommandReady = true; 
        _currentGracePeriodCoroutine = StartCoroutine(GracePeriodCountdown());
    }
    
}