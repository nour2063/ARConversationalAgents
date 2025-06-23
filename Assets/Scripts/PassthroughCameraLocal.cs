using UnityEngine;
using System.Collections.Generic;
using Meta.WitAi.TTS.Utilities;
using Newtonsoft.Json;
using Oculus.Voice.Dictation;
using PassthroughCameraSamples;
using TMPro;
using ollama;
using UnityEditor.Experimental.GraphView;

public class PassthroughCameraLocal : MonoBehaviour
{
    [Header("References")] 
    public WebCamTextureManager webcamManager;
    public AppDictationExperience dictation;
    public TTSSpeaker speaker;

    [Header("Vision Model")] 
    [SerializeField] private string serverIP = "http://localhost:11434/";
    [SerializeField] private string visionModel = "gemma3:12b";
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI dictationText;
    
    [Header("Debug Image")]
    [SerializeField] private Texture2D image;
    
    private bool _resultLocked = false;
    
    public class Message
    {
        public string role;
        public string content;

        public Message(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    private void Awake()
    {
        Ollama.SetServer(serverIP);
        if (serverIP == "http://localhost:11434/")
        {
            Ollama.Launch();
        }
        resultText.text = Ollama.GetServer();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public async void Start()
    {
        if (webcamManager == null)
        {
            Debug.LogError("Webcam manager not set in PassthroughCameraLocal");
        }

        if (dictation == null)
        {
            Debug.LogError("Dictation manager not set in PassthroughCameraLocal");
        }

        if (speaker == null)
        {
            Debug.LogError("Speaker not set in PassthroughCameraLocal");
        }

        if (dictationText == null)
        {
            Debug.LogError("DictationText UI not set in PassthroughCameraLocal");
        }

        if (resultText == null)
        {
            Debug.LogError("ResultText UI not set in PassthroughCameraLocal");
        }

        if (image != null)
        {
            SubmitImage();
        } 
        var response = await Ollama.Generate(visionModel, "Hey there!");
        resultText.text = response;
    }

    // Update is called once per frame
    void Update()
    {
        if (!ReferenceEquals(webcamManager.WebCamTexture, null))
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
        var test = await Ollama.Generate(visionModel, "Hello");
        Debug.Log(test);
        
        if (_resultLocked) return;
        _resultLocked = true;
        
        var messages = new List<Message>
        {
            new Message("system", initialPrompt),
            new Message("user", dictationText.text)
        };
        
        var jsonPrompt = JsonConvert.SerializeObject(new { messages }, Formatting.Indented);
        
        var fullPrompt = "Please interpret the following input as JSON. The system message informs you on how to behave, and the user's message is contextualized with the image provided. Fulfill the role of the system and respond to the user directly.:\n" + jsonPrompt;
        
        var response = await Ollama.Generate(visionModel, fullPrompt, images: new [] { image });
        
        resultText.text = response;
        speaker.Speak(response);
        Debug.Log(response);
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

        image = ResizeTexture(image, 256, 256);
    }
    
    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D newTex = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        newTex.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        newTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return newTex;
    }

}
