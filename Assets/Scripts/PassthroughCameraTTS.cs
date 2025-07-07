using System;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Meta.WitAi.TTS.Utilities;
using PassthroughCameraSamples;
using TMPro;

public class PassthroughCameraTTS : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private OpenAIConfiguration configuration;
    [SerializeField] private TTSSpeaker speaker;
    
    [Header("Vision Model")]
    [TextArea(30,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    
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
    private OpenAIClient _api;
    
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

        if (resultText == null)
        {
            Debug.LogError("ResultText UI is not set in PassthroughCameraDescription");
        }
        
        // Debug
        if (image != null)
        {
            SubmitImage("What do you think of this?");
        }

        _api = new OpenAIClient(configuration);
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
        if (_processing) return;
        _processing = true;

        resultText.text = "making chat request...";

        if (_chatHistory.Count == 0)
        {
            var systemMessage = new Message(Role.System, initialPrompt);
            _chatHistory.Add(systemMessage);
        }

        // building user's current message
        Message request;
        if (image == null) // editor testing -- no camera
        {
            request = new Message(Role.User, prompt);
            Debug.Log("making chat request...");
        }
        else
        {
            var contents = new List<Content> {image, prompt};
            request = new Message(Role.User, contents);
        }

        // full list of messages to send
        var messagesToSend = new List<Message>(_chatHistory) { request };

        // api call with constructed list
        var chatRequest = new ChatRequest(messagesToSend, model: Model.GPT4o);
        var result = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
        Debug.Log("obtained completion result...");

        string response = result.FirstChoice;
        
        // updating chat history
        _chatHistory.Add(new Message(Role.User, prompt));
        _chatHistory.Add(new Message(Role.Assistant, response));

        resultText.text = response;
        ParseResponse(response);

        _processing = false;
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

    private void ParseResponse(string text)
    {
        Debug.Log("parsing response...");
        var randomIndex = UnityEngine.Random.Range(0, 9); 
        
        const string pattern = @"^\[\((\d+\.?\d*),\s*(\d+\.?\d*),\s*(\d+\.?\d*)\),\s*""([^""]*)""\]$";
        var match = Regex.Match(text, pattern);

        if (!match.Success) return;
        
        var pleasure = float.Parse(match.Groups[1].Value);
        var arousal = float.Parse(match.Groups[2].Value);
        var dominance = float.Parse(match.Groups[3].Value);
            
        var message = match.Groups[4].Value;
            
        // todo: maybe there's a better approach here...
        var emotionState = ((int)Math.Round(pleasure), (int)Math.Round(arousal), (int)Math.Round(dominance));
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
            
        speaker.Speak(message);
    }
    
    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }
}
