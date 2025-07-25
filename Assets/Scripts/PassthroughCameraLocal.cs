using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PassthroughCameraSamples;
using TMPro;
using ollama;

public class PassthroughCameraLocal : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private LocalNetworkTTS speaker;
    [SerializeField] private ColourManager colourManager;

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
        public string Content;

        public Message(string role, string content)
        {
            this.Role = role;
            this.Content = content;
        }
    }

    private void Awake()
    {
        Ollama.SetServer(serverIP);
        
        // on-device testing
        if (serverIP == "http://localhost:11434/")
        {
            Ollama.Launch();
        }
        
        Debug.Log(Ollama.GetServer());
        
        // debug
        // SubmitImage("Hey there!");
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

        // Editor debug
        if (image != null)
        {
            // SubmitImage("What do you see?");
            // SubmitImage("What have you got for me? Give me a full rundown!");
            // SubmitImage("What should I make for dinner?");
        } 
    }

    public async void SubmitImage(string prompt)
    {
        
        if (_processing) return;
        _processing = true;

        var userMessage = new Message("user", prompt);
        
        if (_chatHistory.Count == 0)
        {
            userMessage.Content = initialPrompt + "\n \n" + prompt;
        }
        
        if (speaker.IsListening())
        {
            userMessage.Content = responsePrompt + "\n \n" + prompt;
        }
        
        // building user's current message

        _chatHistory.Add(userMessage);
        
        // https://ai.google.dev/gemma/docs/core/prompt-structure
        var fullGemmaPrompt = _chatHistory.Aggregate("", (current, message) => current + message.Role switch
        {
            "model" => $"<start_of_turn>model\n{message.Content}<end_of_turn>\n",
            _ => $"<start_of_turn>user\n{message.Content}<end_of_turn>\n",
        });

        fullGemmaPrompt += "<start_of_turn>model\n";
        
        Debug.Log(fullGemmaPrompt);

        Texture2D[] imagesToSend = null;

        // Editor debug
        if (image != null)
        {
            imagesToSend = new Texture2D[] { image };
        }

        string response = null;
        try
        {
            resultText.text = "making chat request...";
            response =
                await Ollama.Generate("gemma3:12b", fullGemmaPrompt, images: imagesToSend);
            
            Debug.Log("Ollama response ok!");
            Debug.Log(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ollama API Error: {e.Message}");
            resultText.text = $"Ollama Error: {e.Message}. Check console.";
            _processing = false;
            return;
        }
        
        _chatHistory.Add(new Message("model", response));
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

    private void ParseResponse(string response)
    {
        Debug.Log("parsing response...");
        var randomIndex = UnityEngine.Random.Range(0, 9);
        
        var jsonResponse = JObject.Parse(response[8..^3]);

        var emotion = jsonResponse["emotion"]?.ToObject<float[]>();
        if (emotion is not { Length: 3 }) return;
        
        var pleasure = (int)Math.Round(emotion[0]);
        var arousal = (int)Math.Round(emotion[1]);
        var dominance = (int)Math.Round(emotion[2]);
        
        // // todo: this is arbitrary -- figure out how to make colour make sense
        // colourManager.SetColor(0, new Color(0.4f, 1f, 0.4f, pleasure));
        // colourManager.SetColor(1, new Color(0.4f, 0.4f, 1f, arousal));
        // colourManager.SetColor(2, new Color(1f, 0.4f, 0.4f, dominance));
        
        // todo -- TEMPORARY SOLUTION read paper thoroughly and match bursts with P-A
        var emotionBurst = (pleasure, arousal, dominance);
        
        switch (emotionBurst)
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

        var message = (string)jsonResponse["message"];
        Debug.Log(message);
        
        resultText.text = message;
        _ = speaker.Speak(message);
        _processing = false;
    }
    
    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }

}
