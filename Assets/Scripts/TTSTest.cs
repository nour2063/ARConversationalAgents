using System.Threading.Tasks;
using Meta.WitAi.TTS.Utilities;
using UnityEngine;

public class TTSTest : MonoBehaviour
{
    public TTSSpeaker speaker;
    [SerializeField] private string initialSpeech = "Hey there!";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        speaker.Speak(initialSpeech);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
