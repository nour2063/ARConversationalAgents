using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AdaptiveWaveform : MonoBehaviour
{
    [Header("Waveform Shape")]
    [Tooltip("The number of points to draw the circle. More points = smoother circle.")]
    [Range(10, 256)]
    public int segments = 100;
    [Tooltip("The radius of the circular wave.")]
    public float radius = 5f;
    
    [Header("Animation States")]
    [Tooltip("Check this box to activate the 'speaking' animation.")]
    public bool isSpeaking = false;
    [SerializeField] private CoquiTTSController speaker;
    [Tooltip("Check this box to activate the 'listening' animation.")]
    public bool isListening = false;
    [SerializeField] private WhisperSTTController listener;
    [Tooltip("How quickly the wave transitions between states. Higher = faster.")]
    public float transitionSpeed = 5f;

    [Header("Idle State (Not Speaking)")]
    public float idleWaveFrequency = 5f;
    public float rotationSpeedIdle = 1f;
    public float amplitudeIdle = 0.2f;

    [Header("Speaking State (Perlin Noise)")]
    public float rotationSpeedSpeaking = 0.3f;
    public float amplitudeSpeaking = 1.0f;
    public float noiseScale = 10f;
    public float noiseSpeed = 2f;
    
    [Header("Listening State")]
    public float listeningWaveFrequency = 5f; // From your screenshot
    public float rotationSpeedListening = 0.1f;
    public float amplitudeListening = 0.1f;
    
    private LineRenderer lineRenderer;
    private float timeOffset = 0f;
    
    // --- Variables for smoothing ---
    private float currentAmplitude;
    private float currentRotationSpeed;
    private float currentWaveFrequency;
    private float currentNoiseScale;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;

        // Initialize current values to the idle state to prevent a jump at the start
        currentAmplitude = amplitudeIdle;
        currentRotationSpeed = rotationSpeedIdle;
        currentWaveFrequency = idleWaveFrequency;
        currentNoiseScale = 0f;
    }

    void Update()
    {
        if (segments <= 0) return;

        lineRenderer.positionCount = segments;
        lineRenderer.loop = true;
        DrawWaveform();
    }

    void DrawWaveform()
    {
        // --- 1. DETERMINE TARGET VALUES BASED ON STATE ---
        float targetAmplitude, targetRotationSpeed, targetWaveFrequency, targetNoiseScale;

        bool isCurrentlySpeaking = isSpeaking || (speaker != null && speaker.IsSpeaking());
        bool isCurrentlyListening = isListening || (listener != null && listener.IsListeningForCommand());

        if (isCurrentlySpeaking)
        {
            targetAmplitude = amplitudeSpeaking;
            targetRotationSpeed = rotationSpeedSpeaking;
            targetWaveFrequency = 0; // Not used but set for completeness
            targetNoiseScale = noiseScale;
        }
        else if (isCurrentlyListening)
        {
            targetAmplitude = amplitudeListening;
            targetRotationSpeed = rotationSpeedListening;
            targetWaveFrequency = listeningWaveFrequency;
            targetNoiseScale = 0;
        }
        else // Idle State
        {
            targetAmplitude = amplitudeIdle;
            targetRotationSpeed = rotationSpeedIdle;
            targetWaveFrequency = idleWaveFrequency;
            targetNoiseScale = 0;
        }

        // --- 2. SMOOTHLY LERP CURRENT VALUES TOWARDS TARGETS ---
        currentAmplitude = Mathf.Lerp(currentAmplitude, targetAmplitude, Time.deltaTime * transitionSpeed);
        currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, targetRotationSpeed, Time.deltaTime * transitionSpeed);
        currentWaveFrequency = Mathf.Lerp(currentWaveFrequency, targetWaveFrequency, Time.deltaTime * transitionSpeed);
        currentNoiseScale = Mathf.Lerp(currentNoiseScale, targetNoiseScale, Time.deltaTime * transitionSpeed);
        
        // --- 3. USE THE SMOOTHED VALUES TO DRAW THE BLENDED WAVE ---
        timeOffset += Time.deltaTime * currentRotationSpeed;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // A. Calculate the Sine Wave component (for Idle/Listening)
            float sineY = Mathf.Sin((angle * currentWaveFrequency) + timeOffset) * currentAmplitude;

            // B. Calculate the Perlin Noise component (for Speaking)
            float noiseX = Mathf.Cos(angle) * noiseScale + timeOffset * noiseSpeed;
            float noiseY = Mathf.Sin(angle) * noiseScale + timeOffset * noiseSpeed;
            float noiseValue = (Mathf.PerlinNoise(noiseX, noiseY) - 0.5f) * 2f;
            float perlinY = noiseValue * currentAmplitude;

            // C. Determine the blend factor based on how much noise should be present
            // We use Mathf.Max to prevent division by zero if noiseScale is 0.
            float blendFactor = Mathf.Clamp01(currentNoiseScale / Mathf.Max(0.001f, noiseScale));
            
            // D. Blend the two waveforms together
            float y = Mathf.Lerp(sineY, perlinY, blendFactor);

            lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }
}