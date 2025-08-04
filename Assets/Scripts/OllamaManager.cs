using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PassthroughCameraSamples;
using TMPro;
using ollama;
using Unity.VisualScripting;
using Random = System.Random;

public class OllamaManager : MonoBehaviour
{
    [Header("Fridge Entity")]
    [SerializeField] private GameObject fridgeEntity;
    
    [Header("Feedback Settings")] 
    [SerializeField] private GameObject blobObject;
    [SerializeField] private GameObject faceObject;
    [SerializeField] private bool blob;
    [SerializeField] private bool face;
    [SerializeField] private bool thought; // kept part of facial expression mode
    [SerializeField] private bool color;
    [SerializeField] private bool sound;
    
    [Header("References")] 
    [SerializeField] private WebCamTextureManager webcamManager;
    [SerializeField] private CoquiTTSController speaker;

    [Header("Vision Model")] 
    [SerializeField] private string serverIP = "http://localhost:11434/";
    [TextArea(30,10)]
    [SerializeField] private string initialPrompt = "You are a helpful assistant.";
    [SerializeField] private string responsePrompt = "response";
    [SerializeField] private string comparePrompt = "compare";

    [Space(10)] [Header("Color Settings")] 
    [SerializeField] private ColorManager fridgeTop;
    
    [Header("Pleasure -> Hue Gradient")]
    [Tooltip("Maps Pleasure (0-1) to this color gradient.")]
    [SerializeField] private Gradient pleasureGradient;

    [Header("Arousal -> Saturation")]
    [Tooltip("Maps Arousal (0-1) to this Saturation range.")]
    [Range(0f, 1f)] [SerializeField] private float saturationMin = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float saturationMax = 1.0f;

    [Header("Dominance -> Transparency")]
    [Tooltip("Maps Dominance (0-1) to the Shell's Alpha.")]
    [Range(0f, 1f)] [SerializeField] private float shellAlphaMin = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float shellAlphaMax = 0.9f;
    
    [Space(10)] 
    [Header("Face Settings")]
    [SerializeField] private FaceController faceController;
    [SerializeField] private ThoughtBubbleController thoughtBubbleController;
    [SerializeField] private ColorManager faceBlush;
    [Range(0f, 1f)] [SerializeField] private float blushAlphaMin = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float blushAlphaMax = 0.9f;
    
    [Space(10)] 
    [Header("Blob Settings")] 
    [SerializeField] private ColorManager polygonColor;
    [SerializeField] private ColorManager shellColor;
    [SerializeField] private ColorManager waveform1Color;
    [SerializeField] private ColorManager waveform2Color;
    
    [Header("Blob Idle Animation Settings")] 
    [SerializeField] private IdleAnimator idleAnimator;
    [Range(0f, 1f)] [SerializeField] private float maxSpeed = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float minSpeed = 0.1f;
    
    [Header("Blob Waveform Settings")]
    [SerializeField] private AdaptiveWaveform wave1;
    [SerializeField] private AdaptiveWaveform wave2;
    
    [Header("Arousal -> Amplitude")]
    [Range(0f, 2f)] [SerializeField] private float amplitudeMin = 0.2f;
    [Range(0f, 2f)] [SerializeField] private float amplitudeMax = 1.5f;

    [Header("Arousal -> Noise Speed")]
    [Range(0f, 10f)] [SerializeField] private float noiseSpeedMin = 1f;
    [Range(0f, 10f)] [SerializeField] private float noiseSpeedMax = 5f;
    
    [Header("Pleasure -> Noise Scale (Jaggedness)")]
    [Tooltip("Lower values are smoother, higher values are more jagged.")]
    [Range(0f, 10f)] [SerializeField] private float noiseScaleMinPleasant = 1f;
    [Range(0f, 10f)] [SerializeField] private float noiseScaleMaxUnpleasant = 5f;
    
    [Header("Dominance -> Rotation Speed")]
    [Range(0f, 1f)] [SerializeField] private float rotationSpeedMin = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float rotationSpeedMax = 1f;
    
