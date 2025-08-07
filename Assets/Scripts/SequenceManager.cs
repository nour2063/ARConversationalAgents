using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private SettingsManager settings;
    [SerializeField] private string[] personas;
    [SerializeField] private TextAsset latinSquare;

    private string[] _sequence;
    private int _currentPhase;
    private int _currentPersona;
    
    public void GenerateSequence(int idx)
    {
        if (latinSquare == null) return;
        
        var lines = latinSquare.text.Trim().Split('\n');
        
        if (idx >= 0 && idx < lines.Length)
        {
            var line = lines[idx].Trim();
            _sequence =  line.Split(',');
        }

        Debug.LogWarning($"Row index {idx} is out of bounds. The file has {lines.Length} lines.");
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
                    settings.pendingSettings.color = true;
                    break;
                case "sound":
                    settings.pendingSettings.sound = true;
                    break;
                case "blob":
                    settings.pendingSettings.blob = true;
                    break;
                case "face":
                    settings.pendingSettings.face = true;
                    break;
            }
        }
        
        settings.ApplyChanges();
        _currentPhase++;
    }

    private void NextPersonality()
    {
        if (_currentPersona >= personas.Length) return;
        settings.pendingSettings.agentPersonality = personas[_currentPersona];
        settings.ApplyChanges();
        _currentPersona++;
    }

    public void NextTask()
    {
        if (_currentPersona < personas.Length)
        {
            NextPersonality();
        }
        else
        {
            _currentPersona = 0;
            NextPhase();
        }
    }
}
