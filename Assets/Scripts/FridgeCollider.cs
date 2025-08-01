using UnityEngine;

public class FridgeCollider : MonoBehaviour
{
    [SerializeField] private OllamaManager llm;
    [SerializeField] private CoquiTTSController ttsController;
    
    private void OnTriggerEnter(Collider other)
    {
        llm.CaptureImage();
        ttsController.HandleCollision();
    }
    
    private void OnTriggerExit(Collider other)
    {
        llm.CaptureImage(1);
    }
}
