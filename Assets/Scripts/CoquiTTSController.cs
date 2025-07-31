using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class LocalNetworkTTS : MonoBehaviour
{
    [Header("Configuration")]
    public string serverIPAddress = "localhost";
    public int serverPort = 5000;
    public string coquiApiEndpoint = "/synthesize_speech";
    public string espeakApiEndpoint = "/synthesize_espeak";

    [Header("Voice Selection")]
    public string coquiSpeakerID = "";
    public string espeakVoiceID = "";

    [Header("State Management")]
    [SerializeField] private WhisperSTTController whisperSTTController;
    [SerializeField] private float listeningDuration = 10.0f;
    
    // --- THIS IS THE CRUCIAL ADDITION ---
    [Tooltip("Drag the GameObject with your WakeWordDetector script onto this slot.")]
    [SerializeField] private WakeWordDetector wakeWordDetector;

    private AudioSource _audioSource;
    private bool _isSpeaking = false;
    private bool _isListening = false;
    private readonly Queue<SpeechRequest> _speechQueue = new Queue<SpeechRequest>();

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        if (whisperSTTController == null) { Debug.LogError("WhisperSTTController is not assigned!", this); enabled = false; }
        if (wakeWordDetector == null) { Debug.LogError("WakeWordDetector is not assigned!", this); enabled = false; }
    }

    void OnEnable()
    {
        whisperSTTController.OnCommandListenTimeout += HandleListenEnd;
    }

    void OnDisable()
    {
        whisperSTTController.OnCommandListenTimeout -= HandleListenEnd;
    }

    public void Speak(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _speechQueue.Enqueue(new SpeechRequest(text, coquiSpeakerID, espeakVoiceID));
        if (!_isSpeaking)
        {
            StartCoroutine(ProcessSpeechQueue());
        }
    }

    private IEnumerator ProcessSpeechQueue()
    {
        _isSpeaking = true;
        while (_speechQueue.Count > 0)
        {
            SpeechRequest currentRequest = _speechQueue.Dequeue();
            var (url, bodyRaw) = PrepareWebRequest(currentRequest);
            using (var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) { Debug.LogError($"TTS Request failed: {www.error}"); continue; }

                AudioClip clip = null;
                bool isDecodingFinished = false;
                DecodeAudioAndCreateClip(www.downloadHandler.data, decodedClip =>
                {
                    clip = decodedClip;
                    isDecodingFinished = true;
                });
                yield return new WaitUntil(() => isDecodingFinished);

                if (clip != null)
                {
                    _audioSource.clip = clip;
                    _audioSource.Play();
                    yield return new WaitUntil(() => !_audioSource.isPlaying);
                    Destroy(clip);
                }
            }
        }
        _isSpeaking = false;
        HandleSpeechCompletion();
    }

    private void HandleSpeechCompletion()
    {
        ListeningPeriod();
    }
    
    // --- THIS METHOD NOW CONTAINS THE FIX ---
    private void ListeningPeriod()
    {
        Debug.Log("TTS has finished. Preparing for follow-up command...");
        _isListening = true;

        // 1. Tell the wake word engine to stop and release the microphone.
        Debug.Log("Stopping wake word listener...");
        wakeWordDetector.StopWakeWordListening();
        
        // 2. Now it's safe to start the command listener.
        whisperSTTController.StartListeningForCommand(listeningDuration);
    }
    
    private void HandleListenEnd()
    {
        // This is called by an event from Whisper. It signals that the whole turn is over.
        // The WakeWordDetector is responsible for restarting itself via this same event.
        _isListening = false;
    }

    // --- The rest of your script (helpers, etc.) is correct and unchanged ---
    public bool IsSpeaking() => _isSpeaking;
    public bool IsListening() => _isListening;
    private async void DecodeAudioAndCreateClip(byte[] data, Action<AudioClip> callback){ float[] samples = null; int channels = 0; int sampleRate = 0; try { await Task.Run(() => { samples = WavUtility.GetSamplesFromWav(data, out channels, out sampleRate); }); } catch (Exception e) { Debug.LogError($"Error decoding audio samples: {e.Message}"); callback?.Invoke(null); return; } if (samples == null) { callback?.Invoke(null); return; } AudioClip clip = AudioClip.Create("TTS_Clip", samples.Length / channels, channels, sampleRate, false); clip.SetData(samples, 0); callback?.Invoke(clip); }
    private (string, byte[]) PrepareWebRequest(SpeechRequest request) { var currentApiEndpoint = !string.IsNullOrEmpty(request.EspeakVoiceID) ? espeakApiEndpoint : coquiApiEndpoint; var requestJson = ""; if (!string.IsNullOrEmpty(request.EspeakVoiceID)) requestJson = JsonUtility.ToJson(new EspeakRequestData { Text = request.Text, VoiceID = request.EspeakVoiceID }); else requestJson = JsonUtility.ToJson(new TextToSpeakData { Text = request.Text, Speaker = request.CoquiSpeakerID }); var url = $"http://{serverIPAddress}:{serverPort}{currentApiEndpoint}"; return (url, System.Text.Encoding.UTF8.GetBytes(requestJson)); }
    private class TextToSpeakData { public string Text; public string Speaker; }
    private class EspeakRequestData { public string Text; public string VoiceID; }
    private class SpeechRequest { public readonly string Text; public readonly string CoquiSpeakerID; public readonly string EspeakVoiceID; public SpeechRequest(string text, string coqui, string espeak) { Text = text; CoquiSpeakerID = coqui; EspeakVoiceID = espeak; } }
}