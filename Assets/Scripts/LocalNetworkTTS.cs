using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;

public class LocalNetworkTTS : MonoBehaviour
{
    [Tooltip("The IP address or hostname of your local TTS server")]
    public string serverIPAddress = "localhost"; 
    [Tooltip("The port your local TTS server is listening on (e.g., 5000).")]
    public int serverPort = 5000; 
    [Tooltip("The API endpoint for your Coqui TTS synthesis (e.g., /synthesize_speech).")]
    public string coquiApiEndpoint = "/synthesize_speech"; 
    [Tooltip("The API endpoint for your eSpeak-NG synthesis (e.g., /synthesize_espeak).")]
    public string espeakApiEndpoint = "/synthesize_espeak"; 
    
    [Header("Voice Selection")]
    [Tooltip("Optional: Coqui TTS Speaker ID (e.g., 'p225'). Leave empty to use Coqui's default.")]
    public string coquiSpeakerID = ""; 
    [Tooltip("Optional: eSpeak-NG Voice ID (e.g., 'en-m', 'en-f3', 'en-Ø29'). If provided, will prioritize eSpeak-NG endpoint.")]
    public string espeakVoiceID = ""; 
    
    [Header("Listening Period Settings")]
    [SerializeField] WhisperSTTController whisperSTTController;
    [SerializeField] private float listeningDuration = 5.0f;

    public event Action OnSpeechFinished;

    private AudioSource _audioSource;
    private bool _isSpeaking = false;
    private bool _isListening = false;
    
    private readonly Queue<SpeechRequest> _speechQueue = new Queue<SpeechRequest>();
    private SpeechRequest _currentRequest = null;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            Debug.LogWarning("TTS Manager: No AudioSource found, added one automatically.");
        }
        _audioSource.spatialBlend = 0f; 
        _audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        OnSpeechFinished += HandleSpeechCompletion;
    }

    private void OnDisable()
    {
        OnSpeechFinished -= HandleSpeechCompletion;
    }

    public async Task Speak(string text) 
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("LocalNetworkTTS: Text to speak is empty. Ignoring.");
            return;
        }

        var newRequest = new SpeechRequest(text, coquiSpeakerID, espeakVoiceID);
        _speechQueue.Enqueue(newRequest);
        Debug.Log($"LocalNetworkTTS: Enqueued new speech request. Queue size: {_speechQueue.Count}");

        if (!_isSpeaking)
        {
            await ProcessNextSpeechRequest(); 
        }
    }

    private void Stop()
    {
        if (_isSpeaking)
        {
            _audioSource.Stop();
            _isSpeaking = false;
            _currentRequest = null; 
            Debug.Log("LocalNetworkTTS: Speech stopped.");
        }
        _speechQueue.Clear(); 
        Debug.Log("LocalNetworkTTS: Speech queue cleared.");
        OnSpeechFinished?.Invoke(); 
    }

    public bool IsSpeaking()
    {
        return _isSpeaking;
    }

    private async Task ProcessNextSpeechRequest()
    {
        while (true)
        {
            if (_isSpeaking || _speechQueue.Count == 0)
            {
                return;
            }

            _currentRequest = _speechQueue.Dequeue(); 
            _isSpeaking = true;

            var currentApiEndpoint = "";
            var requestJson = "";

            if (!string.IsNullOrEmpty(_currentRequest.EspeakVoiceID))
            {
                currentApiEndpoint = espeakApiEndpoint;
                requestJson = JsonUtility.ToJson(new EspeakRequestData { Text = _currentRequest.Text, VoiceID = _currentRequest.EspeakVoiceID });
                Debug.Log($"LocalNetworkTTS: Processing eSpeak-NG voice: '{_currentRequest.EspeakVoiceID}' for '{_currentRequest.Text}'");
            }
            else
            {
                currentApiEndpoint = coquiApiEndpoint;
                var coquiRequestData = new TextToSpeakData { Text = _currentRequest.Text };
                if (!string.IsNullOrEmpty(_currentRequest.CoquiSpeakerID))
                {
                    coquiRequestData.Speaker = _currentRequest.CoquiSpeakerID;
                }

                requestJson = JsonUtility.ToJson(coquiRequestData);
                Debug.Log($"LocalNetworkTTS: Processing Coqui voice (Speaker: {_currentRequest.CoquiSpeakerID ?? "Default"}) for '{_currentRequest.Text}'");
            }

            var url = $"http://{serverIPAddress}:{serverPort}{currentApiEndpoint}";
            var bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestJson);

            using (var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Accept", "audio/wav, audio/mpeg");

                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"LocalNetworkTTS: TTS server request to {currentApiEndpoint} failed: {www.error}");
                    _isSpeaking = false;
                }
                else
                {
                    Debug.Log($"LocalNetworkTTS: Audio data received from server ({www.downloadHandler.data.Length} bytes).");

                    var clip = WavUtility.ToAudioClip(www.downloadHandler.data);

                    if (clip != null)
                    {
                        _audioSource.Stop();
                        _audioSource.clip = clip;
                        _audioSource.Play();

                        // Wait until the audio finishes playing
                        await Task.Delay(Mathf.RoundToInt(clip.length * 1000));

                        _audioSource.clip = null;
                        Debug.Log("LocalNetworkTTS: Speech playback finished.");
                    }
                    else
                    {
                        Debug.LogError("LocalNetworkTTS: Failed to create AudioClip from received data.");
                    }
                }
            }

            _isSpeaking = false;
            _currentRequest = null;

            OnSpeechFinished?.Invoke(); 

            if (_speechQueue.Count > 0)
            {
                continue;
            }

            break;
        }
    }
    
    private void HandleSpeechCompletion()
    {
        Debug.Log("LocalNetworkTTS: Speech playback event received: Speech has finished.");
        ListeningPeriod();
    }
    
    private void ListeningPeriod()
    {
        _isListening = true;
        whisperSTTController.StartListeningForCommand(listeningDuration);
    }

    public bool IsListening()
    {
        return _isListening;
    }

    private void OnDestroy()
    {
        Stop();
    }

    private class TextToSpeakData 
    {
        public string Text;
        public string Speaker; 
    }

    private class EspeakRequestData 
    {
        public string Text;
        public string VoiceID; 
    }

    private class SpeechRequest
    {
        public readonly string Text;
        public readonly string CoquiSpeakerID;
        public readonly string EspeakVoiceID;

        public SpeechRequest(string text, string coquiSpeakerId, string espeakVoiceId)
        {
            this.Text = text;
            this.CoquiSpeakerID = coquiSpeakerId;
            this.EspeakVoiceID = espeakVoiceId;
        }
    }
}