using UnityEngine;
using System.Collections;

public class IdleAnimator : MonoBehaviour
{
    [Header("Idle Animation Settings")]
    [SerializeField] private bool enableIdleAnimation = true;
    [Range(0f, 0.5f)] [SerializeField] private float hoverHeight = 0.1f;
    [Range(0f, 0.1f)] [SerializeField] private float breathScale = 0.05f;
    [Range(0f, 5f)] public float hoverSpeed = 1f;
    [Range(0f, 5f)] public float breathSpeed = 1.5f;

    [Header("Talking Animation Settings")]
    [Range(0f, 1f)] [SerializeField] private float talkingHoverHeightMultiplier = 0.5f;
    [Range(0f, 10f)] [SerializeField] private float talkingBaseBreathSpeed = 5f;
    [Range(0f, 5f)] [SerializeField] private float talkingRandomBreathFluctuation = 3f;
    [Range(0f, 1f)] [SerializeField] private float talkingRandomnessFrequency = 0.5f;

    [SerializeField] private bool isSpeaking;
    [SerializeField] private CoquiTTSController speaker;
    
    private Vector3 _initialPosition;
    private Vector3 _initialScale;
    private Coroutine _idleCoroutine;

    private void Start()
    {
        _initialPosition = transform.position;
        _initialScale = transform.localScale;

        if (enableIdleAnimation) Invoke(nameof(StartIdleAnimationDelayed), 0.01f);
    }

    // Called when a value is changed in the Inspector (in editor mode)
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        
        switch (enableIdleAnimation)
        {
            // If idle animation is enabled/disabled during play, start/stop the coroutine
            case true when _idleCoroutine == null:
                
                Invoke(nameof(StartIdleAnimationDelayed), 0.5f);
                break;
            
            case false when _idleCoroutine != null:
                
                CancelInvoke(nameof(StartIdleAnimationDelayed)); // Cancel any pending invokes
                StopCoroutine(_idleCoroutine);
                _idleCoroutine = null;
                
                // Ensure it snaps back to initial position/scale if animation is stopped
                transform.position = _initialPosition;
                transform.localScale = _initialScale;
                break;
        }
    }

    private void StartIdleAnimationDelayed() => _idleCoroutine ??= StartCoroutine(DoIdleAnimation());

    private IEnumerator DoIdleAnimation()
    {
        while (true)
        {
            // --- Hovering (Y-axis movement) ---
            // Uses Time.time for a continuous, framerate-independent animation
            var currentHoverHeight = hoverHeight;
            if (speaker.IsSpeaking() || isSpeaking)
            {
                currentHoverHeight *= talkingHoverHeightMultiplier; // Reduce hover height when talking
            }
            var currentHoverY = _initialPosition.y + Mathf.Sin(Time.time * hoverSpeed * 2 * Mathf.PI) * currentHoverHeight;
            transform.position = new Vector3(_initialPosition.x, currentHoverY, _initialPosition.z);

            // --- Breathing (Scale modulation) ---
            var currentBreathSpeed = breathSpeed;
            if (speaker.IsSpeaking() || isSpeaking)
            {
                // Modulate breath speed for "talking" effect
                // Use Perlin noise for smooth, random-like fluctuations
                var noise = Mathf.PerlinNoise(Time.time * talkingRandomnessFrequency, 0f); // 0-1 range
                currentBreathSpeed = talkingBaseBreathSpeed + (noise * talkingRandomBreathFluctuation);
            }

            var breathScaleMod = 1f + Mathf.Sin(Time.time * currentBreathSpeed * 2 * Mathf.PI) * breathScale;
            transform.localScale = _initialScale * breathScaleMod;

            yield return null;
        }
    }
}
