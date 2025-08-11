using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    [SerializeField] private string model = "gemma3:12b";
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

    // --- 1. Prepare the content for this turn ---
    var finalUserContent = prompt;
    var imageTokens = "";
    Texture2D[] imagesToSend = null;

    // Check if this is the very first user turn to prepend the main system prompt
    if (_chatHistory.Count == 0)
    {
        finalUserContent = $"{initialPrompt}{settings.activeSettings.agentPersonality}\n\n------------------" +
                           $"This is the user's first message:\n\n{finalUserContent}";
    }

    // Handle image-specific modes and turn-specific instructions
    if (image != null)
    {
        if (speaker.IsListening() && _comparing)
        {
            // Prepend the "compare" instruction and prepare two image tokens
            finalUserContent = $"{comparePrompt}\n\n{finalUserContent}";
            imageTokens = "<image>\n<image>\n";
            imagesToSend = new[] { image, image2 };
        }
        else 
        {
            // Handle the "quick reply" instruction if applicable
            if (speaker.IsListening())
            {
                finalUserContent = $"{responsePrompt}\n\n{finalUserContent}";
            }
            imageTokens = "<image>\n";
            imagesToSend = new[] { image };
        }
    }

    // Create the final user message object for the history
    var userMessage = new Message("user", $"{imageTokens}{finalUserContent}");
    _chatHistory.Add(userMessage);

    // --- 2. Build the entire prompt string from history ---
    // Tokenization based on https://huggingface.co/openbmb/MiniCPM-Llama3-V-2_5
    var promptBuilder = new StringBuilder();
    foreach (var message in _chatHistory)
    {
        switch (message.Role)
        {
            case "user":
                promptBuilder.Append("<s>[INST] ").Append(message.Content).Append(" [/INST]");
                break;
            case "assistant":
                promptBuilder.Append(message.Content).Append("</s>");
                break;
        }
    }
    var fullPrompt = promptBuilder.ToString();

    // --- 3. Send the request to Ollama ---
    string response;
    try
    {
        resultText.text = "making chat request...";
        response = await Ollama.Generate(model, fullPrompt, images: imagesToSend);
        Debug.Log("Ollama response ok!");
        Debug.Log(response);
    }
    catch (Exception e)
    {
        Debug.LogError($"Ollama API Error: {e.Message}");
        resultText.text = $"Ollama Error: {e.Message}. Check console.";
        // If the request fails, remove the user message added
        _chatHistory.Remove(userMessage);
        processing = false;
        return;
    }

    // --- 4. Handle the response ---
    _chatHistory.Add(new Message("assistant", response));
    feedbackManager.ParseResponse(response);
    processing = false;
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
