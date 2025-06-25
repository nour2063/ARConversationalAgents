using System;
using TMPro;
using UnityEngine;

public class PlacingFridge : MonoBehaviour
{
    public Transform hand;
    public GameObject fridge;
    private bool _placed;
    
    void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
        if (_placed) return;
        transform.position = hand.position;
        if (!OVRInput.GetDown(OVRInput.RawButton.X)) return; 
        _placed = true;
        fridge.SetActive(true);
    }
}
