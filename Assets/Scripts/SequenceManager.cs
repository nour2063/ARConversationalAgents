using System.IO;
using System.Text;
using TMPro;
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
    [SerializeField] private GameObject surveyPopup;
    [SerializeField] private UnityEngine.UI.Slider pleasure;
    [SerializeField] private UnityEngine.UI.Slider arousal;
    [SerializeField] private UnityEngine.UI.Slider dominance;
    [SerializeField] private TextMeshProUGUI emotionalState;

    private string[] _sequence;
    private int _currentPhase;
    private int _currentPersona;
    private bool _firstStart = true;
    
    private const string Header = "Condition,Emotion,Pleasure,Arousal,Dominance,EmotionalState";

    // todo make GUI for participant ID
    private void Start()
    {
        Invoke(nameof(HideFridge), 0.1f);
    }
    
    public void GenerateSequence()
    {
        if (string.IsNullOrWhiteSpace(participantID.text)) return;
        var idx = int.Parse(participantID.text) % 14; // number of combinations in latin sqare
        
        Debug.Log("Participant number: " + participantID.text + ", Sequence ID: " + idx);
        
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
    }

    public void NextTask()
    {
        if (_firstStart)
        {
            wakeWordDetector.SetActive(true);
            ttsManager.SetActive(true);
            fridge.SetActive(true);
    
            dialogTitle.text = title;
            dialogBody.text = body;
            
            _firstStart = false;
            NextPhase();
            return;
        }
        
        speaker.StopTalking();
        
        popup.SetActive(false);
        ShowSurvey();
    }

    private void HideFridge()
    {
        fridge.SetActive(false);
    }

    private void ShowSurvey()
    {
        surveyPopup.SetActive(true);
        settings.fridge.transform.parent.gameObject.SetActive(false);
    }
    
    public void WriteFeedbackToFile()
    {
        // 1. Define the file path using the participant's ID
        var filePath = Path.Combine(Application.persistentDataPath, $"{participantID.text}.csv");

        // 2. Check if the file exists to determine if we need to add a header
        var fileExists = File.Exists(filePath);

        try
        {
            // Use a StreamWriter to append data. 'true' enables append mode.
            using (var writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                // If the file is new, write the header first
                if (!fileExists)
                {
                    writer.WriteLine(Header);
                }

                // 3. Create the new data row
                var dataRow = $"{_sequence[_currentPhase]},{personas[_currentPersona]},{pleasure.value},{arousal.value},{dominance.value},{emotionalState.text}";

                // 4. Write the new row to the file
                writer.WriteLine(dataRow);
            }

            surveyPopup.SetActive(false);
            pleasure.value = 1;
            arousal.value = 1;
            dominance.value = 1;
            emotionalState.text = "Select Emotion";
            
            Debug.Log($"<color=lime>Successfully wrote to: {filePath}</color>");
            
            if (_currentPersona < personas.Length - 1)
            {
                _currentPersona++;
                selectedPersona.name = personas[_currentPersona];
                settings.ApplyChanges();
            }
            else
            {
                _currentPersona = 0;
                NextPhase();
                _currentPhase++;
            }
            
            if (debug) popup.SetActive(true);
            settings.fridge.transform.parent.gameObject.SetActive(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write to CSV. Error: {e.Message}");
        }
    }
}
