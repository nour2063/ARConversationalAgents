using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi.TTS.Utilities;
using Oculus.Voice.Dictation;
using PassthroughCameraSamples;
using TMPro;

public class PassthroughCameraTTS : MonoBehaviour
{
    public WebCamTextureManager webcamManager;
    public OpenAIConfiguration configuration;
    public Texture2D image;
    public TextMeshProUGUI resultText;
    public AppDictationExperience dictation;
    public TextMeshProUGUI dictationText;
    public TTSSpeaker speaker;
    
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private int chatEndpointDelay = 1000;
    
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
                dictation.Activate();
                CaptureImage();
            }
        }
    }

    public async void SubmitImage()
    {
        var api = new OpenAIClient(configuration);
        
        var messages = new List<Message>();
        Message systemMessage = new Message(Role.System, initialPrompt);
        
        List<Content> imageContents = new List<Content>();
        string textContent = dictationText.text;
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
        
        await Task.Delay(chatEndpointDelay); // making sure that the result is complete
        resultText.text = result.FirstChoice;
        speaker.Speak(result.FirstChoice);
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
