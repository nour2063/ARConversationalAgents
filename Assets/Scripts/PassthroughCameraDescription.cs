using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using PassthroughCameraSamples;
using TMPro;

public class PassthroughCameraDescription : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private OpenAIConfiguration configuration;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("UI")]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string imagePrompt = "What's in this image?";
    
    [Header("Debug Image")]
    [SerializeField] private Texture2D image;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (webcamManager == null)
        {
            Debug.LogError("Webcam manager is not set in PassthroughCameraDescription");
        }

        if (configuration == null)
        {
            Debug.LogError("OpenAI Configuration is not set in PassthroughCameraDescription");
        }

        if (resultText == null)
        {
            Debug.LogError("ResultText UI is not set in PassthroughCameraDescription");
        }

        if (image != null)
        {
            SubmitImage();
        }
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
