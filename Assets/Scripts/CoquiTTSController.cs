using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CoquiTTSController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string serverIPAddress = "localhost";
    [SerializeField] private int serverPort = 5000;
    [SerializeField] private string coquiApiEndpoint = "/synthesize_speech";
    [SerializeField] private string espeakApiEndpoint = "/synthesize_espeak";

    [Header("Voice Selection")]
    [SerializeField] private string coquiSpeakerID = "";
    [SerializeField] private string espeakVoiceID = "";

    [Header("State Management")]
    [SerializeField] private WhisperSTTController whisperSTTController;
    [SerializeField] private float listeningDuration = 10.0f;
    
    [SerializeField] private WakeWordDetector wakeWordDetector;
    [SerializeField] private GameObject popup;

    private AudioSource _audioSource;
    private bool _isSpeaking;
    private bool _isListening;
    private int _interactionCount;
    
    private readonly Queue<SpeechRequest> _speechQueue = new ();

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        if (whisperSTTController == null)
        {
            Debug.LogError("WhisperSTTController is not assigned!", this); enabled = false;
        }
        if (wakeWordDetector != null) return;
        Debug.LogError("WakeWordDetector is not assigned!", this); enabled = false;
    }

    private void OnEnable()
    {
        whisperSTTController.OnCommandListenTimeout += HandleListenEnd;
    }

    private void OnDisable()
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
            var currentRequest = _speechQueue.Dequeue();
            var (url, bodyRaw) = PrepareWebRequest(currentRequest);
            
            using var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"TTS Request failed: {www.error}"); continue;
            }

            AudioClip clip = null;
            var isDecodingFinished = false;
            DecodeAudioAndCreateClip(www.downloadHandler.data, decodedClip =>
            {
                clip = decodedClip;
                isDecodingFinished = true;
            });
            
            yield return new WaitUntil(() => isDecodingFinished);

            if (!clip) continue;
            
            _audioSource.clip = clip;
            _audioSource.Play();
            
            yield return new WaitUntil(() => !_audioSource.isPlaying);
            
            Destroy(clip);
        }
        
        _isSpeaking = false;
        
        ListeningPeriod();
        
        _interactionCount++;
        if (_interactionCount > 1) popup.SetActive(true);
    }

    public void HandleCollision()
    {
        ListeningPeriod();
    }
    
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

    public bool IsSpeaking() => _isSpeaking;
    public bool IsListening() => _isListening;

    private static async void DecodeAudioAndCreateClip(byte[] data, Action<AudioClip> callback)
    {
        float[] samples = null; var channels = 0; var sampleRate = 0;
        
        try
        {
            await Task.Run(() => { samples = WavUtility.GetSamplesFromWav(data, out channels, out sampleRate); });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error decoding audio samples: {e.Message}"); callback?.Invoke(null); return;
        }

        if (samples == null)
        {
            callback?.Invoke(null); return;
        } 
        
        var clip = AudioClip.Create("TTS_Clip", samples.Length / channels, channels, sampleRate, false); 
        clip.SetData(samples, 0); callback?.Invoke(clip);
    }

    private (string, byte[]) PrepareWebRequest(SpeechRequest request)
    {
        var currentApiEndpoint = !string.IsNullOrEmpty(request.EspeakVoiceID) ? espeakApiEndpoint : coquiApiEndpoint; 
        string requestJson;

        if (!string.IsNullOrEmpty(request.EspeakVoiceID))
        {
            requestJson = JsonUtility.ToJson(new EspeakRequestData {
                Text = request.Text, VoiceID = request.EspeakVoiceID
            }); 
        } 
        else 
        {    
            requestJson = JsonUtility.ToJson(new TextToSpeakData {
            Text = request.Text, Speaker = request.CoquiSpeakerID 
            }); 
        }
        
        var url = $"http://{serverIPAddress}:{serverPort}{currentApiEndpoint}"; 
        return (url, System.Text.Encoding.UTF8.GetBytes(requestJson));
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

        public SpeechRequest(string text, string coqui, string espeak)
        {
            Text = text; CoquiSpeakerID = coqui; 
            EspeakVoiceID = espeak;
        }
    }
}