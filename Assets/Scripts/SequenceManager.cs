using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private SettingsManager settings;
    [SerializeField] private string[] personas;
    [SerializeField] private TextAsset latinSquare;
    [SerializeField] private CoquiTTSController speaker;

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

    [Header("UI")] 
    [SerializeField] private TMP_InputField participantID;
    [SerializeField] private GameObject idPopup;

    private string[] _sequence;
    private int _currentPhase;
    private int _currentPersona;

    // todo make GUI for participant ID
    private void Start()
    {
        Invoke(nameof(HideFridge), 0.1f);
    }
    
    public void GenerateSequence()
    {
        if (string.IsNullOrWhiteSpace(participantID.text)) return;
        var idx = int.Parse(participantID.text);
        
        if (latinSquare == null) return;
        
        var lines = latinSquare.text.Trim().Split('\n');
        
        if (idx >= 0 && idx < lines.Length)
        {
            var line = lines[idx].Trim();
            _sequence =  line.Split(',');
            
            idPopup.SetActive(false);
            popup.SetActive(true);
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
        
        selectedPersona.name = personas[0];
        settings.ApplyChanges();
        _currentPhase++;
        _currentPersona++;
    }

    public void NextTask()
    {
        if (_currentPhase == 0 && _currentPersona == 0)
        {
            wakeWordDetector.SetActive(true);
            ttsManager.SetActive(true);
            fridge.SetActive(true);
    
            dialogTitle.text = title;
            dialogBody.text = body;
            
            NextPhase();
            return;
        }
        
        speaker.StopTalking();
        
        if (!debug) popup.SetActive(false);
        
        if (_currentPersona < personas.Length)
        {
            selectedPersona.name = personas[_currentPersona];
            settings.ApplyChanges();
            _currentPersona++;
        }
        else
        {
            _currentPersona = 0;
            NextPhase();
        }
    }

    private void HideFridge()
    {
        fridge.SetActive(false);
    }
}
