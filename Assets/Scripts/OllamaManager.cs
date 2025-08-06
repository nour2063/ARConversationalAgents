using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PassthroughCameraSamples;
using TMPro;
using ollama;

public class OllamaManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private FeedbackManager feedbackManager;
    [SerializeField] private CoquiTTSController speaker;

    [Header("Vision Model")] 
    [SerializeField] private string serverIP = "http://localhost:11434/";
    [TextArea(30,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string responsePrompt = "response";
    [SerializeField] private string comparePrompt = "compare";
    
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private SettingsManager settings;
    
    [Header("Image Captures")]
    [SerializeField] private Texture2D image;
    [SerializeField] private Texture2D image2;
    
    public bool processing;
    
    private bool _comparing;
    private readonly List<Message> _chatHistory = new ();
    
    private class Message
    {
        public readonly string Role;
        public string Content;

        public Message(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    private void Awake()
    {
        Ollama.SetServer(serverIP);
        
        // on-device testing
        if (serverIP == "http://localhost:11434/") Ollama.Launch();
        
        Debug.Log(Ollama.GetServer());
    }

    public void Start()
    {
        if (webcamManager == null) Debug.LogError("Webcam manager not set in PassthroughCameraLocal");
        if (resultText == null) Debug.LogError("ResultText UI not set in PassthroughCameraLocal");
    }

    public async void SubmitImage(string prompt)
    {
        if (processing) return;
        processing = true;

        var userMessage = new Message("user", prompt);
        
        if (_chatHistory.Count == 0)
        {
            userMessage.Content = initialPrompt + settings.activeSettings.agentPersonality + "\n------------------\nThe following is the user's initial message:" + "\n \n" + prompt;
        }
        
        if (speaker.IsListening() && _comparing)
        {
            userMessage.Content = comparePrompt + "\n \n" + prompt;
        } 
        else if (speaker.IsListening())
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
            imagesToSend = _comparing ? new [] {image, image2} : new [] { image };
        }

        string response;
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
            processing = false;
            return;
        }
        
        _chatHistory.Add(new Message("model", response));
        feedbackManager.ParseResponse(response);
    }

    public void CaptureImage(int idx = 0)
    {
        try
        {
            resultText.text = "Capturing...";
            var width = webcamManager.WebCamTexture.width;
            var height = webcamManager.WebCamTexture.height;
            
            var pixels = new Color32[width * height];
            webcamManager.WebCamTexture.GetPixels32(pixels);

            if (idx == 0)
            {
                image ??= new Texture2D(width, height);
                image.SetPixels32(pixels);
                image.Apply();
            }
            else
            {
                _comparing = true;
                image2 ??= new Texture2D(width, height);
                image2.SetPixels32(pixels);
                image2.Apply();
            }
        }
        catch
        {
            // testing in editor -- no camera
        }
    }

    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }
}