    [Header("Blob Poly Count")]
    [Tooltip("Lower values are more acute, higher values are more smooth.")]
    [SerializeField] private PolygonalSphereGenerator polygonShape;
    [SerializeField] private PolygonalSphereGenerator shellShape;
    [Range(0f, 10f)] [SerializeField] private int minPoly = 2;
    [Range(0f, 10f)] [SerializeField] private int maxPoly = 6;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private SettingsManager settings;
    
    [Serializable]
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
    
    [Header("Image Captures")]
    [SerializeField] private Texture2D image;
    [SerializeField] private Texture2D image2;
    
    private bool _processing;
    private bool _comparing;
    private readonly List<Message> _chatHistory = new List<Message>();
    
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
        
        Setup();
    }

    private void Setup()
    {
        blob = settings.activeSettings.blob;
        face = settings.activeSettings.face;
        color = settings.activeSettings.color;
        thought = settings.activeSettings.thought;
        sound = settings.activeSettings.sound;
        
        Debug.Log("Blob is set to " + blob);
        Debug.Log("Color is set to " + color);
        Debug.Log("Thought is set to " + thought);
        Debug.Log("Sound is set to " + sound);
        Debug.Log("Face is set to " + face);
        
        blobObject.SetActive(blob);
        faceObject.SetActive(face);
        fridgeTop.gameObject.SetActive(color);
    }

    public async void SubmitImage(string prompt)
    {
        
        if (_processing) return;
        _processing = true;

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
            _processing = false;
            return;
        }
        
        _chatHistory.Add(new Message("model", response));
        ParseResponse(response);
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

    private void ParseResponse(string response)
    {
        Debug.Log("parsing response...");

        JObject jsonResponse;
        
        try
        {
            jsonResponse = JObject.Parse(response[8..^3]);
        }
        catch
        {
            _processing = false;
            return; // no emotion to give, error parsing JSON output
        }

        var emotion = jsonResponse["emotion"]?.ToObject<float[]>();
        if (emotion is not { Length: 3 }) return;
        
        var pleasure = (int)Math.Round(emotion[0]);
        var arousal = (int)Math.Round(emotion[1]);
        var dominance = (int)Math.Round(emotion[2]);
        
        if (blob)
        {
            SetWaveformProperties(pleasure, arousal, dominance);
            SetLoD(pleasure);
            SetIdleSpeed(arousal);
            if (color)
            {
                SetBlobColors(pleasure, arousal, dominance);
            }
        }

        if (face)
        {
            var expression = GetExpression(pleasure, arousal, dominance);
            faceController.SetFaceExpression(expression);
            
            if (color)
            {
                faceBlush.SetColor(GetColor(pleasure, arousal, dominance));
            }   
            
            if (thought)
            {
                switch (expression)
                {
                    case "happy":
                        thoughtBubbleController.ShowHappyThought();
                        break;
                    case "sad":
                        thoughtBubbleController.ShowSadThought();
                        break;
                    case "angry":
                        thoughtBubbleController.ShowAngryThought();
                        break;
                    case "scared":
                        thoughtBubbleController.ShowScaredThought();
                        break;
                    case "surprised":
                        thoughtBubbleController.ShowSurprisedThought();
                        break;
                    case "neutral":
                        thoughtBubbleController.HideThought();
                        break;
                }
            }
        }
        
        if (color)
        {
            fridgeTop.SetColor(GetColor(pleasure, arousal, dominance));
        }   

        if (sound)
        {
            var expression = GetEmotionalBurst(pleasure, arousal);
            var randomIndex = UnityEngine.Random.Range(0, 9);
            switch (expression)
            {
                case "happiness":
                    audioOutput.PlayOneShot(audioData.happinessClips[randomIndex]);
                    break;
                case "sadness":
                    audioOutput.PlayOneShot(audioData.sadnessClips[randomIndex]);
                    break;
                case "neutral":
                    audioOutput.PlayOneShot(audioData.neutralClips[randomIndex]);
                    break;
                case "fear":
                    audioOutput.PlayOneShot(audioData.fearClips[randomIndex]);
                    break;
            }
        }

        var message = (string)jsonResponse["message"];
        Debug.Log(message);
        
        resultText.text = message;
        speaker.Speak(message);
        _processing = false;
    }

    // --- Mapping Pleasure-Arousal Space to 6 Discrete Emotions ---

    // The Russell (1980) citation provides the original, descriptive map (the "what").
    // It shows that emotions can be organized in a pleasure-arousal circle, with coordinates for
    // emotions like happy (high-pleasure, mid-arousal) and angry (low-pleasure, high-arousal).
    // Russell, J. A. (1980).
    // A circumplex model of affect.
    // Journal of Personality and Social Psychology, 39(6), 1161–1178.

    // The Barrett (2006) citation provides the modern, explanatory theory (the "how" and "why").
    // It explains how the brain uses those fundamental dimensions of pleasure and arousal (core affect) as raw ingredients to
    // construct the distinct emotional experiences we label as "happy," "sad," and "angry."
    // Barrett, L. F. (2006).
    // Solving the emotion paradox: Categorization and the experience of emotion.
    // Personality and Social Psychology Review, 10(1), 20–46.
    
    // Using Dominance to differentiate emotions like Anger (+D) from Fear (-D)
    // is a primary feature of the three-dimensional PAD model.
    // Mehrabian, A. (1995).
    // Framework for a comprehensive description and measurement of emotional states.
    // Genetic, Social, and General Psychology Monographs, 121(3), 339–363.
    
    private static string GetExpression(float p, float a, float d)
    {
        var pad = (p, a, d);   
        var possibleEmotions = new List<string>();

        // --- Neutral ---
        // High pleasure, low arousal
        if (pad is { p: >= 0.5f, a: < 0.5f })
        {
            possibleEmotions.Add("neutral");
        }

        // --- Happy ---
        // High pleasure, high arousal
        if (pad is { p: >= 0.5f, a: >= 0.5f })
        {
            possibleEmotions.Add("happy");
        }

        // --- Angry ---
        // Low pleasure, high arousal, high dominance
        if (pad is { p: < 0.5f, a: >= 0.5f, d: >= 0.5f })
        {
            possibleEmotions.Add("angry");
        }

        // --- Sad ---
        // Low pleasure, low arousal
        if (pad is { p: < 0.5f, a: < 0.5f })
        {
            possibleEmotions.Add("sad");
        }

        // --- Scared ---
        // Low pleasure, high arousal, low dominance
        if (pad is { p: < 0.5f, a: >= 0.5f, d: < 0.5f })
        {
            possibleEmotions.Add("scared");
        }

        // --- Surprised (secondary classification) ---
        // high arousal
        if (pad.a >= 0.7f)
        {
            possibleEmotions.Add("surprised");
        }
        
        // Choosing one random selection from any "valid" emotional states -- adding nuance for surprise state
        
        var random = new Random();
        var randomIndex = random.Next(0, possibleEmotions.Count);
        var expression = possibleEmotions[randomIndex];
        Debug.Log("Expression set to " + expression);
        return expression;
    }
    
    // --- Deterministic Mapping of PA plane to 4 Discrete Emotions ---
    
    // The specific coordinates for each emotion are informed by Mehrabian's three-dimensional PAD model.
    // Notably, the Low-Pleasure/High-Arousal quadrant is assigned to "Fear". In a fuller model, this
    // quadrant also contains "Anger", but the two are distinguished by Dominance (Fear is Low-Dominance,
    // Anger is High-Dominance). As "Fear" is the only required output for this quadrant, the entire
    // space is allocated to it.
    // Mehrabian, A. (1995).
    // Framework for a comprehensive description and measurement of emotional states.
    // Genetic, Social, and General Psychology Monographs, 121(3), 339–363.

    private static string GetEmotionalBurst(float p, float a)
    {
        return p switch
        {
            // -- Happy --
            // high pleasure, high arousal
            >= 0.5f when a >= 0.5f => "happiness",
            // -- Neutral --
            // High pleasure, low arousal
            >= 0.5f when a < 0.5f => "neutral",
            // Low pleasure, low arousal
            // Sadness
            < 0.5f when a < 0.5f => "sadness",
            // else (Low pleasure, high arousal)
            _ => "fear"
        };
    }
    
    // Mapping Pleasure to Hue
    // Gao, X., Xin, J., Sato, T., Hansuebsai, A., Scalzo, M., Kajiwara, K., & Guan, S. S. (2007).
    // Analysis of cross-cultural color emotion. Color Research & Application, 32(3), 223-229.
    // 
    // Mapping Dominance to Contrast
    // Itten, J. (1970). The Elements of Color: A Treatise on the Color System of Johannes Itten. Van Nostrand Reinhold.
    //
    // Mapping Arousal to Saturation and Value + Mapping pleasure to hue
    // Valdez, P., & Mehrabian, A. (1994). Effects of color on emotions. Journal of Experimental Psychology: General, 123(4), 394–409.
    // 
    // Mapping Dominance to Contrast and Transparency
    // Ware, C. (2021). Information Visualization: Perception for Design (4th ed.). Morgan Kaufmann Publishers Inc.
    
    private void SetBlobColors(float p, float a, float d)
    {
        // 1. Pleasure -> Hue
        var baseColor = pleasureGradient.Evaluate(p);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);

        // 2. Arousal -> Saturation
        s = Mathf.Lerp(saturationMin, saturationMax, a);
        var newColor = Color.HSVToRGB(h, s, v);

        // 3. Dominance -> Alpha & Contrast
        var shellAlpha = Mathf.Lerp(shellAlphaMin, shellAlphaMax, d);
        var newShellColor = new Color(newColor.r, newColor.g, newColor.b, shellAlpha);

        var complementaryHue = (h + 0.5f) % 1.0f;
        var complementaryColor = Color.HSVToRGB(complementaryHue, s, v);
        var newWaveformColor = Color.Lerp(newColor, complementaryColor, d);
        
        polygonColor.SetColor(newColor);
        shellColor.SetColor(newShellColor);
        waveform1Color.SetColor(newColor);
        waveform2Color.SetColor(newWaveformColor);
    }

    private Color GetColor(float p, float a, float d)
    {
        // 1. Pleasure -> Hue
        var baseColor = pleasureGradient.Evaluate(p);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);
        
        // 2. Arousal -> Saturation
        s = Mathf.Lerp(saturationMin, saturationMax, a);
        var saturatedColor = Color.HSVToRGB(h, s, v);
        
        // 3. Dominance -> Alpha & Contrast
        var alpha = Mathf.Lerp(blushAlphaMin, blushAlphaMax, d);
        var finalColor = new Color(saturatedColor.r, saturatedColor.g, saturatedColor.b, alpha);

        return finalColor;
    }
    
    
    // --- Arousal Mappings ---
    // Mapping Arousal to Motion Speed/Frequency (noiseSpeed)
    // Wilson, G., Romeo, P., & Brewster, S. A. (2016).
    // Mapping Abstract Visual Feedback to a Dimensional Model of Emotion.
    // In Proceedings of the 2016 CHI Conference on Human Factors in Computing Systems (pp. 1779-1787).

    // Mapping Arousal to Amplitude/Movement Size (amplitude)
    // Wallbott, H. G. (1998).
    // Bodily expression of emotion.
    // European journal of social psychology, 28(6), 879-896.
    
    // --- Pleasure Mappings ---
    // Mapping Pleasure to Curvature (noiseScale creating smooth vs. jagged lines)
    // Bar, M., & Neta, M. (2006).
    // Humans prefer curved visual objects.
    // Psychological Science, 17(8), 645-648.
    
    // --- Dominance Mappings ---
    // Mapping Dominance to Powerful/Assertive Motion (e.g., rotationSpeed)
    // This is based on the principle that stronger visual cues (like size or forceful motion) convey dominance.
    // Melcer, E., & Isbister, K. (2015).
    // Motion, Emotion, and Form: Exploring Affective Dimensions of Shape.
    // In Proceedings of the 2015 Annual Symposium on Computer-Human Interaction in Play (pp. 155-165).
    
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
        var noiseScale = Mathf.Lerp(noiseScaleMaxUnpleasant, noiseScaleMinPleasant, p);
        wave1.noiseScale = noiseScale;
        wave2.noiseScale = noiseScale;

        // Dominance controls rotation speed.
        var rotationSpeed = Mathf.Lerp(rotationSpeedMin, rotationSpeedMax, d);
        wave1.rotationSpeedSpeaking = rotationSpeed;
        wave2.rotationSpeedSpeaking = rotationSpeed;
    }

    // Pleasure -> Shape curvature
    // Bar, M., & Neta, M. (2006). Humans prefer curved visual objects. Psychological Science, 17(8), 645-648.
    // Participants prefer curvier shapes.
    //
    // Ramachandran, V. S., & Hubbard, E. M. (2001). Synaesthesia--a window into perception, thought and language. Journal of consciousness studies, 8(12), 3-34.
    // This paper explores the bouba kiki effect in detail
    //
    // Leder, H., Belke, B., Oeberst, A., & Augustin, D. (2004). A model of aesthetic appreciation and aesthetic judgments. British journal of psychology, 95(4), 489-508.
    // Their model supports the idea that simple, symmetrical, and curved objects are often processed more fluently and thus perceived as more pleasant.
    
    private void SetLoD(float p)
    {
        var detailLevel = Mathf.RoundToInt(Mathf.Lerp(minPoly, maxPoly, p));
        polygonShape.detailLevel = detailLevel;
        shellShape.detailLevel = detailLevel;
        StartCoroutine(polygonShape.DoChangeShapeAndAnimate());
        StartCoroutine(shellShape.DoChangeShapeAndAnimate());
    }
    
    // Speed -> Arousal
    //
    // Wilson, G., Romeo, P., & Brewster, S. A. (2016). Mapping Abstract Visual Feedback to a Dimensional Model of Emotion.
    // In Proceedings of the 2016 CHI Conference on Human Factors in Computing Systems (pp. 1779-1787).
    // "All participants mapped faster motion to higher arousal."
    //
    // Fagerlönn, J. (2015). A survey of affective expressions in abstract shapes and animations. Linköping University Electronic Press.
    // Confirms mapping of high speed to high arousal and low speed to low arousal
    //
    // Camurri, A., Lagerlöf, I., & Volpe, G. (2003). Recognizing emotion from dance movement: Comparison of different observers. 
    // Kinetic energy or biological motion like dance = perceived arousal -- bopping speed of blob is a simplified implementation of this energy cue

    private void SetIdleSpeed(float a)
    {
        var speed = Mathf.Lerp(minSpeed, maxSpeed, a);
        idleAnimator.hoverSpeed = speed;
        idleAnimator.breathSpeed = speed;
    }

    public void Reset()
    {
        if (fridgeEntity == null)
        {
            Debug.LogError("FridgeEntity Root has not been assigned in the OllamaManager Inspector! Cannot reset.");
            return;
        }
    
        // Remember the context
        var parent = fridgeEntity.transform.parent;
        var localPosition = fridgeEntity.transform.localPosition;
        var localRotation = fridgeEntity.transform.localRotation;
        var localScale = fridgeEntity.transform.localScale;

        // --- STEP 1: Instantiate a new clone ---
        var newFridge = Instantiate(fridgeEntity, parent);

        // Apply the saved local transform properties
        newFridge.transform.localPosition = localPosition;
        newFridge.transform.localRotation = localRotation;
        newFridge.transform.localScale = localScale;

        // --- STEP 2: NOW, destroy the original object ---
        Destroy(fridgeEntity);

        Debug.Log("FridgeEntity has been cloned and the original destroyed.");
    }
}
