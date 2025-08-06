using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PolygonalSphereGenerator : MonoBehaviour
{
    [Range(0, 32)] public int detailLevel = 16;
    [Range(0f, 5f)] [SerializeField] private float radius = 1f;

    [Header("Change Animation & Flare Settings")]
    [Range(0f, 2.5f)] [SerializeField] private float changeAnimDuration = 0.8f; 
    [Range(0f, 360f)] [SerializeField] private float changeRotationAmount = 360f;
    
    [SerializeField] private ParticleSystem changeEffectPrefab; // Todo blob animation change particles
    [SerializeField] private Color minParticleColor = Color.yellow;
    [SerializeField] private Color maxParticleColor = Color.red;

    [Header("Boing Scale Animation Settings")]
    [Range(0f, 1f)] [SerializeField] private float boingMinScale = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float boingMaxOvershoot = 0.2f;
    [Range(0.01f, 0.5f)] [SerializeField] private float shrinkPhaseRatio = 0.2f;
    [Range(0.01f, 0.5f)] [SerializeField] private float meshChangeOffsetRatio = 0.05f;
    [Range(0.1f, 0.5f)] [SerializeField] private float overshootRiseRatio = 0.4f; 

    private MeshFilter _meshFilter;
    private Mesh _mesh;
    private ParticleSystem _activeParticleSystem;
    private Coroutine _changeAnimationCoroutine;

    private Vector3 initialScale;
    private Quaternion initialRotation; // This will be the resting rotation
    
    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        if (_mesh == null)
        {
            _mesh = new Mesh();
        }
        _meshFilter.mesh = _mesh;

        var existingRb = GetComponent<Rigidbody>();
        if (existingRb != null) Destroy(existingRb);

        var existingCollider = GetComponent<MeshCollider>();
        if (existingCollider != null) Destroy(existingCollider);
    }

    private void OnValidate()
    {
        if (_meshFilter == null || _mesh == null) return;
        if (Application.isPlaying)
        {
            if (_changeAnimationCoroutine != null)
            {
                StopCoroutine(_changeAnimationCoroutine);
            }
            _changeAnimationCoroutine = StartCoroutine(DoChangeShapeAndAnimate());
        }
        else GeneratePolygonalSphereInstant();
    }

    private void Start()
    {
        initialScale = transform.localScale;
        initialRotation = transform.rotation;

        GeneratePolygonalSphereInstant();
    }

    private void GeneratePolygonalSphereInstant()
    {
        if (!_mesh)
        {
            Debug.LogError("Mesh object is null in GeneratePolygonalSphereInstant. This should not happen if called correctly.");
            return;
        }

        _mesh.Clear();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        var latSegments = Mathf.Max(2, detailLevel);
        var longSegments = Mathf.Max(3, detailLevel * 2);

        GenerateUVSphere(ref vertices, ref triangles, latSegments, longSegments);

        _mesh.vertices = vertices.ToArray();
        _mesh.triangles = triangles.ToArray();

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }


    private void GenerateUVSphere(ref List<Vector3> vertices, ref List<int> triangles, int latSegments, int longSegments)
    {
        vertices.Add(Vector3.up * radius);
        for (var lat = 1; lat <= latSegments - 1; lat++)
        {
            var theta = (float)lat * Mathf.PI / latSegments;
            var sinTheta = Mathf.Sin(theta);
            var cosTheta = Mathf.Cos(theta);
            for (var lon = 0; lon <= longSegments; lon++)
            {
                var phi = (float)lon * 2 * Mathf.PI / longSegments;
                var x = radius * sinTheta * Mathf.Cos(phi);
                var y = radius * cosTheta;
                var z = radius * sinTheta * Mathf.Sin(phi);
                vertices.Add(new Vector3(x, y, z));
            }
        }
        vertices.Add(Vector3.down * radius);

        // --- Triangle generation ---
        // --- Top cap triangles (CLOCKWISE when looking DOWN from North Pole) ---
        // Connect North Pole (index 0) to the first ring of vertices (indices 1 to longSegments)
        const int firstRingStartIndex = 1;
        for (var i = 0; i < longSegments; i++)
        {
            triangles.Add(0); // North Pole
            triangles.Add(firstRingStartIndex + i); // Current vertex on the first ring
            triangles.Add(firstRingStartIndex + ((i + 1) % longSegments)); // Next vertex on the first ring (wraps around)
        }

        // --- Middle section triangles (forming quads from two triangles) ---
        // Ensure consistent winding (e.g., clockwise)
        var ringVertexCount = longSegments + 1; // Number of vertices in each latitude ring (including duplicated end vertex)
        for (var lat = 0; lat < latSegments - 2; lat++) // Loop through latitude strips (excluding top/bottom caps)
        {
            for (var lon = 0; lon < longSegments; lon++) // Loop through longitude segments within a strip
            {
                var currentTopLeft = 1 + lat * ringVertexCount + lon;
                var currentTopRight = currentTopLeft + 1;
                var currentBottomLeft = currentTopLeft + ringVertexCount;
                var currentBottomRight = currentTopLeft + ringVertexCount + 1; 

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
        var southPoleIndex = vertices.Count - 1;
        var lastRingStartIndex = southPoleIndex - ringVertexCount; // Start index of the ring just above South Pole

        for (var i = 0; i < longSegments; i++)
        {
            triangles.Add(southPoleIndex); // South Pole
            triangles.Add(lastRingStartIndex + (i + 1)); // Next vertex on the last ring (wraps around)
            triangles.Add(lastRingStartIndex + i); // Current vertex on the last ring
        }
    }

    // Easing function for smooth acceleration/deceleration (EaseInOutQuint)
    private static float EaseInOutQuint(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t * t * t + 2f);
    }
    
    public IEnumerator DoChangeShapeAndAnimate()
    {
        // Calculate continuous spin rate for the change animation
        var changeSpinRateDegreesPerSecond = changeRotationAmount / changeAnimDuration;


        // --- Flare: Play Particle Effect at the start ---
        if (changeEffectPrefab)
        {
            if (_activeParticleSystem)
            {
                _activeParticleSystem.Stop();
                Destroy(_activeParticleSystem.gameObject);
            }
            _activeParticleSystem = Instantiate(changeEffectPrefab, transform.position, Quaternion.identity);
            var mainModule = _activeParticleSystem.main;
            var lerpVal = (float)detailLevel / 32f; // Ensure float division
            mainModule.startColor = Color.Lerp(minParticleColor, maxParticleColor, lerpVal);
            _activeParticleSystem.Play();
        }

        var timer = 0f;
        var meshChanged = false; // Flag to ensure mesh changes only once

        // Define phase start/end times relative to total duration
        var shrinkEndTime = shrinkPhaseRatio;
        var meshChangeTransitionEndTime = shrinkEndTime + meshChangeOffsetRatio;
        var boingExpandEndTime = meshChangeTransitionEndTime + overshootRiseRatio;


        while (timer < changeAnimDuration) // Use changeAnimDuration directly
        {
            var animProgress = timer / changeAnimDuration; // Normalized total progress (0 to 1)


            // --- ROTATION: Use transform.Rotate for continuous spin ---
            transform.Rotate(Vector3.up, changeSpinRateDegreesPerSecond * Time.deltaTime, Space.Self);


            // Scale Animation: Unified curve for shrink, overshoot, and settle
            float currentScaleMultiplier;
            
            if (animProgress < shrinkEndTime) 
            {
                // 1. Shrink (from 1 to boingMinScale)
                var phaseProgress = animProgress / shrinkEndTime;
                var easedProgress = EaseInOutQuint(phaseProgress);
                currentScaleMultiplier = Mathf.Lerp(1f, boingMinScale, easedProgress);
            }
            else if (animProgress < meshChangeTransitionEndTime)
            {
                // 2. Hold shrink and change mesh
                if (!meshChanged)
                {
                    GeneratePolygonalSphereInstant();
                    meshChanged = true;
                }
                currentScaleMultiplier = boingMinScale; // Hold at min scale
            }
            else if (animProgress < boingExpandEndTime) 
            {
                // 3. Grow to Overshoot (from boingMinScale to 1 + boingMaxOvershoot)
                var phaseProgress = (animProgress - meshChangeTransitionEndTime) / (boingExpandEndTime - meshChangeTransitionEndTime);
                var easedProgress = EaseInOutQuint(phaseProgress);

                currentScaleMultiplier = Mathf.Lerp(boingMinScale, 1f + boingMaxOvershoot, easedProgress);
            }
            else
            {
                // 4. Settle Back to 1.0 (from 1 + boingMaxOvershoot to 1)
                var phaseProgress = (animProgress - boingExpandEndTime) / (1f - boingExpandEndTime);
                phaseProgress = Mathf.Clamp01(phaseProgress);
                
                var easedProgress = EaseInOutQuint(phaseProgress);

                currentScaleMultiplier = Mathf.Lerp(1f + boingMaxOvershoot, 1f, easedProgress);
            }

            // Apply calculated scale multiplier relative to the object's initial scale
            transform.localScale = initialScale * currentScaleMultiplier;

            timer += Time.deltaTime;
            yield return null;
        }

        // --- Final Reset: Ensures exact initial state after animation ---
        transform.rotation = initialRotation; // Snap back to the true initialRotation for consistency
        transform.localScale = initialScale; 

        // --- Clean up Particle Effect ---
        if (_activeParticleSystem)
        {
            _activeParticleSystem.Stop();
            Destroy(_activeParticleSystem.gameObject, _activeParticleSystem.main.duration + 0.1f);
            _activeParticleSystem = null;
        }

        // Mark this coroutine as finished
        _changeAnimationCoroutine = null;
    }
}