using UnityEngine;
using System.Collections;
using Meta.WitAi.TTS.Utilities; // Required for IEnumerator

public class IdleAnimator : MonoBehaviour
{
    [Header("Idle Animation Settings")]
    [Tooltip("Enables/disables the continuous idle animation.")]
    public bool enableIdleAnimation = true;
    [Tooltip("How much the object hovers up and down from its initial Y position.")]
    public float hoverHeight = 0.1f;
    [Tooltip("How fast the object hovers up and down (cycles per second).")]
    public float hoverSpeed = 1f;
    [Tooltip("How much the object scales up and down from its initial size (e.g., 0.05 means 5% change).")]
    public float breathScale = 0.05f;
    [Tooltip("How fast the object 'breathes' (cycles per second).")]
    public float breathSpeed = 1.5f;
    [Tooltip("Degrees per second for the continuous idle spin around the Y-axis.")]
    public float idleSpinSpeed = 10f; // Adjusted from 1f to 10f as per request

    [Header("Talking Animation Settings")]
    [Tooltip("A multiplier applied to hoverHeight when 'Is Talking' is enabled. Use a value < 1 to reduce hover height.")]
    [Range(0f, 1f)] // Restrict to 0-1 range for reduction
    public float talkingHoverHeightMultiplier = 0.5f; // Default to half hover height when talking
    [Tooltip("The base speed for breathing when talking.")]
    public float talkingBaseBreathSpeed = 5f;
    [Tooltip("The maximum random fluctuation added to talkingBaseBreathSpeed.")]
    public float talkingRandomBreathFluctuation = 3f;
    [Tooltip("How quickly the random breath speed changes when talking.")]
    public float talkingRandomnessFrequency = 0.5f; // How often the random speed is re-evaluated

    [SerializeField] private bool isSpeaking = false;
    [SerializeField] private LocalNetworkTTS speaker;
    
    private Vector3 initialPosition;
    private Vector3 initialScale;
    private Coroutine idleCoroutine;

    void Start()
    {
        // Capture the parent GameObject's initial position and scale
        // All idle animations will be relative to these values.
        initialPosition = transform.position;
        initialScale = transform.localScale;

        if (enableIdleAnimation)
        {
            // Delay the start of the idle animation
            Invoke(nameof(StartIdleAnimationDelayed), 0.01f);
        }
    }

    // Called when a value is changed in the Inspector (in editor mode)
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            // If idle animation is enabled/disabled during play, start/stop the coroutine
            if (enableIdleAnimation && idleCoroutine == null)
            {
                Invoke(nameof(StartIdleAnimationDelayed), 0.5f);
            }
            else if (!enableIdleAnimation && idleCoroutine != null)
            {
                CancelInvoke(nameof(StartIdleAnimationDelayed)); // Cancel any pending invokes
                StopCoroutine(idleCoroutine);
                idleCoroutine = null;
                // Ensure it snaps back to initial position/scale if animation is stopped
                transform.position = initialPosition;
                transform.localScale = initialScale;
            }
        }
    }

    // New method to start the idle animation after a delay
    private void StartIdleAnimationDelayed()
    {
        idleCoroutine ??= StartCoroutine(DoIdleAnimation());
    }

    private IEnumerator DoIdleAnimation()
    {
        while (true) // Loop indefinitely for continuous idle
        {
            // --- Hovering (Y-axis movement) ---
            // Uses Time.time for a continuous, framerate-independent animation
            float currentHoverHeight = hoverHeight;
            if (speaker.IsSpeaking() || isSpeaking)
            {
                currentHoverHeight *= talkingHoverHeightMultiplier; // Reduce hover height when talking
            }
            float currentHoverY = initialPosition.y + Mathf.Sin(Time.time * hoverSpeed * 2 * Mathf.PI) * currentHoverHeight;
            transform.position = new Vector3(initialPosition.x, currentHoverY, initialPosition.z);

            // --- Breathing (Scale modulation) ---
            float currentBreathSpeed = breathSpeed;
            if (speaker.IsSpeaking() || isSpeaking)
            {
                // Modulate breath speed for "talking" effect
                // Use Perlin noise for smooth, random-like fluctuations
                float noise = Mathf.PerlinNoise(Time.time * talkingRandomnessFrequency, 0f); // 0-1 range
                currentBreathSpeed = talkingBaseBreathSpeed + (noise * talkingRandomBreathFluctuation);
            }

            float breathScaleMod = 1f + Mathf.Sin(Time.time * currentBreathSpeed * 2 * Mathf.PI) * breathScale;
            transform.localScale = initialScale * breathScaleMod;

            // --- NEW: Idly Spinning ---
            // Rotate around the Y-axis at a constant speed
            transform.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.Self);

            yield return null; // Wait for next frame
        }
    }
}
