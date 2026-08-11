using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/App Settings", fileName = "AppSettings")]
    public sealed class AppSettings : ScriptableObject
    {
        [Header("Application")]
        [SerializeField] private string productName = "Сортировочная станция";
        [SerializeField] private string androidPackageName = "com.lunacharprod.sortingstation";
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1200f);

        [Header("Accessible input")]
        [SerializeField] [Min(80f)] private float minimumTargetSize = 120f;
        [SerializeField] [Min(8f)] private float targetGap = 24f;
        [SerializeField] [Range(0.05f, 0.8f)] private float inputCooldown = 0.18f;
        [SerializeField] [Min(0.5f)] private float parentHoldSeconds = 2f;

        [Header("Motion")]
        [SerializeField] [Min(0.1f)] private float wagonTravelSeconds = 1.1f;
        [SerializeField] [Min(0.1f)] private float feedbackSeconds = 0.65f;
        [SerializeField] [Range(0.9f, 1f)] private float pressedScale = 0.96f;

        [Header("Speech")]
        [SerializeField] private string speechLanguage = "ru_RU";
        [SerializeField] [Range(0.5f, 2f)] private float speechRate = 0.9f;
        [SerializeField] [Range(0.5f, 2f)] private float speechPitch = 1f;

        [Header("Theme")]
        [SerializeField] private Color skyColor = new Color(0.56f, 0.83f, 0.96f, 1f);
        [SerializeField] private Color groundColor = new Color(0.36f, 0.56f, 0.35f, 1f);
        [SerializeField] private Color panelColor = new Color(0.086f, 0.149f, 0.192f, 0.96f);
        [SerializeField] private Color panelAltColor = new Color(0.149f, 0.231f, 0.286f, 0.96f);
        [SerializeField] private Color surfaceColor = new Color(0.20f, 0.302f, 0.353f, 0.96f);
        [SerializeField] private Color primaryColor = new Color(0.404f, 0.78f, 0.949f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.831f, 0.369f, 1f);
        [SerializeField] private Color focusColor = new Color(1f, 0.831f, 0.369f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.345f, 0.82f, 0.576f, 1f);
        [SerializeField] private Color textColor = new Color(0.969f, 0.984f, 0.992f, 1f);
        [SerializeField] private Color mutedTextColor = new Color(0.792f, 0.851f, 0.878f, 1f);
        [SerializeField] private Color textOnBrightColor = new Color(0.063f, 0.137f, 0.176f, 1f);
        [SerializeField] private Color errorColor = new Color(0.949f, 0.486f, 0.42f, 1f);
        [SerializeField] private Color brakeColor = new Color(0.949f, 0.486f, 0.42f, 1f);

        [Header("Typography")]
        [SerializeField] [Min(24)] private int displayFontSize = 64;
        [SerializeField] [Min(20)] private int titleFontSize = 48;
        [SerializeField] [Min(18)] private int controlFontSize = 32;
        [SerializeField] [Min(16)] private int statusFontSize = 26;
        [SerializeField] [Min(14)] private int captionFontSize = 22;

        public string ProductName => productName;
        public string AndroidPackageName => androidPackageName;
        public Vector2 ReferenceResolution => referenceResolution;
        public float MinimumTargetSize => Mathf.Max(80f, minimumTargetSize);
        public float TargetGap => Mathf.Max(8f, targetGap);
        public float InputCooldown => Mathf.Clamp(inputCooldown, 0.05f, 0.8f);
        public float ParentHoldSeconds => Mathf.Max(0.5f, parentHoldSeconds);
        public float WagonTravelSeconds => Mathf.Max(0.1f, wagonTravelSeconds);
        public float FeedbackSeconds => Mathf.Max(0.1f, feedbackSeconds);
        public float PressedScale => Mathf.Clamp(pressedScale, 0.95f, 1f);
        public string SpeechLanguage => string.IsNullOrWhiteSpace(speechLanguage) ? "ru_RU" : speechLanguage;
        public float SpeechRate => Mathf.Clamp(speechRate, 0.5f, 2f);
        public float SpeechPitch => Mathf.Clamp(speechPitch, 0.5f, 2f);
        public Color SkyColor => skyColor;
        public Color GroundColor => groundColor;
        public Color PanelColor => panelColor;
        public Color PanelAltColor => panelAltColor;
        public Color SurfaceColor => surfaceColor;
        public Color PrimaryColor => primaryColor;
        public Color AccentColor => accentColor;
        public Color FocusColor => focusColor;
        public Color SelectedColor => selectedColor;
        public Color TextColor => textColor;
        public Color MutedTextColor => mutedTextColor;
        public Color TextOnBrightColor => textOnBrightColor;
        public Color ErrorColor => errorColor;
        public Color BrakeColor => brakeColor;
        public int DisplayFontSize => Mathf.Max(24, displayFontSize);
        public int TitleFontSize => Mathf.Max(20, titleFontSize);
        public int ControlFontSize => Mathf.Max(18, controlFontSize);
        public int StatusFontSize => Mathf.Max(16, statusFontSize);
        public int CaptionFontSize => Mathf.Max(14, captionFontSize);

        public Color TextForBackground(Color background)
        {
            float lightContrast = ContrastRatio(textColor, background);
            float darkContrast = ContrastRatio(textOnBrightColor, background);
            return darkContrast > lightContrast ? textOnBrightColor : textColor;
        }

        public static float ContrastRatio(Color foreground, Color background)
        {
            Color composite = new Color(
                foreground.r * foreground.a + background.r * (1f - foreground.a),
                foreground.g * foreground.a + background.g * (1f - foreground.a),
                foreground.b * foreground.a + background.b * (1f - foreground.a), 1f);
            float a = RelativeLuminance(composite);
            float b = RelativeLuminance(background);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        private static float RelativeLuminance(Color value)
        {
            return 0.2126f * Linear(value.r) + 0.7152f * Linear(value.g) + 0.0722f * Linear(value.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

#if UNITY_EDITOR
        public void ConfigureTheme()
        {
            skyColor = Hex("#8ED4F4");
            groundColor = Hex("#5C8F59");
            panelColor = WithAlpha(Hex("#162631"), 0.96f);
            panelAltColor = WithAlpha(Hex("#263B49"), 0.96f);
            surfaceColor = WithAlpha(Hex("#334D5A"), 0.96f);
            primaryColor = Hex("#67C7F2");
            accentColor = Hex("#FFD45E");
            focusColor = Hex("#FFD45E");
            selectedColor = Hex("#58D193");
            textColor = Hex("#F7FBFD");
            mutedTextColor = Hex("#CAD9E0");
            textOnBrightColor = Hex("#10232D");
            errorColor = Hex("#F27C6B");
            brakeColor = errorColor;
            minimumTargetSize = 120f;
            targetGap = 24f;
            pressedScale = 0.96f;
            displayFontSize = 64;
            titleFontSize = 48;
            controlFontSize = 32;
            statusFontSize = 26;
            captionFontSize = 22;
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color parsed) ? parsed : Color.white;
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }
#endif
    }
}
