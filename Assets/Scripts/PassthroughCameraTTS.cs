using System;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Meta.WitAi.TTS.Utilities;
using Newtonsoft.Json.Linq;
using PassthroughCameraSamples;
using TMPro;
using Exception = System.Exception;

public class PassthroughCameraTTS : MonoBehaviour
{
    [Header("References")]
    public TTSSpeaker speaker;
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private OpenAIConfiguration configuration;
    [SerializeField] private VoiceManager voiceManager;
    [SerializeField] private ColourManager colourManager;
    
    [Header("Vision Model")]
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
    private OpenAIClient _api;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (webcamManager == null)
        {
            Debug.LogError("Webcam manager is not set in PassthroughCameraTTS");
        }

        if (configuration == null)
        {
            Debug.LogError("OpenAI Configuration is not set in PassthroughCameraTTS");
        }

        if (speaker == null)
        {
            Debug.LogError("Speaker is not set in PassthroughCameraTTS");
        }

        if (resultText == null)
        {
            Debug.LogError("ResultText UI is not set in PassthroughCameraTTS");
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

        if (voiceManager.listening)
        {
            var systemMessage = new Message(Role.System, responsePrompt);
            _chatHistory.Add(systemMessage);
            voiceManager.listening = false;
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
        
        Debug.Log(result);
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

    private void ParseResponse(string response)
    {
        Debug.Log("parsing response...");
        var randomIndex = UnityEngine.Random.Range(0, 9);

        JObject jsonResponse;
        
        if (response.StartsWith("```json") && response.EndsWith("```"))
        {
            try
            {
                jsonResponse = JObject.Parse(response[8..^3]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse JSON with markdown delimiters: {ex.Message}");
                jsonResponse = null;
            }
        }
        else
        {
            try
            {
                jsonResponse = JObject.Parse(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse JSON directly: {ex.Message}");
                jsonResponse = null;
            }
        }

        if (jsonResponse == null)
        {
            Debug.LogError("Could not parse JSON from the response string.");
        }

        var emotion = jsonResponse["emotion"]?.ToObject<float[]>();
        if (emotion is not { Length: 3 }) return;
        
        var pleasure = (int)Math.Round(emotion[0]);
        var arousal = (int)Math.Round(emotion[1]);
        var dominance = (int)Math.Round(emotion[2]);
        
        // todo: this is arbitrary -- figure out how to make colour make sense
        colourManager.SetColor(0, new Color(0.4f, pleasure, 0.4f, 1f)); // green
        colourManager.SetColor(1, new Color(0.4f, 0.4f, arousal, 1f)); // blue
        colourManager.SetColor(2, new Color(dominance, 0.4f, 0.4f, 1f)); // red
        
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
        speaker.SpeakQueued(message);
        _processing = false;
    }
    
    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }
}
