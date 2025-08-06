using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct SettingsData
{
    // --- Feedback Channel Settings ---
    public bool color;
    public bool sound;
    public bool blob;
    public bool face;
    public bool thought;

    // --- Agent Priming Settings ---
    public string agentPersonality;

    public static SettingsData GetDefaults()
    {
        return new SettingsData
        {
            color = false,
            sound = false,
            blob = false,
            face = false,
            thought = false,
            agentPersonality = "Happy"
        };
    }
}

public class SettingsManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FeedbackManager fridge;
    
    // --- Singleton Instance ---
    private static SettingsManager Instance { get; set; }

    // --- UI References ---
    [Header("UI Element References")]
    [Tooltip("Drag the UI components from your Hierarchy here.")]
    public Toggle colorToggle;
    public Toggle soundToggle;
    public Toggle blobToggle;
    public Toggle faceToggle;
    public Toggle thoughtToggle;
    public ToggleGroup personalityToggleGroup;

    // --- State Management ---
    public SettingsData activeSettings;  // The settings currently used by the application.
    private SettingsData _pendingSettings; // Temporary settings modified by the UI.
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings(); // Load saved settings on startup.
        }
    }

    private void Start()
    {
        // Since the panel is always open, sync the UI visuals once at the start.
        UpdateUIForms();
    }

    public void ApplyChanges()
    {
        // First, determine the selected personality from the ToggleGroup.
        var activeToggle = personalityToggleGroup.GetFirstActiveToggle();
        if (activeToggle != null)
        {
            // Set the personality in our pending settings based on the GameObject's name.
            _pendingSettings.agentPersonality = activeToggle.gameObject.name;
        }

        // Now, commit all pending settings to be the new active settings.
        activeSettings = _pendingSettings;
        
        // Finally, save the new active settings to disk.
        SaveSettings();

        Debug.Log("Settings Applied and Saved! Personality is now: " + activeSettings.agentPersonality);
        fridge.Reset();
        
        Invoke(nameof(FindNewFridge), 1f);
    }

    private void FindNewFridge()
    {
        fridge = FindAnyObjectByType<FeedbackManager>();
    }

    // --- Methods for individual On/Off Toggles ---
    // These methods modify the 'pendingSettings' only.
    public void SetColor(bool isEnabled) => _pendingSettings.color = isEnabled; 
    public void SetSound(bool isEnabled) => _pendingSettings.sound = isEnabled;
    public void SetBlob(bool isEnabled) => _pendingSettings.blob = isEnabled;
    public void SetFace(bool isEnabled) => _pendingSettings.face = isEnabled;
    public void SetThought(bool isEnabled) => _pendingSettings.thought = isEnabled;
    
    private void UpdateUIForms()
    {
        // Update the simple on/off toggles.
        colorToggle.isOn = _pendingSettings.color;
        soundToggle.isOn = _pendingSettings.sound;
        blobToggle.isOn = _pendingSettings.blob;
        faceToggle.isOn = _pendingSettings.face;
        thoughtToggle.isOn = _pendingSettings.thought;

        // Update the personality radio buttons.
        foreach (var toggle in personalityToggleGroup.GetComponentsInChildren<Toggle>())
        {
            // If this toggle's name matches our saved setting, make it active.
            if (toggle.gameObject.name != _pendingSettings.agentPersonality) continue;
            toggle.isOn = true;
            break; 
        }
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.SetInt("useColor", activeSettings.color ? 1 : 0);
        PlayerPrefs.SetInt("useSound", activeSettings.sound ? 1 : 0);
        PlayerPrefs.SetInt("useBlob", activeSettings.blob ? 1 : 0);
        PlayerPrefs.SetInt("useFace", activeSettings.face ? 1 : 0);
        PlayerPrefs.SetInt("useThought", activeSettings.thought ? 1 : 0);
        PlayerPrefs.SetString("agentPersonality", activeSettings.agentPersonality);
        PlayerPrefs.Save();
    }
    
    private void LoadSettings()
    {
        var loadedData = SettingsData.GetDefaults(); 
        
        loadedData.color = PlayerPrefs.GetInt("useColor", loadedData.color ? 1 : 0) == 1;
        loadedData.sound = PlayerPrefs.GetInt("useSound", loadedData.sound ? 1 : 0) == 1;
        loadedData.blob = PlayerPrefs.GetInt("useBlob", loadedData.blob ? 1 : 0) == 1;
        loadedData.face = PlayerPrefs.GetInt("useFace", loadedData.face ? 1 : 0) == 1;
        loadedData.thought = PlayerPrefs.GetInt("useThought", loadedData.thought ? 1 : 0) == 1;
        loadedData.agentPersonality = PlayerPrefs.GetString("agentPersonality", loadedData.agentPersonality);

        // On initial load, both the active and pending states are set to the loaded data.
        activeSettings = loadedData;
        _pendingSettings = loadedData;
    }
}