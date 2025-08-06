using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

// Pulled from https://github.com/Torquoal/AZRAM/
public class ThoughtBubbleController : MonoBehaviour
{
    [SerializeField] private Transform cameraRig;
    
    [Header("Bubble Components")]
    [SerializeField] private GameObject mainBubble;
    [SerializeField] private GameObject smallBubble1;
    [SerializeField] private GameObject smallBubble2;
    [SerializeField] private Image thoughtImage;
    [SerializeField] private CanvasGroup mainBubbleGroup;
    [SerializeField] private CanvasGroup smallBubble1Group;
    [SerializeField] private CanvasGroup smallBubble2Group;

    [Header("Animation Settings")]
    [SerializeField] private float bubbleAppearDelay = 0.3f;
    [SerializeField] private float bubbleFadeDuration = 0.5f;
    [SerializeField] private float floatSpeed = 0.5f;
    [SerializeField] private float floatAmount = 0.1f;
    
    [Header("Thought Images")]
    [SerializeField] private Sprite[] thoughtSprites; // Array of different thought images/emojis
    
    private Vector3[] initialPositions;
    private bool isVisible = false;
    private Coroutine floatingCoroutine;
    private Vector3 mainBubbleBasePos;
    private Vector3 smallBubble1BasePos;
    private Vector3 smallBubble2BasePos;

    void Start()
    {
        Debug.Log("ThoughtBubbleController Start called");
        
        // Validate thought sprites
        if (thoughtSprites != null)
        {
            Debug.Log($"Number of thought sprites: {thoughtSprites.Length}");
            for (int i = 0; i < thoughtSprites.Length; i++)
            {
                if (thoughtSprites[i] == null)
                {
                    Debug.LogError($"Thought sprite at index {i} is null!");
                }
            }
        }
        
        if (cameraRig == null)
        {
            Debug.LogError("Camera Rig not found!");
        }
        
        // Check if all required components are assigned
        if (mainBubble == null) Debug.LogError("Main Bubble not assigned!");
        if (smallBubble1 == null) Debug.LogError("Small Bubble 1 not assigned!");
        if (smallBubble2 == null) Debug.LogError("Small Bubble 2 not assigned!");
        if (thoughtImage == null) Debug.LogError("Thought Image not assigned!");
        if (thoughtSprites == null || thoughtSprites.Length == 0) Debug.LogError("No thought sprites assigned!");
        
        // Store initial positions
        mainBubbleBasePos = mainBubble.transform.localPosition;
        smallBubble1BasePos = smallBubble1.transform.localPosition;
        smallBubble2BasePos = smallBubble2.transform.localPosition;

        Debug.Log($"Initial positions stored: Main={mainBubbleBasePos}, Small1={smallBubble1BasePos}, Small2={smallBubble2BasePos}");

        // Initially hide all bubbles
        SetBubblesVisible(false);
    }

    void LateUpdate()
    {
        // Always face the main camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - transform.position;
            directionToCamera.y = 0; // Keep upright by zeroing out y component
            
            if (directionToCamera != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera.normalized, Vector3.up);
                // Smoothly interpolate the rotation for smoother movement
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }

    public void ShowThought(string emotion)
    {
        Debug.Log($"ShowThought called with emotion: {emotion}");
        
        // Validate sprites array
        if (thoughtSprites == null)
        {
            Debug.LogError("thoughtSprites array is null!");
            return;
        }
        
        if (thoughtSprites.Length == 0)
        {
            Debug.LogError("thoughtSprites array is empty!");
            return;
        }
        
        // Find the appropriate sprite based on emotion
        Sprite thoughtSprite = null;
        for (int i = 0; i < thoughtSprites.Length; i++)
        {
            if (thoughtSprites[i] == null)
            {
                Debug.LogError($"Sprite at index {i} is null!");
                continue;
            }
            
            if (thoughtSprites[i].name.ToLower().Contains(emotion.ToLower()))
            {
                thoughtSprite = thoughtSprites[i];
                Debug.Log($"Found sprite: {thoughtSprites[i].name}");
                break;
            }
        }

        if (thoughtSprite != null)
        {
            Debug.Log("Setting sprite and showing bubble sequence");
            thoughtImage.sprite = thoughtSprite;
            ShowBubbleSequence();
        }
        else
        {
            Debug.LogWarning($"No sprite found for emotion: {emotion}. Available sprites: {string.Join(", ", thoughtSprites.Select(s => s.name))}");
        }
    }

