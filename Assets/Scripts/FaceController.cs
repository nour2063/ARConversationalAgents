using UnityEngine;
using System.Collections;

public class FaceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshFilter faceMeshFilter;
    [SerializeField] private MeshRenderer faceMeshRenderer;
    [SerializeField] private Material defaultFaceMaterial;
    [SerializeField] private FaceAnimationController faceAnimationController;

    [Header("Face Materials")]
    [SerializeField] private Material happyFaceMaterial;
    [SerializeField] private Material sadFaceMaterial;
    [SerializeField] private Material angryFaceMaterial;
    [SerializeField] private Material scaredFaceMaterial;
    [SerializeField] private Material surprisedFaceMaterial;
    [SerializeField] private Material neutralFaceMaterial;
    [SerializeField] private Material sleepyFaceMaterial;

    [Header("Face Display Settings")]
    [SerializeField] private float faceOffset = 0.01f; // Distance in front of Qoobo mesh
    [SerializeField] private float faceDiameter = 0.214f; // Diameter of the Qoobo from top view
    [SerializeField] private float faceHeight = 0.15f; // Vertical size of the face
    [SerializeField] private float curvatureAngle = 60f; // How much the face curves around
    [SerializeField] private int curveResolution = 20; // Number of segments for the curve
    [SerializeField] private float scaleFactor = 2f; // Overall scale multiplier
    [SerializeField] private float fadeInDuration = 2f;

    private Material currentMaterial;
    private float currentAlpha = 0f;
    private Coroutine fadeCoroutine;
    private bool isUsingAnimatedFace = false;

    private void Start()
    {
        if (faceMeshFilter == null)
        {
            Debug.LogError("Face Mesh Filter not assigned!");
            return;
        }

        // Rotate the face object 180 degrees around Y axis
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        CreateCurvedFaceMesh();
        SetupMaterialProperties();
        
        // Ensure face starts completely invisible
        currentAlpha = 0f;
        faceMeshRenderer.enabled = false;  // Disable renderer completely
        SetFaceExpression("neutral"); // Start with neutral expression but invisible
        
        Debug.Log($"Face mesh created with settings: Scale={scaleFactor}, Height={faceHeight}, Diameter={faceDiameter}, Curvature={curvatureAngle}");
    }

    private void SetupMaterialProperties()
    {
        if (faceMeshRenderer == null)
        {
            Debug.LogError("Face Mesh Renderer not assigned!");
            return;
        }

        // Set up material array with all face materials
        Material[] materials = new Material[] 
        { 
            defaultFaceMaterial, 
            happyFaceMaterial, 
            sadFaceMaterial, 
            angryFaceMaterial, 
            scaredFaceMaterial, 
            surprisedFaceMaterial, 
            neutralFaceMaterial, 
            sleepyFaceMaterial
        };

        // Configure each material
        foreach (Material mat in materials)
        {
            if (mat != null)
            {
                // Create a new material instance to avoid modifying the original
                Material instanceMat = new Material(mat);
                
                // Force unlit shader and configure it
                instanceMat.shader = Shader.Find("Universal Render Pipeline/Unlit");
                
                // Configure transparency
                instanceMat.SetInt("_Surface", 1); // 1 = Transparent
                instanceMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                instanceMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                instanceMat.SetInt("_ZWrite", 0);
                instanceMat.SetFloat("_Blend", 0);
                instanceMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                instanceMat.EnableKeyword("_ALPHABLEND_ON");
                
                // Set initial color with zero alpha
                Color color = instanceMat.color;
                color.a = 0f;
                instanceMat.color = color;
                
                // Ensure the material is completely unaffected by lighting
                instanceMat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                instanceMat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                instanceMat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                instanceMat.SetFloat("_EnvironmentReflections", 0f);
                instanceMat.SetFloat("_SpecularHighlights", 0f);
                
                // Ensure back-face culling is enabled
                instanceMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                
                // Set render queue for transparency
                instanceMat.renderQueue = 3000;
            }
        }

        // Disable shadows and configure renderer
        faceMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        faceMeshRenderer.receiveShadows = false;
        faceMeshRenderer.enabled = false;  // Start with renderer disabled
    }

    private void CreateCurvedFaceMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "FaceMesh";

        // Calculate dimensions
        float radius = (faceDiameter * 0.5f) * scaleFactor;
        float halfHeight = (faceHeight * 0.5f) * scaleFactor;
        float angleStep = curvatureAngle / (curveResolution - 1);
        float startAngle = -curvatureAngle * 0.5f;

        // Create vertices
        Vector3[] vertices = new Vector3[curveResolution * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(curveResolution - 1) * 6];

        Debug.Log($"Creating mesh with radius={radius}, halfHeight={halfHeight}");

        for (int i = 0; i < curveResolution; i++)
        {
            float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius - radius; // Offset to place mesh in front

            // Top vertex
            vertices[i * 2] = new Vector3(x, halfHeight, z + faceOffset);
            uvs[i * 2] = new Vector2((float)i / (curveResolution - 1), 1);

            // Bottom vertex
            vertices[i * 2 + 1] = new Vector3(x, -halfHeight, z + faceOffset);
            uvs[i * 2 + 1] = new Vector2((float)i / (curveResolution - 1), 0);

            // Create triangles with reversed winding order
            if (i < curveResolution - 1)
            {
                int baseIndex = i * 6;
                int vertIndex = i * 2;

                // First triangle (reversed winding)
                triangles[baseIndex] = vertIndex;
                triangles[baseIndex + 1] = vertIndex + 1;
                triangles[baseIndex + 2] = vertIndex + 2;

                // Second triangle (reversed winding)
                triangles[baseIndex + 3] = vertIndex + 2;
                triangles[baseIndex + 4] = vertIndex + 1;
                triangles[baseIndex + 5] = vertIndex + 3;
            }
        }

        // Assign mesh data
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        faceMeshFilter.mesh = mesh;
        
        // Verify mesh creation
        Debug.Log($"Mesh created with {vertices.Length} vertices, bounds: {mesh.bounds}");
    }

    public void SetFaceExpression(string expression)
    {
        Material targetMaterial = null;
        bool shouldAnimate = false;

        switch (expression.ToLower())
        {
            case "happy":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = happyFaceMaterial;
                }
                break;
            case "sad":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = sadFaceMaterial;
                }
                break;
            case "angry":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = angryFaceMaterial;
                }
                break;
            case "scared":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = scaredFaceMaterial;
                }
                break;
            case "surprised":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = surprisedFaceMaterial;
                }
                break;
            case "sleepy":
                targetMaterial = sleepyFaceMaterial;
                break;
            case "neutral":
                if (faceAnimationController != null)
                {
                    targetMaterial = faceAnimationController.GetAnimatedMaterial();
                    shouldAnimate = true;
                }
                else
                {
                    targetMaterial = neutralFaceMaterial;
                }
                break;
            default:
                targetMaterial = defaultFaceMaterial;
                Debug.LogWarning($"Unknown expression: {expression}, using default face");
                break;
        }

        if (targetMaterial != null && faceMeshRenderer != null)
        {
            // Stop any current animation if we're switching away from animated face
            if (isUsingAnimatedFace && !shouldAnimate && faceAnimationController != null)
            {
                faceAnimationController.StopAnimation();
            }

            currentMaterial = targetMaterial;
            faceMeshRenderer.material = currentMaterial;
            
            // Start animation if using animated face
            if (shouldAnimate && faceAnimationController != null)
            {
                faceAnimationController.StartAnimation(expression.ToLower());
                isUsingAnimatedFace = true;
            }
            else
            {
                isUsingAnimatedFace = false;
            }

            // Maintain current alpha when changing expression
            SetFaceVisibility(currentAlpha);
            Debug.Log($"Set face material to {expression}");
        }
        else
        {
            Debug.LogError($"Failed to set face material. Material: {(targetMaterial == null ? "null" : "valid")}, Renderer: {(faceMeshRenderer == null ? "null" : "valid")}");
        }
    }

    public void StartFadeIn()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeInCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsedTime = 0f;
        float startAlpha = currentAlpha;

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, 1f, elapsedTime / fadeInDuration);
            SetFaceVisibility(newAlpha);
            yield return null;
        }

        SetFaceVisibility(1f);
        fadeCoroutine = null;
    }

    public void SetFaceVisibility(float alpha)
    {
        currentAlpha = alpha;
        
        // Enable/disable renderer based on alpha
        if (alpha <= 0f)
        {
            faceMeshRenderer.enabled = false;
        }
        else if (!faceMeshRenderer.enabled)
        {
            faceMeshRenderer.enabled = true;
        }

        if (currentMaterial != null)
        {
            if (isUsingAnimatedFace && faceAnimationController != null)
            {
                faceAnimationController.SetAlpha(alpha);
            }
            else
            {
                Color color = currentMaterial.color;
                color.a = alpha;
                currentMaterial.color = color;
            }
        }
    }

    [ContextMenu("Set Face to Happy")]
    private void SetFaceToHappy(){
        SetFaceExpression("happy");
    }

    // Public method to update the face offset at runtime if needed
    public void UpdateFaceOffset(float newOffset)
    {
        faceOffset = newOffset;
        CreateCurvedFaceMesh();
        Debug.Log($"Updated face offset to {newOffset}");
    }

    // Public method to update the curvature at runtime if needed
    public void UpdateCurvature(float newAngle)
    {
        curvatureAngle = newAngle;
        CreateCurvedFaceMesh();
        Debug.Log($"Updated curvature angle to {newAngle}");
    }

    // Method to update scale at runtime
    public void UpdateScale(float newScale)
    {
        scaleFactor = newScale;
        CreateCurvedFaceMesh();
        Debug.Log($"Updated scale factor to {newScale}");
    }

    // Editor-only validation
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            CreateCurvedFaceMesh();
            Debug.Log("Mesh updated due to parameter change in inspector");
        }
    }
} 