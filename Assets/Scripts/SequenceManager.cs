using TMPro;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private SettingsManager settings;
    [SerializeField] private string[] personas;
    [SerializeField] private TextAsset latinSquare;

    [Header("Objects")] 
    [SerializeField] private GameObject wakeWordDetector;
    [SerializeField] private GameObject ttsManager;
    [SerializeField] private GameObject fridge;

    [Header("Dialog")]
    [SerializeField] private GameObject popup;
    [SerializeField] private TextMeshProUGUI dialogTitle;
    [SerializeField] private TextMeshProUGUI dialogBody;
    [SerializeField] private string title;
    [SerializeField] private string body;
    [SerializeField] private GameObject selectedPersona;
    [SerializeField] private bool debug = true;

    private string[] _sequence;
    private int _currentPhase;
    private int _currentPersona;

    // todo make GUI for participant ID
    private void Start()
    {
        GenerateSequence(0);
        
        Invoke(nameof(HideFridge), 0.1f);
    }
    
    public void GenerateSequence(int idx)
    {
        if (latinSquare == null) return;
        
        var lines = latinSquare.text.Trim().Split('\n');
        
        if (idx >= 0 && idx < lines.Length)
        {
            var line = lines[idx].Trim();
            _sequence =  line.Split(',');
        }
        else
        {
            Debug.LogWarning($"Row index {idx} is out of bounds. The file has {lines.Length} lines.");
        }
    }

    private void NextPhase()
    {
        if (_currentPhase >= _sequence.Length) return;
        
        var task = _sequence[_currentPhase].Split('+');
        settings.pendingSettings = SettingsData.GetDefaults();
        
        foreach (var option in task)
        {
            switch (option)
            {
                case "color":
                    settings.SetColor(true);
                    break;
                case "sound":
                    settings.SetSound(true);
                    break;
                case "blob":
                    settings.SetBlob(true);
                    break;
                case "face":
                    settings.SetFace(true);
                    break;
            }
        }
        
        settings.ApplyChanges();
        _currentPhase++;
    }

    private void NextPersonality()
    {
        if (_currentPersona >= personas.Length) return;
        selectedPersona.name = personas[_currentPersona];
        settings.ApplyChanges();
        _currentPersona++;
    }

    public void NextTask()
    {
        if (!wakeWordDetector.activeInHierarchy)
        {
            dialogTitle.text = title;
            dialogBody.text = body;
        
            wakeWordDetector.SetActive(true);
            ttsManager.SetActive(true);
            fridge.SetActive(true);
        }
        
        if (!debug) popup.SetActive(false);
        
        if (_currentPersona != personas.Length - 1)
        {
            NextPersonality();
        }
        else
        {
            NextPhase();
        }
    }

    private void HideFridge()
    {
        fridge.SetActive(false);
    }
}
