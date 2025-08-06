using UnityEngine;

public class AudioController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;

    private void Start()
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

    public void PlayStartSound() => audioSource.PlayOneShot(startSound);
    public void PlayEndSound() => audioSource.PlayOneShot(endSound);
}
