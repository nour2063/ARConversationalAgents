using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi.TTS.Utilities;
using Newtonsoft.Json;
using PassthroughCameraSamples;
using TMPro;
using ollama;

public class PassthroughCameraLocal : MonoBehaviour
{
    [Header("References")] 
    public TTSSpeaker speaker;
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private VoiceManager voiceManager;

    [Header("Vision Model")] 
    [SerializeField] private string serverIP = "http://localhost:11434/";
    [TextArea(30,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string responsePrompt = "response";
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI resultText;
    
    [System.Serializable]
    public class EmotionalAudioBurstData
    {
        public List<AudioClip> neutralClips = new List<AudioClip>();
        public List<AudioClip> fearClips = new List<AudioClip>();
        public List<AudioClip> happinessClips = new List<AudioClip>();
        public List<AudioClip> sadnessClips = new List<AudioClip>();
    }

    [Header("Emotional Audio Bursts")]
    [SerializeField] private AudioSource audioOutput;
    [SerializeField] private EmotionalAudioBurstData audioData = new EmotionalAudioBurstData();
    
    [Header("Debug Image")]
    [SerializeField] private Texture2D image;
    
    private bool _processing;
    private readonly List<Message> _chatHistory = new List<Message>();
    
    private class Message
    {
        public readonly string Role;
        public readonly string Content;

        public Message(string role, string content)
        {
            this.Role = role;
            this.Content = content;
        }
    }
    
    [System.Serializable]
    private class ChatResponse
    {
        [JsonProperty("response")]
        public string response;
        [JsonProperty("emotion")]
        public float[] emotion; // [pleasure, arousal, dominance]
    }

    private async void Awake()
    {
        Ollama.SetServer(serverIP);
        
        // on-device testing
        if (serverIP == "http://localhost:11434/")
        {
            Ollama.Launch();
        }
        
        Debug.Log(Ollama.GetServer());
        var response = await Ollama.Generate("gemma3:12b", "Hey there!");
        Debug.Log(response);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        if (webcamManager == null)
        {
            Debug.LogError("Webcam manager not set in PassthroughCameraLocal");
        }

        if (resultText == null)
        {
            Debug.LogError("ResultText UI not set in PassthroughCameraLocal");
        }

        if (image != null)
        {
            SubmitImage("What do you think of this?");
        } 
    }

    public async void SubmitImage(string prompt)
    {
        
        if (_processing) return;
        _processing = true;

        if (_chatHistory.Count == 0)
        {
            var systemMessage = new Message("system", initialPrompt);
            _chatHistory.Add(systemMessage);
        }

        if (voiceManager.listening)
        {
            var systemMessage = new Message("system", responsePrompt);
            _chatHistory.Add(systemMessage);
            voiceManager.listening = false;
        }
        
        // building user's current message

        _chatHistory.Add(new Message("user", prompt));
        
        // https://ai.google.dev/gemma/docs/core/prompt-structure
        var fullGemmaPrompt = _chatHistory.Aggregate("", (current, message) => current + message.Role switch
        {
            "user" => $"<start_of_turn>user\n{message.Content}<end_of_turn> ",
            "model" => $"<start_of_turn>model\n{message.Content}<end_of_turn> ",
            _ => $"{message.Content}\n"
        });

        fullGemmaPrompt += "<start_of_turn>model\n>";
        
        Debug.Log(fullGemmaPrompt);

        Texture2D[] imagesToSend = null;

        if (image != null)
        {
            imagesToSend = new Texture2D[] { image };
        }

        ChatResponse response = null;
        try
        {
            resultText.text = "making chat request...";
            response =
                await Ollama.GenerateJson<ChatResponse>("gemma3:12b", fullGemmaPrompt, images: imagesToSend);
            Debug.Log("Ollama response ok!");
            Debug.Log(response.response + response.emotion);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ollama API Error: {e.Message}");
            resultText.text = $"Ollama Error: {e.Message}. Check console.";
            _processing = false;
            return;
        }
        
        _chatHistory.Add(new Message("model", $"{response.emotion}, {response.response}"));
        Debug.Log(_chatHistory);

        ParseResponse(response);
    }

    public void CaptureImage()
    {
        try
        {
            resultText.text = "Capturing...";
            var width = webcamManager.WebCamTexture.width;
            var height = webcamManager.WebCamTexture.height;

            image ??= new Texture2D(width, height);

            var pixels = new Color32[width * height];
            webcamManager.WebCamTexture.GetPixels32(pixels);

            image.SetPixels32(pixels);
            image.Apply();
        }
        catch
        {
            // testing in editor -- no camera
        }
    }

    private void ParseResponse(ChatResponse response)
    {
        Debug.Log("parsing response...");
        var randomIndex = UnityEngine.Random.Range(0, 9);

        if (response.emotion is not { Length: 3 }) return;

        // todo: maybe there's a better approach here...
        
        var pleasure = (int)Math.Round(response.emotion[0]);
        var arousal = (int)Math.Round(response.emotion[1]);
        var sadness = (int)Math.Round(response.emotion[2]);
        
        var emotionState = (pleasure, arousal, sadness);
        
        switch (emotionState)
        {
            case (0, 0, 0):
                audioOutput.PlayOneShot(audioData.sadnessClips[randomIndex]);
                break;
            case (1, 0, 0):
            case (1, 1, 0):
            case (1, 1, 1):
                audioOutput.PlayOneShot(audioData.happinessClips[randomIndex]);
                break;
            case (0, 0, 1): 
            case (1, 0, 1):
            case (0, 1, 1):
                audioOutput.PlayOneShot(audioData.neutralClips[randomIndex]);
                break;
            case (0, 1, 0):
                audioOutput.PlayOneShot(audioData.fearClips[randomIndex]);
                break;
        }
        
        resultText.text = response.response;
        Debug.Log(response.response);
        speaker.SpeakQueued(response.response);
        _processing = false;
    }
    
    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }

}
