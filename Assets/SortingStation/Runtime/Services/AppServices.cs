using UnityEngine;

namespace SortingStation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class AppServices : MonoBehaviour
    {
        private const string ConfigRoot = "Configuration/";
        public static AppServices Instance { get; private set; }

        public AppSettings Settings { get; private set; }
        public GameCatalog Games { get; private set; }
        public VisualCatalog Visuals { get; private set; }
        public AudioCatalog AudioCatalog { get; private set; }
        public CabRideDefinition CabRide { get; private set; }
        public CabSceneryCatalog CabScenery { get; private set; }
        public IAudioService Audio { get; private set; }
        public ISpeechService Speech { get; private set; }
        public IProgressRepository Progress { get; private set; }
        public InputRouter Input { get; private set; }
        public GameSession Session { get; private set; }
        public UserPreferences Preferences { get; private set; }

        public static AppServices Ensure()
        {
            if (Instance != null) return Instance;
            AppServices existing = FindObjectOfType<AppServices>();
            if (existing != null) return existing;
            GameObject root = new GameObject("AppServices");
            return root.AddComponent<AppServices>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfiguration();
            Progress = new PlayerPrefsProgressRepository();
            Preferences = Progress.LoadPreferences();
            Session = new GameSession();

            Input = gameObject.GetComponent<InputRouter>() ?? gameObject.AddComponent<InputRouter>();
            Input.SetCooldown(Preferences.inputCooldown > 0f ? Preferences.inputCooldown : Settings.InputCooldown);

            AudioService audio = gameObject.GetComponent<AudioService>() ?? gameObject.AddComponent<AudioService>();
            audio.Initialize(AudioCatalog, Preferences);
            Audio = audio;

            SpeechService speech = gameObject.GetComponent<SpeechService>() ?? gameObject.AddComponent<SpeechService>();
            speech.Initialize(Settings, Audio, Preferences);
            Speech = speech;
        }

        public void SavePreferences()
        {
            Preferences.inputCooldown = Mathf.Clamp(Preferences.inputCooldown, 0.05f, 0.8f);
            Progress.SavePreferences(Preferences);
            Input.SetCooldown(Preferences.inputCooldown);
            Audio.ApplyPreferences(Preferences);
            if (Speech is SpeechService speech) speech.SetPreferences(Preferences);
        }

        private void LoadConfiguration()
        {
            Settings = Resources.Load<AppSettings>(ConfigRoot + "AppSettings");
            Games = Resources.Load<GameCatalog>(ConfigRoot + "GameCatalog");
            Visuals = Resources.Load<VisualCatalog>(ConfigRoot + "VisualCatalog");
            AudioCatalog = Resources.Load<AudioCatalog>(ConfigRoot + "AudioCatalog");
            CabRide = Resources.Load<CabRideDefinition>(ConfigRoot + "CabRideDefinition");
            CabScenery = Resources.Load<CabSceneryCatalog>(ConfigRoot + "CabSceneryCatalog");

            if (Settings == null) Settings = ScriptableObject.CreateInstance<AppSettings>();
            if (Games == null) Games = ScriptableObject.CreateInstance<GameCatalog>();
            if (Visuals == null) Visuals = ScriptableObject.CreateInstance<VisualCatalog>();
            if (AudioCatalog == null) AudioCatalog = ScriptableObject.CreateInstance<AudioCatalog>();
            if (CabRide == null) CabRide = ScriptableObject.CreateInstance<CabRideDefinition>();
            if (CabScenery == null) CabScenery = ScriptableObject.CreateInstance<CabSceneryCatalog>();
        }
    }
}
