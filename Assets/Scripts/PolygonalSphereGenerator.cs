using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Required for IEnumerator

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PolygonalSphereGenerator : MonoBehaviour
{
    [SerializeField] private IdleAnimator idleAnimator; 
    
    [Tooltip("Controls the polygonal detail. Higher values = more perfect sphere. 0 or 1 for very low poly.")]
    [Range(0, 32)]
    public int detailLevel = 16;
    public float radius = 1f;

    [Header("Change Animation & Flare Settings")]
    public float changeAnimDuration = 0.8f; // Total duration for the entire change animation
    public float changeRotationAmount = 360f; // Total degrees to spin during change animation
    public ParticleSystem changeEffectPrefab; // Particle system prefab
    public Color minParticleColor = Color.yellow;
    public Color maxParticleColor = Color.red;

    [Header("Boing Scale Animation Settings")]
    public float boingMinScale = 0.5f;              // Target minimum scale during shrink (e.g., 0.5 means 50% of initial)
    public float boingMaxOvershoot = 0.2f;          // How much it grows past normal (e.g., 0.2 means 120% of initial)

    // Timing ratios for the combined scale animation within changeAnimDuration
    [Range(0.01f, 0.4f)] public float shrinkPhaseRatio = 0.2f; // % of total duration for shrinking
    [Range(0.01f, 0.5f)] public float meshChangeOffsetRatio = 0.05f; // % of total duration for hold/mesh change
    [Range(0.1f, 0.5f)] public float overshootRiseRatio = 0.4f; // % of total duration for growth to overshoot peak
    // The remaining duration (1.0 - sum of above) is for the final smooth settle.

    private MeshFilter meshFilter;
    private Mesh mesh;
    private ParticleSystem activeParticleSystem;
    private Coroutine changeAnimationCoroutine;

    private Vector3 initialPosition;
    private Vector3 initialScale;
    private Quaternion initialRotation; // This will be the resting rotation


    void Awake()
    {
        // Primary initialization: This is the ONLY place Mesh and MeshFilter.mesh are created and assigned.
        meshFilter = GetComponent<MeshFilter>();
        if (mesh == null) // Create new Mesh object only if it hasn't been instantiated yet
        {
            mesh = new Mesh();
        }
        meshFilter.mesh = mesh; // Assign the Mesh to the MeshFilter

        // Remove physics components if they somehow exist from previous iterations, to ensure no conflict.
        Rigidbody existingRb = GetComponent<Rigidbody>();
        if (existingRb != null) Destroy(existingRb);

        MeshCollider existingCollider = GetComponent<MeshCollider>();
        if (existingCollider != null) Destroy(existingCollider);
    }

    void OnValidate()
    {
        // IMPORTANT: OnValidate can run before Awake. We cannot safely assign meshFilter.mesh
        // or create new Mesh objects here if they are null, due to Unity's "SendMessage" error.
        // We only proceed if meshFilter and mesh are ALREADY initialized (from Awake, or preserved state).
        if (meshFilter != null && mesh != null)
        {
            if (Application.isPlaying)
            {
                if (changeAnimationCoroutine != null)
                {
                    StopCoroutine(changeAnimationCoroutine);
                }
                changeAnimationCoroutine = StartCoroutine(DoChangeShapeAndAnimate());
            }
            else
            {
                // In Editor (not playing), generate instantly for preview, IF components are ready.
                GeneratePolygonalSphereInstant();
            }
        }
    }

    void Start()
    {
        // Capture initial transform values. These are the "home" values for all animations.
        initialPosition = transform.position;
        initialScale = transform.localScale;
        initialRotation = transform.rotation; // Capture initial rotation

        // Generate the initial sphere mesh when the game starts.
        GeneratePolygonalSphereInstant();
    }

    // This method ONLY updates the MeshFilter's mesh data. It does NOT create Mesh objects
    // or assign them to meshFilter.mesh. It assumes 'mesh' is already valid.
    void GeneratePolygonalSphereInstant()
    {
        // Mesh should be initialized by Awake(). If this is called from OnValidate before Awake,
        // it might still be null, but OnValidate guards against that.
        if (!mesh)
        {
            Debug.LogError("Mesh object is null in GeneratePolygonalSphereInstant. This should not happen if called correctly.");
            return;
        }

        mesh.Clear(); // Clear any existing data in *our* Mesh object

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int latSegments = Mathf.Max(2, detailLevel);
        int longSegments = Mathf.Max(3, detailLevel * 2);

        GenerateUVSphere(ref vertices, ref triangles, latSegments, longSegments);

        // Assign generated data to the mesh
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        // Recalculate properties for proper rendering
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void GenerateUVSphere(ref List<Vector3> vertices, ref List<int> triangles, int latSegments, int longSegments)
    {
        vertices.Add(Vector3.up * radius);
        for (int lat = 1; lat <= latSegments - 1; lat++)
        {
            float theta = (float)lat * Mathf.PI / latSegments;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            for (int lon = 0; lon <= longSegments; lon++)
            {
                float phi = (float)lon * 2 * Mathf.PI / longSegments;
                float x = radius * sinTheta * Mathf.Cos(phi);
                float y = radius * cosTheta;
                float z = radius * sinTheta * Mathf.Sin(phi);
                vertices.Add(new Vector3(x, y, z));
            }
        }
        vertices.Add(Vector3.down * radius);

        for (int i = 0; i < longSegments; i++)
        {
            triangles.Add(0);
            triangles.Add(1 + ((i + 1) % longSegments));
            triangles.Add(1 + i);
        }

        int ringVertexCount = longSegments + 1;
        for (int lat = 0; lat < latSegments - 2; lat++)
        {
            for (int lon = 0; lon < longSegments; lon++)
            {
                int currentTopLeft = 1 + lat * ringVertexCount + lon;
                int currentTopRight = currentTopLeft + 1;
                int currentBottomLeft = currentTopLeft + ringVertexCount;
                int currentBottomRight = currentTopLeft + ringVertexCount + 1; // Corrected calculation for currentBottomRight

                triangles.Add(currentTopLeft);
                triangles.Add(currentBottomLeft);
                triangles.Add(currentTopRight);

                triangles.Add(currentTopRight);
                triangles.Add(currentBottomLeft);
                triangles.Add(currentBottomRight);
            }
        }

        int southPoleIndex = vertices.Count - 1;
        int lastRingStartIndex = southPoleIndex - ringVertexCount;

        for (int i = 0; i < longSegments; i++)
        {
            triangles.Add(southPoleIndex);
            triangles.Add(lastRingStartIndex + (i + 1));
            triangles.Add(lastRingStartIndex + i);
        }
    }

    // Easing function for smooth acceleration/deceleration (EaseInOutQuint)
    float EaseInOutQuint(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t * t * t + 2f);
    }
    
    private IEnumerator DoChangeShapeAndAnimate()
    {
        // Capture initial rotation at the start (animStartRot)
        // No need for animTargetRot for Slerp as we use transform.Rotate

        // Calculate continuous spin rate for the change animation
        // This makes it spin 'changeRotationAmount' degrees over 'changeAnimDuration'
        float changeSpinRateDegreesPerSecond = changeRotationAmount / changeAnimDuration;

        // --- Flare: Play Particle Effect at the start ---
        if (changeEffectPrefab)
        {
            if (activeParticleSystem)
            {
                activeParticleSystem.Stop();
                Destroy(activeParticleSystem.gameObject);
            }
            activeParticleSystem = Instantiate(changeEffectPrefab, transform.position, Quaternion.identity);
            var mainModule = activeParticleSystem.main;
            float lerpVal = detailLevel / 32f;
            mainModule.startColor = Color.Lerp(minParticleColor, maxParticleColor, lerpVal);
            activeParticleSystem.Play();
        }

        float timer = 0f;
        bool meshChanged = false; // Flag to ensure mesh changes only once

        // Define phase start/end times relative to total duration
        float shrinkEndTime = shrinkPhaseRatio;
        float meshChangeTransitionEndTime = shrinkEndTime + meshChangeOffsetRatio;
        float boingExpandEndTime = meshChangeTransitionEndTime + overshootRiseRatio;


        while (timer < changeAnimDuration) // Use changeAnimDuration directly
        {
            float animProgress = timer / changeAnimDuration; // Normalized total progress (0 to 1)
            // No specific easedAnimProgress needed for Slerp anymore, as we use transform.Rotate.


            // --- ROTATION: Use transform.Rotate for continuous spin (as per working version) ---
            transform.Rotate(Vector3.up, changeSpinRateDegreesPerSecond * Time.deltaTime, Space.Self);


            // Scale Animation: Unified curve for shrink, overshoot, and settle
            float currentScaleMultiplier = 1f; // Declare and initialize here

            if (animProgress < shrinkEndTime) // Phase 1: Shrink (from 1 to boingMinScale)
            {
                float phaseProgress = animProgress / shrinkEndTime;
                float easedProgress = EaseInOutQuint(phaseProgress);
                currentScaleMultiplier = Mathf.Lerp(1f, boingMinScale, easedProgress);
            }
            else if (animProgress < meshChangeTransitionEndTime) // Phase 2: Hold shrink and change mesh
            {
                if (!meshChanged)
                {
                    GeneratePolygonalSphereInstant(); // Change the mesh NOW!
                    meshChanged = true;
                }
                currentScaleMultiplier = boingMinScale; // Hold at min scale
            }
            else if (animProgress < boingExpandEndTime) // Phase 3: Grow to Overshoot (from boingMinScale to 1 + boingMaxOvershoot)
            {
                float phaseProgress = (animProgress - meshChangeTransitionEndTime) / (boingExpandEndTime - meshChangeTransitionEndTime);
                float easedProgress = EaseInOutQuint(phaseProgress); // Use EaseInOutQuint for growth to peak

                currentScaleMultiplier = Mathf.Lerp(boingMinScale, 1f + boingMaxOvershoot, easedProgress);
            }
            else // Phase 4: Settle Back to 1.0 (from 1 + boingMaxOvershoot to 1)
            {
                float phaseProgress = (animProgress - boingExpandEndTime) / (1f - boingExpandEndTime);
                // Clamp progress to ensure it hits 1.0 exactly at the end
                phaseProgress = Mathf.Clamp01(phaseProgress);
                
                float easedProgress = EaseInOutQuint(phaseProgress); // Using EaseInOutQuint for settling (CORRECTED USAGE)

                currentScaleMultiplier = Mathf.Lerp(1f + boingMaxOvershoot, 1f, easedProgress);
            }

            // Apply calculated scale multiplier relative to the OBJECT'S INITIAL SCALE
            transform.localScale = initialScale * currentScaleMultiplier;

            timer += Time.deltaTime;
            yield return null;
        }

        // --- Final Reset: Ensures exact initial state after animation ---
        // For transform.rotation, it has been continuously rotating. We snap it back to initialRotation.
        transform.position = initialPosition;
        transform.rotation = initialRotation; // Snap back to the true initialRotation for consistency
        transform.localScale = initialScale; 

        // --- Clean up Particle Effect ---
        if (activeParticleSystem != null)
        {
            activeParticleSystem.Stop();
            Destroy(activeParticleSystem.gameObject, activeParticleSystem.main.duration + 0.1f);
            activeParticleSystem = null;
        }

        // Mark this coroutine as finished
        changeAnimationCoroutine = null;
    }
}