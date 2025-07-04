using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Meta.WitAi.TTS.Utilities;
using Oculus.Voice.Dictation;
using PassthroughCameraSamples;
using TMPro;
using Unity.VisualScripting;

public class PassthroughCameraTTS : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private OpenAIConfiguration configuration;
    [SerializeField] private TTSSpeaker speaker;
    
    [Header("Vision Model")]
    [TextArea(10,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI dictationText;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("Debug Image")]
    [SerializeField] private Texture2D image;
    
    private bool _resultLocked = false;
    
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

        if (speaker == null)
        {
            Debug.LogError("Speaker is not set in PassthroughCameraDescription");
        }

        if (dictationText == null)
        {
            Debug.LogError("DictationText UI is not set in PassthroughCameraDescription");
        }

        if (resultText == null)
        {
            Debug.LogError("ResultText UI is not set in PassthroughCameraDescription");
        }
        
        // Debug
        if (image != null)
        {
            SubmitImage("What do you think of this?");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Debug activation
        // if (!ReferenceEquals(webcamManager.WebCamTexture, null))
        // {
        //     if (OVRInput.GetDown(OVRInput.RawButton.A))
        //     {
        //         dictation.Activate();
        //         CaptureImage();
        //     }
        // }
    }

    public async void SubmitImage(string prompt)
    {
        if (_resultLocked) return;
        _resultLocked = true;
        
        resultText.text = "making chat request...";
        
        var api = new OpenAIClient(configuration);
        
        var messages = new List<Message>();
        Message systemMessage = new Message(Role.System, initialPrompt);
        
        List<Content> imageContents = new List<Content>();
        Texture2D imageContent = image;
        
        imageContents.Add(prompt);
        imageContents.Add(imageContent);

        Message imageMessage = new Message(Role.User, imageContents);
        
        messages.Add(systemMessage);
        messages.Add(imageMessage);
        
        var chatRequest = new ChatRequest(messages, model: Model.GPT4o);
        var result = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
        
        resultText.text = result.FirstChoice;
        ParseResponse(result.FirstChoice);
    }

    public void CaptureImage()
    {
        _resultLocked = false;
        
        resultText.text = "Capturing...";
        int width = webcamManager.WebCamTexture.width;
        int height = webcamManager.WebCamTexture.height;

        if (ReferenceEquals(image, null))
        {
            image = new Texture2D(width, height);
        }
        
        Color32[] pixels = new Color32[width * height];
        webcamManager.WebCamTexture.GetPixels32(pixels);
        
        image.SetPixels32(pixels);
        image.Apply();
    }

    private void ParseResponse(string text)
    {
        Debug.Log("parsing response...");
        
        const string pattern = @"^\[\((\d+\.?\d*),\s*(\d+\.?\d*),\s*(\d+\.?\d*)\),\s*""([^""]*)""\]$";
        var match = Regex.Match(text, pattern);

        if (match.Success)
        {
            var pleasure = float.Parse(match.Groups[1].Value);
            var arousal = float.Parse(match.Groups[2].Value);
            var dominance = float.Parse(match.Groups[3].Value);
            var message = match.Groups[4].Value;
            
            (float, float, float) emotionState = (pleasure, arousal, dominance);
            Debug.Log(emotionState);
            
            speaker.Speak(message);
        }
    }
}
