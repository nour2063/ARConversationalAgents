using UnityEngine;
using System.Collections;

public class ColorManager : MonoBehaviour
{
    public float transitionDuration = 1.0f;

    private Renderer _objectRenderer;
    private Coroutine _runningCoroutine;

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
        float elapsedTime = 0;
        var startingColor = _objectRenderer.material.GetColor("_BaseColor");

        while (elapsedTime < transitionDuration)
        {
            var progress = elapsedTime / transitionDuration;
            var newFrameColor = Color.Lerp(startingColor, targetColor, progress);
            _objectRenderer.material.SetColor("_BaseColor", newFrameColor);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        _objectRenderer.material.SetColor("_BaseColor", targetColor);
    }
}