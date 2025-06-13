using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip startSound;
    public AudioClip endSound;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayStartSound()
    {
        audioSource.PlayOneShot(startSound);
    }

    public void PlayEndSound()
    {
        audioSource.PlayOneShot(endSound);
    }
}
