using UnityEngine;

public class AudioController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (audioSource == null)
        {
            Debug.LogError("Audio source not set in AudioController");
        }

        if (startSound == null)
        {
            Debug.LogError("Start sound not set in AudioController");
        }

        if (endSound == null)
        {
            Debug.LogError("End sound not set in AudioController");
        }
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
