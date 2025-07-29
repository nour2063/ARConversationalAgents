using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oculus.Platform;
using PassthroughCameraSamples;
using TMPro;
using ollama;

public class PassthroughCameraLocal : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private LocalNetworkTTS speaker;

    [Header("Vision Model")] 
    [SerializeField] private string serverIP = "http://localhost:11434/";
    [TextArea(30,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string responsePrompt = "response";
    
    [Space(10)] 
    [Header("Blob Colour Settings")] 
    [SerializeField] private ColorManager polygon;
    [SerializeField] private ColorManager shell;
    [SerializeField] private ColorManager waveform1;
    [SerializeField] private ColorManager waveform2;

    [Header("Pleasure -> Color Gradient")]
    [Tooltip("Maps Pleasure (0-1) to this color gradient.")]
    public Gradient pleasureGradient;

    [Header("Arousal -> Saturation")]
    [Tooltip("Maps Arousal (0-1) to this Saturation range.")]
    [Range(0f, 1f)] public float saturationMin = 0.4f;
    [Range(0f, 1f)] public float saturationMax = 1.0f;

    [Header("Dominance -> Control")]
    [Tooltip("Maps Dominance (0-1) to the Shell's Alpha.")]
    [Range(0f, 1f)] public float shellAlphaMin = 0.1f;
    [Range(0f, 1f)] public float shellAlphaMax = 0.9f;
    
    [Space(10)] 
    [Header("Blob Waveform Settings")]
    [SerializeField] private AdaptiveWaveform wave1;
    [SerializeField] private AdaptiveWaveform wave2;
    [Header("Arousal -> Amplitude")]
    public float amplitudeMin = 0.2f;
    public float amplitudeMax = 1.5f;

    [Header("Arousal -> Noise Speed")]
    public float noiseSpeedMin = 1f;
    public float noiseSpeedMax = 5f;

    [Header("Pleasure -> Noise Scale (Jaggedness)")]
    [Tooltip("Lower values are smoother, higher values are more jagged.")]
    public float noiseScaleMin_Pleasant = 5f;
    public float noiseScaleMax_Unpleasant = 20f;
    
    [Header("Dominance -> Rotation Speed")]
    public float rotationSpeedMin = 0f;
    public float rotationSpeedMax = 0.5f;

    [Space(10)]
    [Header("Arousal -> Blob Poly Count")]
    [Tooltip("Lower values are more acute, higher values are more smooth.")]
    [SerializeField] private PolygonalSphereGenerator polygonShape;
    [SerializeField] private PolygonalSphereGenerator shellShape;
    [SerializeField] private int minPoly = 2;
    [SerializeField] private int maxPoly = 6;
    
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

        JObject jsonResponse;
        
        try
        {
            jsonResponse = JObject.Parse(response[8..^3]);
        }
        catch
        {
            _processing = false;
            return; // no emotion to give.
        }

        var emotion = jsonResponse["emotion"]?.ToObject<float[]>();
        if (emotion is not { Length: 3 }) return;
        
        var pleasure = (int)Math.Round(emotion[0]);
        var arousal = (int)Math.Round(emotion[1]);
        var dominance = (int)Math.Round(emotion[2]);

        SetColors(pleasure, arousal, dominance);
        SetWaveformProperties(pleasure, arousal, dominance);
        SetLoD(arousal);
        
        // todo -- TEMPORARY SOLUTION read paper thoroughly and match bursts with P-A ONLY REMOVE D
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
        speaker.Speak(message);
        _processing = false;
    }
    
    public void ClearChatHistory()
    {
        _chatHistory.Clear();
    }
    
    // Mapping Pleasure to Hue
    // Gao, X., Xin, J., Sato, T., Hansuebsai, A., Scalzo, M., Kajiwara, K., & Guan, S. S. (2007). Analysis of cross-cultural color emotion. Color Research & Application, 32(3), 223-229.
    // 
    // Mapping Dominance to Contrast
    // Itten, J. (1970). The Elements of Color: A Treatise on the Color System of Johannes Itten. Van Nostrand Reinhold.
    // 
    // PAD Model
    // Mehrabian, A., & Russell, J. A. (1974). An approach to environmental psychology. The MIT Press.
    //
    // Mapping Arousal to Saturation and Value + Mapping pleasure to hue
    // Valdez, P., & Mehrabian, A. (1994). Effects of color on emotions. Journal of Experimental Psychology: General, 123(4), 394–409.
    // 
    // Mapping Dominance to Contrast and Transparency
    // Ware, C. (2021). Information Visualization: Perception for Design (4th ed.). Morgan Kaufmann Publishers Inc.
    
    private void SetColors(float p, float a, float d)
    {
        // 1. PLEASURE -> HUE
        // Get the base color from the gradient you designed.
        Color baseColor = pleasureGradient.Evaluate(p);
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        // 2. AROUSAL -> SATURATION
        // Arousal controls how intense/muted the color is.
        s = Mathf.Lerp(saturationMin, saturationMax, a);
        Color polygonColor = Color.HSVToRGB(h, s, v);

        // 3. DOMINANCE -> ALPHA & CONTRAST
        // Dominance controls shell transparency and waveform color contrast.
        float shellAlpha = Mathf.Lerp(shellAlphaMin, shellAlphaMax, d);
        Color shellColor = new Color(polygonColor.r, polygonColor.g, polygonColor.b, shellAlpha);

        Color waveform1Color = polygonColor;
        float complementaryHue = (h + 0.5f) % 1.0f;
        Color complementaryColor = Color.HSVToRGB(complementaryHue, s, v);
        Color waveform2Color = Color.Lerp(polygonColor, complementaryColor, d);
        
        polygon.SetColor(polygonColor);
        shell.SetColor(shellColor);
        waveform1.SetColor(waveform1Color);
        waveform2.SetColor(waveform2Color);
    }
    
    private void SetWaveformProperties(float p, float a, float d)
    {
        // Arousal controls amplitude and noise speed.
        var amplitude = Mathf.Lerp(amplitudeMin, amplitudeMax, a);
        wave1.amplitudeSpeaking = amplitude;
        wave2.amplitudeSpeaking = amplitude;
        
        var noiseSpeed = Mathf.Lerp(noiseSpeedMin, noiseSpeedMax, a);
        wave1.noiseSpeed = noiseSpeed;
        wave2.noiseSpeed = -noiseSpeed;
        
        // Pleasure controls noise scale.
        // We invert the lerp: low pleasure = high scale (jagged), high pleasure = low scale (smooth).
        var noiseScale = Mathf.Lerp(noiseScaleMax_Unpleasant, noiseScaleMin_Pleasant, p);
        wave1.noiseScale = noiseScale;
        wave2.noiseScale = noiseScale;

        // Dominance controls rotation speed.
        var rotationSpeed = Mathf.Lerp(rotationSpeedMin, rotationSpeedMax, d);
        wave1.rotationSpeedSpeaking = rotationSpeed;
        wave2.rotationSpeedSpeaking = rotationSpeed;
    }

    private void SetLoD(float a)
    {
        var detailLevel = Mathf.RoundToInt(Mathf.Lerp(maxPoly, minPoly, a));
        polygonShape.detailLevel = detailLevel;
        shellShape.detailLevel = detailLevel;
        StartCoroutine(polygonShape.DoChangeShapeAndAnimate());
        StartCoroutine(shellShape.DoChangeShapeAndAnimate());
    }

}
