using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using PassthroughCameraSamples;
using TMPro;

public class PassthroughCameraDescription : MonoBehaviour
{
    public WebCamTextureManager webcamManager;
    public OpenAIConfiguration configuration;
    public Texture2D image;
    public TextMeshProUGUI resultText;
    
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string imagePrompt = "What's in this image?";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (webcamManager.WebCamTexture != null)
        {
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                CaptureImage();
                SubmitImage();
            }
        }
    }

    public async void SubmitImage()
    {
        resultText.text = "Analyzing...";
        var api = new OpenAIClient(configuration);
        
        var messages = new List<Message>();
        Message systemMessage = new Message(Role.System, initialPrompt);
        
        List<Content> imageContents = new List<Content>();
        string textContent = imagePrompt;
        Texture2D imageContent = image;
        
        imageContents.Add(textContent);
        imageContents.Add(imageContent);

        Message imageMessage = new Message(Role.User, imageContents);
        
        messages.Add(systemMessage);
        messages.Add(imageMessage);
        
        resultText.text = "before chat request...";
        var chatRequest = new ChatRequest(messages, model: Model.GPT4o);
        
        resultText.text = "making chat request...";
        var result = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
        
        // Debug.Log(result.FirstChoice);
        resultText.text = result.FirstChoice;
    }

    public void CaptureImage()
    {
        resultText.text = "Capturing...";
        int width = webcamManager.WebCamTexture.width;
        int height = webcamManager.WebCamTexture.height;

        if (image == null)
        {
            image = new Texture2D(width, height);
        }
        
        Color32[] pixels = new Color32[width * height];
        webcamManager.WebCamTexture.GetPixels32(pixels);
        
        image.SetPixels32(pixels);
        image.Apply();
    }
}
