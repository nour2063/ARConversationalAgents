using UnityEngine;

public class PlacingFridge : MonoBehaviour
{
    [SerializeField] private Transform hand;
    [SerializeField] private GameObject fridge;
    
    private bool _placed;

    private void Update()
    {
        if (_placed) return;
        
        transform.position = hand.position;
        if (!OVRInput.GetDown(OVRInput.RawButton.X)) return; 
        
        _placed = true;
        fridge.SetActive(true);
    }
}
