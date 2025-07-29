using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Required for IEnumerator

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PolygonalSphereGenerator : MonoBehaviour
{
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

    private Vector3 initialScale;
    private Quaternion initialRotation; // This will be the resting rotation


    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        meshFilter.mesh = mesh;

        Rigidbody existingRb = GetComponent<Rigidbody>();
        if (existingRb != null) Destroy(existingRb);

        MeshCollider existingCollider = GetComponent<MeshCollider>();
        if (existingCollider != null) Destroy(existingCollider);
    }

    void OnValidate()
    {
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
                GeneratePolygonalSphereInstant();
            }
        }
    }

    void Start()
    {
        initialScale = transform.localScale;
        initialRotation = transform.rotation;

        GeneratePolygonalSphereInstant();
    }

    void GeneratePolygonalSphereInstant()
    {
        if (mesh == null)
        {
            Debug.LogError("Mesh object is null in GeneratePolygonalSphereInstant. This should not happen if called correctly.");
            return;
        }

        mesh.Clear();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int latSegments = Mathf.Max(2, detailLevel);
        int longSegments = Mathf.Max(3, detailLevel * 2);

        GenerateUVSphere(ref vertices, ref triangles, latSegments, longSegments);

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

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

        // --- Triangle generation ---
        // --- Top cap triangles (CLOCKWISE when looking DOWN from North Pole) ---
        // Connect North Pole (index 0) to the first ring of vertices (indices 1 to longSegments)
        int firstRingStartIndex = 1;
        for (int i = 0; i < longSegments; i++)
        {
            triangles.Add(0); // North Pole
            triangles.Add(firstRingStartIndex + i); // Current vertex on the first ring
            triangles.Add(firstRingStartIndex + ((i + 1) % longSegments)); // Next vertex on the first ring (wraps around)
        }

        // --- Middle section triangles (forming quads from two triangles) ---
        // Ensure consistent winding (e.g., clockwise)
        int ringVertexCount = longSegments + 1; // Number of vertices in each latitude ring (including duplicated end vertex)
        for (int lat = 0; lat < latSegments - 2; lat++) // Loop through latitude strips (excluding top/bottom caps)
        {
            for (int lon = 0; lon < longSegments; lon++) // Loop through longitude segments within a strip
            {
                int currentTopLeft = 1 + lat * ringVertexCount + lon;
                int currentTopRight = currentTopLeft + 1;
                int currentBottomLeft = currentTopLeft + ringVertexCount;
                int currentBottomRight = currentTopLeft + ringVertexCount + 1; 

                // First triangle of the quad (Top-Left, Bottom-Left, Bottom-Right)
                // This creates a clockwise triangle when looking at the outside face.
                triangles.Add(currentTopLeft);
                triangles.Add(currentBottomLeft);
                triangles.Add(currentBottomRight);

                // Second triangle of the quad (Top-Left, Bottom-Right, Top-Right)
                // This completes the quad with another clockwise triangle.
                triangles.Add(currentTopLeft);
                triangles.Add(currentBottomRight);
                triangles.Add(currentTopRight);
            }
        }

        // --- Bottom cap triangles (CLOCKWISE when looking UP from South Pole) ---
        // Connect South Pole (last index) to the last ring of vertices
        int southPoleIndex = vertices.Count - 1;
        int lastRingStartIndex = southPoleIndex - ringVertexCount; // Start index of the ring just above South Pole

        for (int i = 0; i < longSegments; i++)
        {
            triangles.Add(southPoleIndex); // South Pole
            // REVERTED TO PREVIOUS WORKING ORDER FOR BOTTOM CAP
            triangles.Add(lastRingStartIndex + (i + 1)); // Next vertex on the last ring (wraps around)
            triangles.Add(lastRingStartIndex + i); // Current vertex on the last ring
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
    
    public IEnumerator DoChangeShapeAndAnimate()
    {
        // Calculate continuous spin rate for the change animation
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
            float lerpVal = (float)detailLevel / 32f; // Ensure float division
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
                
                float easedProgress = EaseInOutQuint(phaseProgress); // Using EaseInOutQuint for settling

                currentScaleMultiplier = Mathf.Lerp(1f + boingMaxOvershoot, 1f, easedProgress);
            }

            // Apply calculated scale multiplier relative to the OBJECT'S INITIAL SCALE
            transform.localScale = initialScale * currentScaleMultiplier;

            timer += Time.deltaTime;
            yield return null;
        }

        // --- Final Reset: Ensures exact initial state after animation ---
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