    private void ShowBubbleSequence()
    {
        Debug.Log($"ShowBubbleSequence called. isVisible: {isVisible}");
        if (!isVisible)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateBubbleSequence());
            if (floatingCoroutine != null)
                StopCoroutine(floatingCoroutine);
            floatingCoroutine = StartCoroutine(FloatBubbles());
            isVisible = true;
        }
    }

    public void HideThought()
    {
        if (isVisible)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOutBubbles());
            isVisible = false;
        }
    }

    private IEnumerator AnimateBubbleSequence()
    {
        // Reset all bubbles
        SetBubblesVisible(false);

        // Animate small bubble 1
        smallBubble1.SetActive(true);
        yield return StartCoroutine(FadeCanvasGroup(smallBubble1Group, 0f, 1f, bubbleFadeDuration));
        yield return new WaitForSeconds(bubbleAppearDelay);

        // Animate small bubble 2
        smallBubble2.SetActive(true);
        yield return StartCoroutine(FadeCanvasGroup(smallBubble2Group, 0f, 1f, bubbleFadeDuration));
        yield return new WaitForSeconds(bubbleAppearDelay);

        // Animate main bubble
        mainBubble.SetActive(true);
        yield return StartCoroutine(FadeCanvasGroup(mainBubbleGroup, 0f, 1f, bubbleFadeDuration));
    }

    private IEnumerator FadeOutBubbles()
    {
        // Fade out all bubbles simultaneously
        StartCoroutine(FadeCanvasGroup(mainBubbleGroup, 1f, 0f, bubbleFadeDuration));
        StartCoroutine(FadeCanvasGroup(smallBubble1Group, 1f, 0f, bubbleFadeDuration));
        StartCoroutine(FadeCanvasGroup(smallBubble2Group, 1f, 0f, bubbleFadeDuration));

        yield return new WaitForSeconds(bubbleFadeDuration);
        SetBubblesVisible(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float start, float end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        group.alpha = end;
    }

    private IEnumerator FloatBubbles()
    {
        Debug.Log("Started floating animation");
        float[] timeOffsets = { 0f, 0.33f, 0.66f }; // Different starting points for each bubble
        float startTime = Time.time;
        
        while (true)
        {
            float time = (Time.time - startTime) * floatSpeed;
            
            // Larger movement for main bubble
            Vector3 mainOffset = new Vector3(
                Mathf.Sin(time + timeOffsets[0]) * floatAmount * 20f,
                Mathf.Cos(time * 0.8f + timeOffsets[0]) * floatAmount * 15f,
                0f
            );
            mainBubble.transform.localPosition = mainBubbleBasePos + mainOffset;
            
            // Medium movement for middle bubble
            Vector3 small1Offset = new Vector3(
                Mathf.Sin(time * 1.1f + timeOffsets[1]) * floatAmount * 15f,
                Mathf.Cos(time * 0.9f + timeOffsets[1]) * floatAmount * 12f,
                0f
            );
            smallBubble1.transform.localPosition = smallBubble1BasePos + small1Offset;
            
            // Smaller movement for smallest bubble
            Vector3 small2Offset = new Vector3(
                Mathf.Sin(time * 1.2f + timeOffsets[2]) * floatAmount * 10f,
                Mathf.Cos(time + timeOffsets[2]) * floatAmount * 8f,
                0f
            );
            smallBubble2.transform.localPosition = smallBubble2BasePos + small2Offset;
            
            yield return null;
        }
    }

    private void SetBubblesVisible(bool visible)
    {
        mainBubble.SetActive(visible);
        smallBubble1.SetActive(visible);
        smallBubble2.SetActive(visible);
        
        // Reset positions when hiding bubbles
        if (!visible)
        {
            mainBubble.transform.localPosition = mainBubbleBasePos;
            smallBubble1.transform.localPosition = smallBubble1BasePos;
            smallBubble2.transform.localPosition = smallBubble2BasePos;
        }
    }

    // Convenience methods for showing specific thoughts
    public void ShowHappyThought() => ShowThought("happy");
    public void ShowSadThought() => ShowThought("sad");
    public void ShowAngryThought() => ShowThought("angry");
    public void ShowSurprisedThought() => ShowThought("surprised");
    public void ShowScaredThought() => ShowThought("scared");
} 