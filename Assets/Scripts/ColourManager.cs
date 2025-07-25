using UnityEngine;
using System.Collections;

public class ColourManager : MonoBehaviour
{
    public float fadeDuration = 0.25f;

    private Renderer _rend;
    private MaterialPropertyBlock _mbp;

    private static readonly int Color0ID = Shader.PropertyToID("_Color0");
    private static readonly int Color1ID = Shader.PropertyToID("_Color1");
    private static readonly int Color2ID = Shader.PropertyToID("_Color2");

    private void Start()
    {
        _rend = GetComponent<Renderer>();
        _mbp = new MaterialPropertyBlock();
        _rend.GetPropertyBlock(_mbp);
    }

    public void SetColor(int radialColorIndex, Color newColor)
    {
        StartCoroutine(FadeColor(radialColorIndex, newColor, fadeDuration));
    }

    private IEnumerator FadeColor(int radialColorIndex, Color targetColor, float duration)
    {
        _rend.GetPropertyBlock(_mbp);

        int propertyID;
        switch (radialColorIndex)
        {
            case 0: propertyID = Color0ID; break;
            case 1: propertyID = Color1ID; break;
            case 2: propertyID = Color2ID; break;
            default:
                Debug.LogError($"FadeToNewColor: Invalid radial color index provided: {radialColorIndex}. Must be 0, 1, or 2.");
                yield break;
        }

        var startColor = _mbp.GetColor(propertyID);
        var time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            var lerpedColor = Color.Lerp(startColor, targetColor, time / duration);
            _mbp.SetColor(propertyID, lerpedColor);
            _rend.SetPropertyBlock(_mbp);
            yield return null;
        }

        _mbp.SetColor(propertyID, targetColor);
        _rend.SetPropertyBlock(_mbp);
    }
    
}