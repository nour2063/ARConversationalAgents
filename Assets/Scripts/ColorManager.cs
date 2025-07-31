using UnityEngine;
using System.Collections;
using NUnit.Framework.Constraints;

public class ColorManager : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int RadialColor = Shader.PropertyToID("_Color0");
    
    public float transitionDuration = 1.0f;

    private Renderer _objectRenderer;
    private Coroutine _runningCoroutine;
    
    [SerializeField] private bool isMultiGradient;

    void Awake()
    {
        _objectRenderer = GetComponent<Renderer>();
        if (_objectRenderer == null)
        {
            Debug.LogError("ColorManager requires a Renderer component.", this);
        }
    }

    public void SetColor(Color newColor)
    {
        if (_runningCoroutine != null)
        {
            StopCoroutine(_runningCoroutine);
        }
        _runningCoroutine = StartCoroutine(TransitionToColor(newColor));
    }

    private IEnumerator TransitionToColor(Color targetColor)
    {
        var propertyID = isMultiGradient
            ? RadialColor
            : BaseColor;

        yield return AnimateColorProperty(propertyID, targetColor);
    }
    
    private IEnumerator AnimateColorProperty(int propertyID, Color targetColor)
    {
        float elapsedTime = 0;
        Color startingColor = _objectRenderer.material.GetColor(propertyID);

        while (elapsedTime < transitionDuration)
        {
            // Calculate the interpolation progress and set the new color.
            var progress = elapsedTime / transitionDuration;
            var newFrameColor = Color.Lerp(startingColor, targetColor, progress);
            _objectRenderer.material.SetColor(propertyID, newFrameColor);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame.
        }

        // Ensure the final target color is set precisely upon completion.
        _objectRenderer.material.SetColor(propertyID, targetColor);
    }
}