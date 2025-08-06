using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AdaptiveWaveform : MonoBehaviour
{
    [Header("Waveform Shape")]
    [Range(10, 256)] [SerializeField] private int segments = 100;
    
    [Tooltip("The radius of the circular wave.")]
    [SerializeField] private float radius = 5f;
    
    [Header("Animation States")]
    [SerializeField] private bool isSpeaking;
    [SerializeField] private CoquiTTSController speaker;
    [SerializeField] private bool isListening;
    [SerializeField] private WhisperSTTController listener;
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Idle State (Not Speaking)")]
    [Range(0f, 10f)] [SerializeField] private float idleWaveFrequency = 5f;
    [Range(-10f, 10f)] [SerializeField] private float rotationSpeedIdle = 1f;
    [Range(0f, 1f)] [SerializeField] private float amplitudeIdle = 0.2f;

    [Header("Speaking State (Perlin Noise)")]
    [Range(-1f, 1f)] public float rotationSpeedSpeaking = 0.3f;
    [Range(0f, 5f)] public float amplitudeSpeaking = 1.0f;
    [Range(0f, 20f)] public float noiseScale = 10f;
    [Range(0f, 10f)] public float noiseSpeed = 2f;
    
    [Header("Listening State")]
    [Range(0f, 10f)] [SerializeField] private float listeningWaveFrequency = 5f;
    [Range(-1f, 1f)] [SerializeField] private float rotationSpeedListening = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float amplitudeListening = 0.1f;
    
    private LineRenderer _lineRenderer;
    private float _timeOffset;
    
    // --- Variables for smoothing ---
    private float _currentAmplitude;
    private float _currentRotationSpeed;
    private float _currentWaveFrequency;
    private float _currentNoiseScale;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;

        // Initialize current values to the idle state to prevent a jump at the start
        _currentAmplitude = amplitudeIdle;
        _currentRotationSpeed = rotationSpeedIdle;
        _currentWaveFrequency = idleWaveFrequency;
        _currentNoiseScale = 0f;
    }

    private void Update()
    {
        _lineRenderer.positionCount = segments;
        _lineRenderer.loop = true;
        DrawWaveform();
    }

    private void DrawWaveform()
    {
        // 1. DETERMINE TARGET VALUES BASED ON STATE
        float targetAmplitude, targetRotationSpeed, targetWaveFrequency, targetNoiseScale;

        var isCurrentlySpeaking = isSpeaking || (speaker != null && speaker.IsSpeaking());
        var isCurrentlyListening = isListening || (listener != null && listener.IsListeningForCommand());

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

        // 2. SMOOTHLY LERP CURRENT VALUES TOWARDS TARGETS
        _currentAmplitude = Mathf.Lerp(_currentAmplitude, targetAmplitude, Time.deltaTime * transitionSpeed);
        _currentRotationSpeed = Mathf.Lerp(_currentRotationSpeed, targetRotationSpeed, Time.deltaTime * transitionSpeed);
        _currentWaveFrequency = Mathf.Lerp(_currentWaveFrequency, targetWaveFrequency, Time.deltaTime * transitionSpeed);
        _currentNoiseScale = Mathf.Lerp(_currentNoiseScale, targetNoiseScale, Time.deltaTime * transitionSpeed);
        
        // 3. USE THE SMOOTHED VALUES TO DRAW THE BLENDED WAVE
        _timeOffset += Time.deltaTime * _currentRotationSpeed;

        for (var i = 0; i < segments; i++)
        {
            var angle = (float)i / segments * 2f * Mathf.PI;
            var x = Mathf.Cos(angle) * radius;
            var z = Mathf.Sin(angle) * radius;

            // A. Calculate the Sine Wave component (for Idle/Listening)
            var sineY = Mathf.Sin((angle * _currentWaveFrequency) + _timeOffset) * _currentAmplitude;

            // B. Calculate the Perlin Noise component (for Speaking)
            var noiseX = Mathf.Cos(angle) * noiseScale + _timeOffset * noiseSpeed;
            var noiseY = Mathf.Sin(angle) * noiseScale + _timeOffset * noiseSpeed;
            var noiseValue = (Mathf.PerlinNoise(noiseX, noiseY) - 0.5f) * 2f;
            var perlinY = noiseValue * _currentAmplitude;

            // C. Determine the blend factor based on how much noise should be present
            // We use Mathf.Max to prevent division by zero if noiseScale is 0.
            var blendFactor = Mathf.Clamp01(_currentNoiseScale / Mathf.Max(0.001f, noiseScale));
            
            // D. Blend the two waveforms together
            var y = Mathf.Lerp(sineY, perlinY, blendFactor);

            _lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }
}