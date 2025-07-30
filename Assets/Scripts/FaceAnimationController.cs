using UnityEngine;
using System.Collections;
using System.Linq;

public class FaceAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private string neutralLoopPath = "NeutralLoop";  // Path in Resources folder
    [SerializeField] private string happyLoopPath = "HappyLoop";      // Path for happy animation
    [SerializeField] private string angryLoopPath = "AngryLoop";      // Path for angry animation
    [SerializeField] private string sadLoopPath = "SadLoop";        // Path for sad animation
    [SerializeField] private string scaredLoopPath = "ScaredLoop";  // Path for scared animation
    [SerializeField] private string surprisedLoopPath = "SurprisedLoop"; // Path for surprised animation
    [SerializeField] private float frameRate = 24f;      // Animation frame rate

    // Future emotion animation paths
    /*
    [Header("Emotion Animation Paths")]
    [SerializeField] private string happyPath = "HappyAnimation";
    [SerializeField] private string sadPath = "SadAnimation";
    [SerializeField] private string angryPath = "AngryAnimation";
    [SerializeField] private string scaredPath = "ScaredAnimation";
    [SerializeField] private string surprisedPath = "SurprisedAnimation";
    */

    [Header("Animation Behavior")]
    [SerializeField] private bool loopNeutralAnimation = true;
    [SerializeField] private bool loopHappyAnimation = true;
    [SerializeField] private bool loopAngryAnimation = true;
    [SerializeField] private bool loopSadAnimation = true;
    [SerializeField] private bool loopScaredAnimation = true;
    [SerializeField] private bool loopSurprisedAnimation = true;
    
    private Material animatedMaterial;
    private float frameInterval;
    private int currentFrame = 0;
    private bool isPlaying = false;
    private Coroutine animationCoroutine;
    private Texture2D[] neutralFrames;
    private Texture2D[] happyFrames;
    private Texture2D[] angryFrames;
    private Texture2D[] sadFrames;
    private Texture2D[] scaredFrames;
    private Texture2D[] surprisedFrames;

    private void Start()
    {
        frameInterval = 1f / frameRate;
        LoadAnimationFrames();
        
        // Create a new material instance
        animatedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        SetupMaterial();
    }

    private void LoadAnimationFrames()
    {
        // Load neutral, happy, angry, sad, scared, and surprised animation frames
        LoadFramesForEmotion(neutralLoopPath, ref neutralFrames);
        LoadFramesForEmotion(happyLoopPath, ref happyFrames);
        LoadFramesForEmotion(angryLoopPath, ref angryFrames);
        LoadFramesForEmotion(sadLoopPath, ref sadFrames);
        LoadFramesForEmotion(scaredLoopPath, ref scaredFrames);
        LoadFramesForEmotion(surprisedLoopPath, ref surprisedFrames);

        // Future emotion frame loading
        /*
        LoadFramesForEmotion(happyPath, ref happyFrames);
        LoadFramesForEmotion(sadPath, ref sadFrames);
        LoadFramesForEmotion(angryPath, ref angryFrames);
        LoadFramesForEmotion(scaredPath, ref scaredFrames);
        LoadFramesForEmotion(surprisedPath, ref surprisedFrames);
        */
    }

    private void LoadFramesForEmotion(string path, ref Texture2D[] frames)
    {
        // Load all textures from the Resources folder
        Object[] loadedObjects = Resources.LoadAll(path, typeof(Texture2D));
        
        // Convert to Texture2D array and sort by name to ensure correct order
        frames = loadedObjects
            .Cast<Texture2D>()
            .OrderBy(tex => tex.name)
            .ToArray();

        Debug.Log($"Loaded {frames.Length} animation frames from {path}");
        
        if (frames.Length == 0)
        {
            Debug.LogWarning($"No frames found in Resources/{path}");
        }
    }

    private void SetupMaterial()
    {
        if (animatedMaterial != null)
        {
            // Configure the material for transparency
            animatedMaterial.EnableKeyword("_ALPHABLEND_ON");
            animatedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            
            // Set the material to be transparent
            animatedMaterial.SetFloat("_Surface", 1f); // 1 = Transparent
            animatedMaterial.SetFloat("_Mode", 3); // Transparent mode
            
            // Set up blending
            animatedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            animatedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            animatedMaterial.SetInt("_ZWrite", 0);
            animatedMaterial.renderQueue = 3000;
            
            // Ensure alpha channel is respected
            animatedMaterial.SetOverrideTag("RenderType", "Transparent");
            animatedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back); // Enable back-face culling
            
            // Set color to white with full alpha to not tint the texture
            animatedMaterial.color = new Color(1, 1, 1, 1);
            
            // Set initial frame
            if (neutralFrames != null && neutralFrames.Length > 0)
            {
                animatedMaterial.mainTexture = neutralFrames[0];
            }
        }
    }

    public void StartAnimation()
    {
        StartAnimation("neutral");
    }

    public void StartAnimation(string emotion)
    {
        // Always stop any current animation first
        StopAnimation();

        Texture2D[] targetFrames = null;
        bool shouldLoop = false;

        // Select the appropriate frames and loop setting based on emotion
        switch (emotion.ToLower())
        {
            case "neutral":
                targetFrames = neutralFrames;
                shouldLoop = loopNeutralAnimation;
                break;
            case "happy":
                targetFrames = happyFrames;
                shouldLoop = loopHappyAnimation;
                break;
            case "angry":
                targetFrames = angryFrames;
                shouldLoop = loopAngryAnimation;
                break;
            case "sad":
                targetFrames = sadFrames;
                shouldLoop = loopSadAnimation;
                break;
            case "scared":
                targetFrames = scaredFrames;
                shouldLoop = loopScaredAnimation;
                break;
            case "surprised":
                targetFrames = surprisedFrames;
                shouldLoop = loopSurprisedAnimation;
                break;
            default:
                Debug.LogWarning($"Unknown emotion for animation: {emotion}");
                return;
        }

        if (targetFrames != null && targetFrames.Length > 0)
        {
            currentFrame = 0;
            isPlaying = true;
            animationCoroutine = StartCoroutine(AnimateFrames(targetFrames, shouldLoop));
            Debug.Log($"Started {emotion} animation with {targetFrames.Length} frames, loop={shouldLoop}");
        }
        else
        {
            Debug.LogError($"No frames available for {emotion} animation");
        }
    }

    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
            Debug.Log("Stopped current animation");
        }
        isPlaying = false;
    }

    private IEnumerator AnimateFrames(Texture2D[] frames, bool loop)
    {
        while (isPlaying)
        {
            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                    Debug.Log("Animation loop restarting");
                }
                else
                {
                    isPlaying = false;
                    Debug.Log("Animation completed (not looping)");
                    break;
                }
            }

            if (frames[currentFrame] != null)
            {
                animatedMaterial.mainTexture = frames[currentFrame];
            }
            
            currentFrame++;
            yield return new WaitForSeconds(frameInterval);
        }
    }

    public Material GetAnimatedMaterial()
    {
        return animatedMaterial;
    }

    public void SetAlpha(float alpha)
    {
        if (animatedMaterial != null)
        {
            Color color = animatedMaterial.color;
            color.a = alpha;
            animatedMaterial.color = color;
        }
    }

    private void OnDestroy()
    {
        if (animatedMaterial != null)
        {
            Destroy(animatedMaterial);
        }
    }
} 