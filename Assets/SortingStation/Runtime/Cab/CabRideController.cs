using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class CabRideController : MonoBehaviour
    {
        private AppServices services;
        private CabMotionModel motion;
        private CabWorldRenderer world;
        private CabJourneyDirector journey;
        private CabInteractionDirector interactions;
        private CabStationStopDirector stationStop;
        private RectTransform root;
        private RectTransform stage;
        private RectTransform cabInterior;
        private AccessibleFocusGroup focusGroup;
        private readonly Dictionary<CabControlAction, AccessibleButton> controls = new Dictionary<CabControlAction, AccessibleButton>();
        private readonly Dictionary<CabControlAction, Image> controlArtwork = new Dictionary<CabControlAction, Image>();
        private TextMeshProUGUI status;
        private TextMeshProUGUI statusCursor;
        private TextMeshProUGUI speedDisplay;
        private TextMeshProUGUI radioDisplay;
        private TextMeshProUGUI radioTrackTitle;
        private TextMeshProUGUI radioTime;
        private TextMeshProUGUI radioState;
        private RectTransform radioPlayer;
        private RectTransform radioPlaylist;
        private ScrollRect radioPlaylistScroll;
        private RectTransform radioPlaylistViewport;
        private RectTransform radioPlaylistContent;
        private Scrollbar radioPlaylistScrollbar;
        private AccessibleButton radioPowerButton;
        private AccessibleButton radioPlaylistButton;
        private AccessibleButton onlineRadioButton;
        private AccessibleButton radioVolumeButton;
        private Image radioNightGlow;
        private Image radioStateLamp;
        private Image radioProgressFill;
        private RectTransform radioPlayGlyph;
        private RectTransform radioPauseGlyph;
        private RectTransform radioPlaylistGlyph;
        private RectTransform onlineRadioGlyph;
        private readonly List<AccessibleButton> radioTrackButtons = new List<AccessibleButton>(4);
        private readonly List<TextMeshProUGUI> radioTrackLabels = new List<TextMeshProUGUI>(12);
        private readonly List<RectTransform> radioEqualizerBars = new List<RectTransform>(5);
        private bool playlistOpen;
        private Vector2Int lastRadioLayoutScreenSize = new Vector2Int(-1, -1);
        private Rect lastRadioLayoutSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private TextMeshProUGUI routeDisplay;
        private Image headlightGlow;
        private Image cabinGlow;
        private Image instrumentGlow;
        private Image throttleGrip;
        private Image brakeGrip;
        private RectTransform leftWiper;
        private RectTransform rightWiper;
        private RectTransform keychain;
        private AccessibleButton keychainButton;
        private Vector2 cabinSwayOffset;
        private Vector2 cabinSwayVelocity;
        private float cabinSwayAngle;
        private float cabinSwayAngleVelocity;
        private float keychainAngle;
        private float keychainAngularVelocity;
        private float keychainPointerX;
        private bool keychainPointerActive;
        private float keychainRailPhase;
        private float headlightAlpha;
        private float cabinAlpha;
        private bool headlights;
        private bool cabinLight;
        private bool wipers;
        private bool radio;
        private bool windowHeater;
        private bool pointerBrake;
        private float pointerBrakeStrength;
        private float brakePulseUntil;
        private float hornGlowUntil;
        private float bellGlowUntil;
        private bool hornLit;
        private bool bellLit;
        private int lastBrakePercent = -1;
        private bool lastBrakeActive;
        private AccessibleButton dispatcherButton;
        private Image dispatcherLamp;
        private bool departureAuthorized;
        private bool vigilanceAlarm;
        private bool automaticStop;
        private float nextVigilanceAt;
        private float vigilanceDeadline;
        private float nextVigilanceBeep;
        private int trainNumber;
        private WeatherType lastDispatcherWeather = (WeatherType)(-1);
        private bool throttleGripPressed;

        public static Rect StatusDisplayRect => new Rect(0.585f, 0.345f, 0.120f, 0.100f);
        public static Rect SpeedDisplayRect => new Rect(0.720f, 0.365f, 0.135f, 0.110f);

        public event Action<CabControlAction> ControlActivated;

        private void Start()
        {
            services = AppServices.Ensure();
            services.Audio.StopAllLoops();
            motion = new CabMotionModel(services.CabRide);
            trainNumber = 2400 + Mathf.Abs(services.CabRide.RouteSeed % 6000);
            Build();
            PromptDeparture();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateRadioPlayerLayout();
            UpdateTerminalCursor();
            bool keyboardBrake = false;
            Keyboard keyboard = Keyboard.current;
            HandleDirectShortcuts(keyboard);
            if (keyboard != null && controls.TryGetValue(CabControlAction.Brake, out AccessibleButton brakeButton))
            {
                keyboardBrake = focusGroup.Current == brakeButton &&
                                (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
            }

            UpdateVigilance();
            RouteSegmentType segment = world != null && world.CurrentSegment != null ? world.CurrentSegment.Type : RouteSegmentType.Meadow;
            stationStop?.Step(motion.Speed01, dt, segment, world != null && world.TunnelBlend > 0.05f,
                vigilanceAlarm || automaticStop);
            float manualBrake = pointerBrake ? pointerBrakeStrength : keyboardBrake || Time.unscaledTime < brakePulseUntil ? 1f : 0f;
            float brake01 = automaticStop ? 1f : Mathf.Max(manualBrake, stationStop != null ? stationStop.BrakeStrength : 0f);
            motion.Step(dt, brake01, stationStop != null ? stationStop.TractionMultiplier : 1f);
            services.Audio.SetRails(motion.Speed01);
            world.Advance(motion.Speed01, motion.Acceleration01, dt);
            journey?.Step(motion.Speed01, dt, headlights, wipers);
            interactions?.Step(motion.Speed01, dt);
            UpdateInstruments();
            UpdateLighting(dt);
            AnimateWipers(dt);
            AnimateCabSway(dt);
            AnimateKeychain(dt);
            UpdateBrakeVisual(brake01);
            UpdateMomentaryGlow(CabControlAction.Horn, hornGlowUntil, ref hornLit);
            UpdateMomentaryGlow(CabControlAction.Bell, bellGlowUntil, ref bellLit);
            UpdateDispatcherVisual();
        }

        private void OnDestroy()
        {
            if (services == null) return;
            if (world != null)
            {
                world.SegmentChanged -= OnSegmentChanged;
                world.AmbientSoundRequested -= OnAmbientSoundRequested;
            }
            services.Audio.SetRails(0f);
            services.Audio.SetWeather(WeatherType.Clear, 0f);
            services.Audio.SetRadio(false);
            services.Audio.SetOnlineRadio(false);
            services.Speech.Stop();
        }

        private void OnAmbientSoundRequested(CabAmbientSoundRequest request)
        {
            services.Audio.PlayAmbient(request.Cue, request.SuggestedDurationSeconds);
            if (request.Cue == CabAmbientSound.City)
                services.Audio.PlayDispatcher(DispatcherVoiceCue.EnteringCity);
        }

        public void ConfigureSmokeDemo(bool tunnel)
        {
            departureAuthorized = true;
            vigilanceAlarm = false;
            automaticStop = false;
            if (dispatcherButton != null) dispatcherButton.gameObject.SetActive(false);
            motion.SetThrottle(0.625f);
            headlights = true;
            cabinLight = true;
            wipers = true;
            UpdateThrottleVisual();
            UpdateToggleVisual(CabControlAction.Headlights, true);
            UpdateToggleVisual(CabControlAction.CabinLight, true);
            UpdateToggleVisual(CabControlAction.Wipers, true);
            if (tunnel) world.SetPreviewSegment(RouteSegmentType.MountainTunnel, 0.48f);
        }

        public void ConfigureTrackPreview(RouteSegmentType type, float progress)
        {
            departureAuthorized = true;
            vigilanceAlarm = false;
            automaticStop = false;
            if (dispatcherButton != null) dispatcherButton.gameObject.SetActive(false);
            motion.SetThrottle(0.625f);
            UpdateThrottleVisual();
            world.SetPreviewSegment(type, progress);
        }

        public void ConfigureWeatherPreview(bool enableWipers)
        {
            ConfigureSmokeDemo(false);
            motion.SetThrottle(0f);
            UpdateThrottleVisual();
            wipers = enableWipers;
            UpdateToggleVisual(CabControlAction.Wipers, wipers);
            journey?.SetPreviewWeather(WeatherType.Rain, 1f);
        }

        public void ConfigureRadioUiPreview()
        {
            departureAuthorized = true;
            vigilanceAlarm = false;
            automaticStop = false;
            if (dispatcherButton != null) dispatcherButton.gameObject.SetActive(false);
            motion.SetThrottle(0.56f);
            UpdateThrottleVisual();
            radio = true;
            services.Audio.SetRadio(true);
            playlistOpen = true;
            if (radioPlaylist != null) radioPlaylist.gameObject.SetActive(true);
            UpdateRadioPlayer();
        }

        public void ConfigureAutumnLeafPreview(bool enableWipers)
        {
            ConfigureSmokeDemo(false);
            wipers = enableWipers;
            UpdateToggleVisual(CabControlAction.Wipers, wipers);
            journey?.SetAutumnLeafPreview(true);
        }

        public void ConfigureSceneryPreview(RouteSegmentType type, float progress, float distance)
        {
            ConfigureTrackPreview(type, progress);
            world.SetPreviewDistance(distance);
        }

        public void ConfigureSceneryPreviewAtSpeed(RouteSegmentType type, float progress, float distance, float throttle)
        {
            ConfigureSceneryPreview(type, progress, distance);
            motion.SetThrottle(Mathf.Clamp01(throttle));
            UpdateThrottleVisual();
        }

        private void Build()
        {
            Canvas canvas;
            root = UiFactory.CreateScreen("CabRideCanvas", out canvas);
            AppSettings theme = services.Settings;

            RectTransform background = UiFactory.Panel("CabBackground", root, new Color(0.025f, 0.045f, 0.055f, 1f));
            UiFactory.Stretch(background);
            background.GetComponent<Image>().raycastTarget = false;

            stage = UiFactory.Panel("CabStage", root, Color.black);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(1500f, 1000f);
            stage.GetComponent<Image>().raycastTarget = false;
            AspectRatioFitter fitter = stage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1.5f;
            Canvas.ForceUpdateCanvases();

            world = stage.gameObject.AddComponent<CabWorldRenderer>();
            world.Initialize(stage, services.Visuals.cabLandscape, services.CabRide, services.CabScenery, services.Preferences);
            world.SegmentChanged += OnSegmentChanged;
            world.AmbientSoundRequested += OnAmbientSoundRequested;
            SeasonType selectedSeason = services.Session.ResolveSeason(services.Preferences.seasonMode,
                services.CabRide.RouteSeed + services.Session.ReplaySeed * 7919);
            journey = stage.gameObject.AddComponent<CabJourneyDirector>();
            journey.Initialize(world, services.CabRide, services.CabEnvironment, selectedSeason, services.Preferences,
                services.CabRide.RouteSeed + services.Session.ReplaySeed * 7919);
            journey.StatusRequested += OnJourneyStatus;
            journey.WeatherChanged += OnJourneyWeather;

            BuildHeadlightLayer();
            BuildCabInterior();
            BuildWipers();

            Sprite overlaySprite = services.CabScenery != null ? services.CabScenery.CabOverlay : null;
            Image overlay = UiFactory.Image("CabOverlay", cabInterior, overlaySprite, Color.white, false);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;
            if (overlay.sprite == null)
            {
                overlay.color = new Color(0.18f, 0.22f, 0.25f, 0.88f);
            }

            BuildCabLighting();
            BuildKeychain();
            BuildDisplays();
            BuildControls();
            BuildDispatcherButton();
            BuildRadioPlayer();
            BuildKeychainInteraction();
            BuildHeader();
            BuildGentleInteractions();
            UpdateThrottleVisual();
            UpdateToggleVisual(CabControlAction.Headlights, false);
            UpdateToggleVisual(CabControlAction.CabinLight, false);
            UpdateToggleVisual(CabControlAction.Wipers, false);
            UpdateToggleVisual(CabControlAction.Radio, false);
            UpdateToggleVisual(CabControlAction.Doors, false);
            UpdateToggleVisual(CabControlAction.WindowHeater, false);
        }

        private void BuildGentleInteractions()
        {
            int seed = services.CabRide.RouteSeed + services.Session.ReplaySeed * 7919;
            stationStop = stage.gameObject.AddComponent<CabStationStopDirector>();
            stationStop.Initialize(services.CabInteractions, seed);
            stationStop.PhaseChanged += OnStationPhaseChanged;
            interactions = stage.gameObject.AddComponent<CabInteractionDirector>();
            interactions.Initialize(root, services.CabInteractions, world, journey, services.Audio, services.Preferences, seed);
        }

        private void OnStationPhaseChanged(CabStationPhase phase)
        {
            bool open = phase == CabStationPhase.DoorsOpen;
            UpdateToggleVisual(CabControlAction.Doors, open);
            if (phase == CabStationPhase.Approaching) SetStatus("Впереди станция. Плавная остановка");
            else if (phase == CabStationPhase.WaitingForDoors) SetStatus("Станция. Можно открыть двери");
            else if (phase == CabStationPhase.DoorsOpen) SetStatus("Двери открыты");
            else if (phase == CabStationPhase.Releasing) SetStatus("Двери закрыты. Можно продолжать путь");
        }

        private void BuildHeadlightLayer()
        {
            Sprite mask = services.CabScenery != null ? services.CabScenery.HeadlightMask : null;
            headlightGlow = UiFactory.Image("HeadlightCone", stage, mask != null ? mask : CreateHeadlightSprite(),
                new Color(1f, 0.91f, 0.64f, 0f), false);
            UiFactory.SetRect(headlightGlow.rectTransform, new Vector2(0.18f, 0.46f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
            headlightGlow.raycastTarget = false;
        }

        private void BuildCabInterior()
        {
            cabInterior = UiFactory.Panel("CabInterior", stage, Color.clear);
            UiFactory.Stretch(cabInterior);
            cabInterior.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildCabLighting()
        {
            Sprite mask = services.CabScenery != null ? services.CabScenery.CabinLightMask : null;
            cabinGlow = UiFactory.Image("CabinGlow", cabInterior, mask != null ? mask : CreateRadialGlowSprite(),
                new Color(1f, 0.67f, 0.25f, 0f), false);
            UiFactory.SetRect(cabinGlow.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.58f), Vector2.zero, Vector2.zero);
            cabinGlow.raycastTarget = false;

            instrumentGlow = UiFactory.Image("InstrumentGlow", cabInterior, CreateRadialGlowSprite(),
                new Color(0.25f, 0.72f, 1f, 0.08f), false);
            UiFactory.SetRect(instrumentGlow.rectTransform, new Vector2(0.12f, 0.22f), new Vector2(0.90f, 0.52f), Vector2.zero, Vector2.zero);
            instrumentGlow.raycastTarget = false;
        }

        private void BuildWipers()
        {
            leftWiper = CreateWiper("LeftWiper", new Vector2(0.41f, 0.455f), 61f);
            rightWiper = CreateWiper("RightWiper", new Vector2(0.59f, 0.455f), -61f);
        }

        private RectTransform CreateWiper(string name, Vector2 anchor, float angle)
        {
            RectTransform wiper = UiFactory.Panel(name, cabInterior, new Color(0.025f, 0.032f, 0.036f, 1f), UiFactory.RoundedSprite());
            wiper.anchorMin = wiper.anchorMax = anchor;
            wiper.pivot = new Vector2(0.5f, 0f);
            wiper.sizeDelta = new Vector2(13f, 372f);
            wiper.anchoredPosition = Vector2.zero;
            wiper.localRotation = Quaternion.Euler(0f, 0f, angle);
            wiper.GetComponent<Image>().raycastTarget = false;
            return wiper;
        }

        private void BuildKeychain()
        {
            GameObject rootObject = new GameObject("Keychain", typeof(RectTransform));
            rootObject.transform.SetParent(cabInterior, false);
            keychain = rootObject.GetComponent<RectTransform>();
            keychain.anchorMin = keychain.anchorMax = new Vector2(0.535f, 0.615f);
            keychain.pivot = new Vector2(0.5f, 1f);
            keychain.sizeDelta = new Vector2(124f, 168f);

            Sprite charmSprite = services.CabScenery != null ? services.CabScenery.Keychain : null;
            if (charmSprite != null)
            {
                Image charm = UiFactory.Image("TrainCharm", keychain, charmSprite, Color.white, true);
                UiFactory.Anchor(charm.rectTransform, new Vector2(0.5f, 1f), new Vector2(100f, 150f), new Vector2(0f, -75f));
                charm.raycastTarget = false;
            }
            else
            {
                RectTransform cord = UiFactory.Panel("Cord", keychain, new Color(0.10f, 0.08f, 0.06f, 0.95f), UiFactory.RoundedSprite());
                UiFactory.Anchor(cord, new Vector2(0.5f, 1f), new Vector2(5f, 70f), new Vector2(0f, -35f));
                cord.GetComponent<Image>().raycastTarget = false;
                Image charm = UiFactory.Image("TrainCharm", keychain, null, services.Settings.PrimaryColor, true);
                UiFactory.Anchor(charm.rectTransform, new Vector2(0.5f, 1f), new Vector2(82f, 72f), new Vector2(0f, -102f));
                charm.raycastTarget = false;
                charm.sprite = UiFactory.RoundedSprite();
                charm.type = Image.Type.Sliced;
                TextMeshProUGUI fallback = UiFactory.Label("CharmLabel", charm.transform, "ПОЕЗД", 17,
                    services.Settings.TextOnBrightColor, TextAlignmentOptions.Center, UiFontRole.Control);
                UiFactory.Stretch(fallback.rectTransform, 5f, 5f, 5f, 5f);
            }
        }

        private void BuildKeychainInteraction()
        {
            if (keychain == null || focusGroup == null) return;
            Image hitArea = keychain.gameObject.AddComponent<Image>();
            hitArea.sprite = UiFactory.RoundedSprite();
            hitArea.type = Image.Type.Sliced;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            keychainButton = keychain.gameObject.AddComponent<AccessibleButton>();
            keychainButton.Initialize(focusGroup, null,
                "Брелок-локомотив. Нажмите или потяните, чтобы раскачать.",
                Color.clear, Color.clear, services.Settings.FocusColor, services.Settings.PressedScale,
                Color.clear, Color.clear, NudgeKeychain);
            keychainButton.ConfigurePersistentPress(false, 0f);
            keychainButton.PointerPressed += BeginKeychainPush;
            keychainButton.PointerDragged += DragKeychain;
            keychainButton.PointerReleased += EndKeychainPush;
        }

        private void BeginKeychainPush(PointerEventData eventData)
        {
            keychainPointerActive = TryGetKeychainPointerX(eventData, out keychainPointerX);
        }

        private void DragKeychain(PointerEventData eventData)
        {
            if (!keychainPointerActive || !TryGetKeychainPointerX(eventData, out float pointerX)) return;
            float delta = pointerX - keychainPointerX;
            keychainPointerX = pointerX;
            if (Mathf.Abs(delta) < 0.2f) return;
            AddKeychainImpulse(delta * 0.75f);
            SetStatus("Брелок качается");
        }

        private void EndKeychainPush(PointerEventData eventData)
        {
            keychainPointerActive = false;
        }

        private bool TryGetKeychainPointerX(PointerEventData eventData, out float pointerX)
        {
            pointerX = 0f;
            if (keychain == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(keychain,
                    eventData.position, eventData.pressEventCamera, out Vector2 local)) return false;
            pointerX = local.x;
            return true;
        }

        private void NudgeKeychain()
        {
            float direction = Mathf.Abs(keychainAngle) < 0.25f ? 1f : -Mathf.Sign(keychainAngle);
            AddKeychainImpulse(58f * direction);
            SetStatus("Брелок качается");
        }

        private void AddKeychainImpulse(float impulse)
        {
            keychainAngularVelocity = Mathf.Clamp(keychainAngularVelocity + impulse, -190f, 190f);
        }

        private void BuildDisplays()
        {
            AppSettings theme = services.Settings;
            RectTransform statusScreen = BuildTerminalScreen("CabStatusScreen", StatusDisplayRect, -3.2f);
            status = UiFactory.Label("CabStatus", statusScreen, string.Empty, theme.CaptionFontSize,
                TerminalGreen(), TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.Stretch(status.rectTransform, 8f, 5f, 20f, 5f);
            ApplyTerminalText(status, false);
            statusCursor = UiFactory.Label("CabStatusCursor", statusScreen, "|", theme.CaptionFontSize,
                TerminalGreen(), TextAlignmentOptions.MidlineRight, UiFontRole.Body);
            UiFactory.SetRect(statusCursor.rectTransform, new Vector2(0.88f, 0.14f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
            ApplyTerminalText(statusCursor, false);

            RectTransform speedScreen = BuildTerminalScreen("SpeedScreen", SpeedDisplayRect, 1.4f);
            speedDisplay = UiFactory.Label("SpeedDisplay", speedScreen, "0 км/ч", theme.ControlFontSize,
                TerminalGreen(), TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.Stretch(speedDisplay.rectTransform, 7f, 4f, 7f, 4f);
            ApplyTerminalText(speedDisplay, true);

            radioDisplay = UiFactory.Label("RadioDisplay", cabInterior, "Радио выключено", theme.CaptionFontSize,
                new Color(0.70f, 0.93f, 0.83f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(radioDisplay.rectTransform, new Vector2(0.365f, 0.335f), new Vector2(0.478f, 0.425f), Vector2.zero, Vector2.zero);
        }

        private RectTransform BuildTerminalScreen(string name, Rect rect, float rotationDegrees)
        {
            RectTransform screen = UiFactory.Panel(name, cabInterior, new Color(0.005f, 0.038f, 0.018f, 0.84f), UiFactory.RoundedSprite());
            UiFactory.SetRect(screen, rect.min, rect.max, Vector2.zero, Vector2.zero);
            screen.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Image image = screen.GetComponent<Image>();
            image.raycastTarget = false;
            Outline outline = screen.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.96f, 0.36f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);
            Shadow glow = screen.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(0.02f, 0.95f, 0.18f, 0.18f);
            glow.effectDistance = new Vector2(0f, 0f);
            return screen;
        }

        private static Color TerminalGreen()
        {
            return new Color(0.47f, 1f, 0.31f, 1f);
        }

        private static void ApplyTerminalText(TextMeshProUGUI label, bool speed)
        {
            if (label == null) return;
            label.characterSpacing = speed ? 2f : 1.5f;
            label.fontStyle = speed ? FontStyles.Bold : FontStyles.Normal;
            label.outlineColor = new Color(0f, 0.32f, 0.05f, 0.85f);
            label.outlineWidth = 0.14f;
            label.enableWordWrapping = true;
        }

        private void UpdateTerminalCursor()
        {
            if (statusCursor == null) return;
            statusCursor.gameObject.SetActive(Mathf.Repeat(Time.unscaledTime, 1.05f) < 0.58f);
        }

        private void BuildControls()
        {
            AppSettings theme = services.Settings;
            focusGroup = root.gameObject.AddComponent<AccessibleFocusGroup>();
            focusGroup.Cancelled += ReturnToMenu;
            focusGroup.MoveInterceptor = InterceptCabMovement;

            CabControlBinding[] bindings = services.CabRide.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding.action == CabControlAction.Radio || binding.action == CabControlAction.Throttle) continue;
                // The whole rectangle remains a large accessible hit target, while the
                // visible control is a compact instrument fitted into the photographed panel.
                Color idle = new Color(0.035f, 0.045f, 0.050f, 0.68f);
                Color selected = WithAlpha(theme.SelectedColor, 0.82f);
                if (binding.action == CabControlAction.Brake)
                {
                    idle = new Color(0.16f, 0.055f, 0.045f, 0.72f);
                    selected = WithAlpha(theme.BrakeColor, 0.86f);
                }
                AccessibleButton button = UiFactory.Button(binding.action.ToString(), cabInterior, focusGroup,
                    InitialControlLabel(binding, binding.artwork != null), idle, selected,
                    () => ActivateControl(binding.action), theme.CaptionFontSize);
                Vector2 half = binding.normalizedSize * 0.5f;
                UiFactory.SetRect(button.RectTransform, binding.normalizedCenter - half, binding.normalizedCenter + half,
                    Vector2.zero, Vector2.zero);
                button.ConfigurePersistentPress(binding.action != CabControlAction.Throttle, 5f);
                button.SetAccessibleName(binding.shortcut == Key.None
                    ? binding.label
                    : binding.label + ", клавиша " + binding.shortcut);
                Shadow shadow = button.GetComponent<Shadow>();
                if (shadow != null) shadow.enabled = false;

                Image statePlate = UiFactory.Image("StatePlate", button.transform, UiFactory.RoundedSprite(), idle, false);
                statePlate.type = Image.Type.Sliced;
                statePlate.raycastTarget = false;
                statePlate.transform.SetAsFirstSibling();
                bool lever = binding.action == CabControlAction.Throttle || binding.action == CabControlAction.Brake;
                bool hasArtwork = binding.artwork != null;
                Vector2 plateMin = hasArtwork ? new Vector2(0.19f, 0.10f) : new Vector2(0.08f, 0.05f);
                Vector2 plateMax = hasArtwork ? new Vector2(0.81f, 0.90f)
                    : lever ? new Vector2(0.92f, 0.47f) : new Vector2(0.92f, 0.80f);
                UiFactory.SetRect(statePlate.rectTransform, plateMin, plateMax, Vector2.zero, Vector2.zero);
                Outline bezel = statePlate.gameObject.AddComponent<Outline>();
                bezel.effectColor = new Color(0.68f, 0.72f, 0.70f, 0.55f);
                bezel.effectDistance = new Vector2(2f, -2f);
                bezel.useGraphicAlpha = false;
                Shadow plateShadow = statePlate.gameObject.AddComponent<Shadow>();
                plateShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                plateShadow.effectDistance = new Vector2(0f, -4f);
                Vector2 labelMin = hasArtwork ? new Vector2(0.06f, 0.04f) : plateMin;
                Vector2 labelMax = hasArtwork ? new Vector2(0.94f, lever ? 0.34f : 0.42f) : plateMax;
                UiFactory.SetRect(button.Label.rectTransform, labelMin, labelMax,
                    new Vector2(8f, 5f), new Vector2(-8f, -5f));
                button.SetStateGraphic(statePlate);

                if (hasArtwork)
                {
                    Image artwork = UiFactory.Image("Artwork", button.transform, binding.artwork,
                        Color.white, true);
                    Vector2 artworkHalf = binding.artworkSize * 0.5f;
                    UiFactory.SetRect(artwork.rectTransform, binding.artworkCenter - artworkHalf,
                        binding.artworkCenter + artworkHalf, Vector2.zero, Vector2.zero);
                    artwork.raycastTarget = false;
                    artwork.transform.SetSiblingIndex(1);
                    controlArtwork[binding.action] = artwork;
                    button.Label.color = new Color(0.88f, 0.96f, 1f, 0.96f);
                    Shadow labelShadow = button.Label.gameObject.AddComponent<Shadow>();
                    labelShadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
                    labelShadow.effectDistance = new Vector2(1.5f, -1.5f);
                }
                controls[binding.action] = button;
            }

            BuildThrottleSlider();

            AccessibleButton brakeButton = controls[CabControlAction.Brake];
            brakeButton.PointerPressed += BeginBrake;
            brakeButton.PointerReleased += EndBrake;
            brakeButton.PointerDragged += SetBrakeFromPointer;
            Image brakeTrack = UiFactory.Image("BrakeTrack", brakeButton.transform, UiFactory.RoundedSprite(),
                WithAlpha(theme.PanelColor, 0.86f), false);
            brakeTrack.type = Image.Type.Sliced;
            brakeTrack.raycastTarget = false;
            brakeTrack.rectTransform.anchorMin = new Vector2(0.84f, 0.16f);
            brakeTrack.rectTransform.anchorMax = new Vector2(0.84f, 0.84f);
            brakeTrack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            brakeTrack.rectTransform.sizeDelta = new Vector2(10f, 0f);
            brakeTrack.rectTransform.anchoredPosition = Vector2.zero;
            brakeGrip = UiFactory.Image("BrakeGrip", brakeButton.transform, UiFactory.RoundedSprite(), theme.BrakeColor, false);
            brakeGrip.type = Image.Type.Sliced;
            brakeGrip.raycastTarget = false;
            brakeGrip.rectTransform.anchorMin = brakeGrip.rectTransform.anchorMax = new Vector2(0.84f, 0.16f);
            brakeGrip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            brakeGrip.rectTransform.sizeDelta = new Vector2(32f, 32f);
            brakeGrip.rectTransform.anchoredPosition = Vector2.zero;

        }

        private void BuildThrottleSlider()
        {
            AppSettings theme = services.Settings;
            AccessibleButton slider = UiFactory.Button("Throttle", stage, focusGroup, string.Empty,
                Color.clear, Color.clear, () => { }, theme.CaptionFontSize);
            Vector2 sliderMin = new Vector2(0.365f, 0.12f);
            Vector2 sliderMax = new Vector2(0.895f, 0.25f);
            if (TryGetControlRect(CabControlAction.Throttle, out Vector2 configuredSliderMin, out Vector2 configuredSliderMax))
            {
                sliderMin = configuredSliderMin;
                sliderMax = configuredSliderMax;
            }
            UiFactory.SetRect(slider.RectTransform, sliderMin, sliderMax, Vector2.zero, Vector2.zero);
            slider.ConfigurePersistentPress(false, 0f);
            slider.SetPressScale(1f);
            slider.SetAccessibleName("Тяга 0 процентов. Проведите пальцем вдоль нижнего ползунка.");
            slider.PointerPressed += SetThrottleFromPointer;
            slider.PointerDragged += SetThrottleFromPointer;
            slider.PointerPressed += _ => SetThrottleGripPressed(true);
            slider.PointerReleased += _ => SetThrottleGripPressed(false);
            slider.Label.alignment = TextAlignmentOptions.Top;
            slider.Label.gameObject.SetActive(true);
            UiFactory.SetRect(slider.Label.rectTransform, new Vector2(0.34f, 0.63f), new Vector2(0.66f, 0.98f),
                Vector2.zero, Vector2.zero);
            controls[CabControlAction.Throttle] = slider;

            Sprite trackSprite = services.CabScenery != null ? services.CabScenery.ThrottleSliderTrack : null;
            Image track = UiFactory.Image("ThrottleSliderTrack", slider.transform,
                trackSprite != null ? trackSprite : UiFactory.RoundedSprite(), Color.white, false);
            track.type = trackSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            track.raycastTarget = false;
            UiFactory.SetRect(track.rectTransform, new Vector2(0.035f, 0.02f), new Vector2(0.965f, 0.73f),
                Vector2.zero, Vector2.zero);
            RectTransform groove = UiFactory.Panel("ThrottleGroove", slider.transform,
                new Color(0.015f, 0.045f, 0.055f, 0.84f), UiFactory.RoundedSprite());
            UiFactory.SetRect(groove, new Vector2(0.095f, 0.25f), new Vector2(0.905f, 0.48f), Vector2.zero, Vector2.zero);
            groove.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI zero = UiFactory.Label("ThrottleZero", slider.transform, "0", theme.CaptionFontSize,
                theme.TextColor, TextAlignmentOptions.MidlineLeft, UiFontRole.Control);
            UiFactory.SetRect(zero.rectTransform, new Vector2(0.055f, 0.04f), new Vector2(0.13f, 0.42f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI full = UiFactory.Label("ThrottleFull", slider.transform, "100", theme.CaptionFontSize,
                theme.TextColor, TextAlignmentOptions.MidlineRight, UiFontRole.Control);
            UiFactory.SetRect(full.rectTransform, new Vector2(0.87f, 0.04f), new Vector2(0.945f, 0.42f), Vector2.zero, Vector2.zero);

            Sprite handleSprite = services.CabScenery != null ? services.CabScenery.ThrottleSliderHandle : null;
            throttleGrip = UiFactory.Image("ThrottleSliderHandle", slider.transform,
                handleSprite != null ? handleSprite : UiFactory.RoundedSprite(),
                handleSprite != null ? Color.white : theme.AccentColor, true);
            throttleGrip.raycastTarget = false;
            throttleGrip.rectTransform.anchorMin = throttleGrip.rectTransform.anchorMax = new Vector2(0.11f, 0.35f);
            throttleGrip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            throttleGrip.rectTransform.sizeDelta = new Vector2(164f, 150f);
            throttleGrip.rectTransform.anchoredPosition = Vector2.zero;
            throttleGrip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            RectTransform gripCore = UiFactory.Panel("GripCore", throttleGrip.transform,
                new Color(0.055f, 0.075f, 0.08f, 0.94f), UiFactory.RoundedSprite());
            UiFactory.SetRect(gripCore, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.66f), Vector2.zero, Vector2.zero);
            gripCore.GetComponent<Image>().raycastTarget = false;
            RectTransform gripMark = UiFactory.Panel("GripMark", gripCore,
                new Color(1f, 0.62f, 0.08f, 0.96f), UiFactory.RoundedSprite());
            UiFactory.SetRect(gripMark, new Vector2(0.46f, 0.08f), new Vector2(0.54f, 0.92f), Vector2.zero, Vector2.zero);
            gripMark.GetComponent<Image>().raycastTarget = false;
        }

        private void SetThrottleGripPressed(bool value)
        {
            throttleGripPressed = value;
            UpdateThrottleGripPressVisual();
        }

        private void BuildRadioPlayer()
        {
            AppSettings theme = services.Settings;
            // The radio lives on the stable stage, not inside the swaying cabin layer.
            // Its surface is built from controls only, so no obsolete player baked into an image can show through.
            radioPlayer = UiFactory.Panel("RetroRadioPlayer", stage,
                new Color(0.025f, 0.070f, 0.088f, 0.985f), UiFactory.RoundedSprite());
            Image playerImage = radioPlayer.GetComponent<Image>();
            playerImage.preserveAspect = false;
            UiFactory.StyleSurface(radioPlayer, true);
            radioNightGlow = UiFactory.Image("RadioNightGlow", radioPlayer, UiFactory.RoundedSprite(), new Color(0.40f, 0.84f, 0.96f, 0.06f), false);
            radioNightGlow.type = Image.Type.Sliced;
            radioNightGlow.raycastTarget = false;
            UiFactory.SetRect(radioNightGlow.rectTransform, new Vector2(-0.025f, -0.035f), new Vector2(1.025f, 1.035f), Vector2.zero, Vector2.zero);
            radioNightGlow.transform.SetAsFirstSibling();

            RectTransform display = UiFactory.Panel("RadioDisplaySurface", radioPlayer, new Color(0.008f, 0.035f, 0.045f, 0.90f), UiFactory.RoundedSprite());
            UiFactory.SetRect(display, new Vector2(0.075f, 0.545f), new Vector2(0.925f, 0.895f), Vector2.zero, Vector2.zero);
            display.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI heading = UiFactory.Label("RadioHeading", display, "РАДИО", theme.CaptionFontSize,
                new Color(0.76f, 0.91f, 0.96f, 1f), TextAlignmentOptions.MidlineLeft, UiFontRole.Control);
            UiFactory.SetRect(heading.rectTransform, new Vector2(0.055f, 0.73f), new Vector2(0.46f, 0.95f), Vector2.zero, Vector2.zero);
            radioStateLamp = UiFactory.Image("RadioStateLamp", display, UiFactory.RoundedSprite(), theme.MutedTextColor, false);
            radioStateLamp.rectTransform.anchorMin = radioStateLamp.rectTransform.anchorMax = new Vector2(0.59f, 0.84f);
            radioStateLamp.rectTransform.sizeDelta = new Vector2(14f, 14f);
            radioStateLamp.raycastTarget = false;
            radioState = UiFactory.Label("RadioState", display, "ВЫКЛ.", theme.CaptionFontSize,
                theme.MutedTextColor, TextAlignmentOptions.MidlineRight, UiFontRole.Control);
            UiFactory.SetRect(radioState.rectTransform, new Vector2(0.63f, 0.73f), new Vector2(0.945f, 0.95f), Vector2.zero, Vector2.zero);

            radioTrackTitle = UiFactory.Label("TrackTitle", display, "Выберите трек", theme.CaptionFontSize,
                theme.TextColor, TextAlignmentOptions.MidlineLeft, UiFontRole.Body);
            radioTrackTitle.enableWordWrapping = false;
            radioTrackTitle.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.SetRect(radioTrackTitle.rectTransform, new Vector2(0.055f, 0.42f), new Vector2(0.945f, 0.72f), Vector2.zero, Vector2.zero);
            radioTime = UiFactory.Label("TrackTime", display, "00:00 — 00:00", theme.CaptionFontSize,
                new Color(0.66f, 0.82f, 0.88f, 1f), TextAlignmentOptions.MidlineRight, UiFontRole.Body);
            UiFactory.SetRect(radioTime.rectTransform, new Vector2(0.52f, 0.14f), new Vector2(0.945f, 0.40f), Vector2.zero, Vector2.zero);

            RectTransform progressTrack = UiFactory.Panel("RadioProgressTrack", display, new Color(0.18f, 0.31f, 0.35f, 0.92f), UiFactory.RoundedSprite());
            UiFactory.SetRect(progressTrack, new Vector2(0.055f, 0.095f), new Vector2(0.945f, 0.16f), Vector2.zero, Vector2.zero);
            progressTrack.GetComponent<Image>().raycastTarget = false;
            radioProgressFill = UiFactory.Image("RadioProgressFill", progressTrack, UiFactory.RoundedSprite(), theme.PrimaryColor, false);
            radioProgressFill.type = Image.Type.Sliced;
            radioProgressFill.raycastTarget = false;
            UiFactory.SetRect(radioProgressFill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            for (int i = 0; i < 5; i++)
            {
                RectTransform bar = UiFactory.Panel("Equalizer_" + i, display, theme.SelectedColor, UiFactory.RoundedSprite());
                bar.anchorMin = bar.anchorMax = new Vector2(0.075f + i * 0.055f, 0.25f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(9f, 16f);
                bar.GetComponent<Image>().raycastTarget = false;
                radioEqualizerBars.Add(bar);
            }

            radioPowerButton = RadioButton("RadioPower", radioPlayer, string.Empty, new Vector2(0.37f, 0.245f), new Vector2(0.63f, 0.54f), ToggleRadio, "Включить радио");
            radioPlayGlyph = CreateRadioGlyph(radioPowerButton, "play");
            radioPauseGlyph = CreateRadioGlyph(radioPowerButton, "pause");
            AccessibleButton previous = RadioButton("RadioPrevious", radioPlayer, string.Empty, new Vector2(0.08f, 0.245f), new Vector2(0.34f, 0.54f), PreviousRadioTrack, "Включить предыдущий трек");
            CreateRadioGlyph(previous, "previous");
            AccessibleButton next = RadioButton("RadioNext", radioPlayer, string.Empty, new Vector2(0.66f, 0.245f), new Vector2(0.92f, 0.54f), NextRadioTrack, "Включить следующий трек");
            CreateRadioGlyph(next, "next");
            radioPlaylistButton = RadioButton("RadioPlaylist", radioPlayer, string.Empty, new Vector2(0.08f, 0.035f), new Vector2(0.58f, 0.225f), TogglePlaylist, "Открыть список треков");
            radioPlaylistGlyph = CreateRadioGlyph(radioPlaylistButton, "playlist");
            radioVolumeButton = RadioButton("RadioVolume", radioPlayer, "60%", new Vector2(0.61f, 0.035f), new Vector2(0.745f, 0.225f), ToggleRadioMusicVolume, "Громкость песен и радио 60 процентов", true);
            radioVolumeButton.Label.alignment = TextAlignmentOptions.Center;
            radioVolumeButton.Label.margin = Vector4.zero;
            onlineRadioButton = RadioButton("OnlineRadio", radioPlayer, string.Empty, new Vector2(0.765f, 0.035f), new Vector2(0.92f, 0.225f), ToggleOnlineRadio, "Включить Детское онлайн-радио");
            onlineRadioGlyph = CreateRadioGlyph(onlineRadioButton, "online");

            radioPlaylist = UiFactory.Panel("RadioPlaylist", stage, new Color(0.025f, 0.070f, 0.088f, 0.99f), UiFactory.RoundedSprite());
            UiFactory.StyleSurface(radioPlaylist, true);
            BuildPlaylistSurface();
            radioPlaylist.gameObject.SetActive(false);
            BuildPlaylistEntries();
            UpdateRadioPlayerLayout(true);
            UpdateRadioPlayer();
        }

        private void UpdateRadioPlayerLayout(bool force = false)
        {
            if (radioPlayer == null || root == null) return;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (!force && screenSize == lastRadioLayoutScreenSize && safeArea == lastRadioLayoutSafeArea) return;
            lastRadioLayoutScreenSize = screenSize;
            lastRadioLayoutSafeArea = safeArea;
            float aspect = safeArea.height > 1f ? safeArea.width / safeArea.height : 1.5f;
            Vector2 visibleMargins = CabStageVisibleMargins(aspect);
            // Keep the player visually in the lower-left corner, but leave enough room for its
            // rounded glow and shadow after the 3:2 cab stage is vertically cropped on 16:9.
            Vector2 playerMin = visibleMargins + new Vector2(0.008f, 0.035f);
            Vector2 playerMax = playerMin + new Vector2(0.312f, 0.355f);
            if (TryGetControlRect(CabControlAction.Radio, out Vector2 configuredPlayerMin, out Vector2 configuredPlayerMax))
            {
                playerMin = configuredPlayerMin;
                playerMax = configuredPlayerMax;
            }
            Vector2 playerSize = playerMax - playerMin;
            UiFactory.SetRect(radioPlayer, playerMin, playerMax, Vector2.zero, Vector2.zero);
            if (radioPlaylist != null)
            {
                Vector2 playlistMin = new Vector2(playerMin.x, playerMin.y + playerSize.y + 0.012f);
                UiFactory.SetRect(radioPlaylist, playlistMin, playlistMin + new Vector2(playerSize.x, 0.36f), Vector2.zero, Vector2.zero);
            }
        }

        private bool TryGetControlRect(CabControlAction action, out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero;
            max = Vector2.zero;
            if (services == null || services.CabRide == null) return false;
            CabControlBinding[] bindings = services.CabRide.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding == null || binding.action != action) continue;
                Vector2 half = binding.normalizedSize * 0.5f;
                min = binding.normalizedCenter - half;
                max = binding.normalizedCenter + half;
                return true;
            }
            return false;
        }

        public static Vector2 CabStageVisibleMargins(float parentAspect)
        {
            const float stageAspect = 1.5f;
            float aspect = Mathf.Clamp(parentAspect, 0.75f, 3f);
            float left = aspect < stageAspect ? (1f - aspect / stageAspect) * 0.5f : 0f;
            float bottom = aspect > stageAspect ? (1f - stageAspect / aspect) * 0.5f : 0f;
            return new Vector2(left, bottom);
        }

        private void BuildDispatcherButton()
        {
            AppSettings theme = services.Settings;
            dispatcherButton = UiFactory.Button("DispatcherAcknowledge", cabInterior, focusGroup, "●",
                new Color(0.42f, 0.035f, 0.045f, 0.96f), new Color(1f, 0.30f, 0.22f, 1f),
                AcknowledgeDispatcher, theme.ControlFontSize);
            UiFactory.SetRect(dispatcherButton.RectTransform, new Vector2(0.752f, 0.785f), new Vector2(0.865f, 0.905f),
                Vector2.zero, Vector2.zero);
            dispatcherButton.ConfigurePersistentPress(false, 8f);
            dispatcherButton.SetAccessibleName("Красная кнопка диспетчера: подтвердить готовность");
            dispatcherLamp = UiFactory.Image("DispatcherLamp", dispatcherButton.transform, UiFactory.RoundedSprite(),
                new Color(1f, 0.24f, 0.18f, 0.94f), false);
            dispatcherLamp.type = Image.Type.Sliced;
            dispatcherLamp.raycastTarget = false;
            UiFactory.SetRect(dispatcherLamp.rectTransform, new Vector2(0.19f, 0.19f), new Vector2(0.81f, 0.81f),
                Vector2.zero, Vector2.zero);
            dispatcherLamp.transform.SetAsFirstSibling();
        }

        private void PromptDeparture()
        {
            SetStatus("Диспетчер: состав №" + trainNumber + ". Подтвердите готовность красной кнопкой.");
        }

        private void AcknowledgeDispatcher()
        {
            services.Audio.Play(SoundCue.Toggle);
            if (!departureAuthorized)
            {
                departureAuthorized = true;
                nextVigilanceAt = Time.unscaledTime + 60f;
                SetStatus("Диспетчер: движение разрешено. Можно набрать тягу.");
                dispatcherButton.SetAccessibleName("Красная кнопка диспетчера: ожидание проверки бдительности");
                return;
            }

            if (TryReleaseAutomaticStop(departureAuthorized, ref automaticStop))
            {
                nextVigilanceAt = Time.unscaledTime + 120f;
                SetStatus("Автоматическая остановка снята. Можно снова набрать тягу.");
                dispatcherButton.SetAccessibleName("Красная кнопка диспетчера: ожидание проверки бдительности");
                services.Audio.PlayDispatcher(DispatcherVoiceCue.VigilancePassed);
                return;
            }

            if (!vigilanceAlarm) return;
            vigilanceAlarm = false;
            nextVigilanceAt = Time.unscaledTime + 120f;
            SetStatus("Бдительность подтверждена. Следующая проверка через две минуты.");
            services.Audio.PlayDispatcher(DispatcherVoiceCue.VigilancePassed);
        }

        public static bool TryReleaseAutomaticStop(bool departureAuthorized, ref bool automaticStop)
        {
            if (!departureAuthorized || !automaticStop) return false;
            automaticStop = false;
            return true;
        }

        private void UpdateVigilance()
        {
            if (!departureAuthorized || automaticStop) return;
            float now = Time.unscaledTime;
            if (!vigilanceAlarm && now >= nextVigilanceAt)
            {
                vigilanceAlarm = true;
                vigilanceDeadline = now + 20f;
                nextVigilanceBeep = now;
                SetStatus("Проверка бдительности: нажмите мигающую красную кнопку за 20 секунд.");
                services.Audio.PlayDispatcher(DispatcherVoiceCue.VigilanceCheck);
            }
            if (!vigilanceAlarm) return;
            if (now >= nextVigilanceBeep)
            {
                services.Audio.Play(SoundCue.Bell);
                nextVigilanceBeep = now + 2.2f;
            }
            if (now < vigilanceDeadline) return;

            vigilanceAlarm = false;
            automaticStop = true;
            motion.SetThrottle(0f);
            UpdateThrottleVisual();
            services.Audio.Play(SoundCue.Brake);
            SetStatus("Нет подтверждения: поезд автоматически останавливается.");
            dispatcherButton.SetAccessibleName("Красная кнопка диспетчера: поезд остановлен автоматически");
        }

        private void UpdateDispatcherVisual()
        {
            if (dispatcherButton == null || dispatcherLamp == null) return;
            float now = Time.unscaledTime;
            float alarmPulse = vigilanceAlarm ? 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(now * 10f)) : 1f;
            Color lampColor = automaticStop ? new Color(0.42f, 0.05f, 0.05f, 0.55f) :
                departureAuthorized ? new Color(0.72f, 0.08f, 0.07f, 0.64f) : new Color(1f, 0.24f, 0.18f, 0.86f);
            lampColor.a *= alarmPulse;
            dispatcherLamp.color = lampColor;
            dispatcherButton.RectTransform.localScale = vigilanceAlarm
                ? Vector3.one * (1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(now * 10f)))
                : Vector3.one;
        }

        private AccessibleButton RadioButton(string name, Transform parent, string label, Vector2 min, Vector2 max,
            System.Action action, string accessibleName, bool showLabel = false)
        {
            AppSettings theme = services.Settings;
            AccessibleButton button = UiFactory.Button(name, parent, focusGroup, label, new Color(0.035f, 0.10f, 0.13f, 0.86f),
                theme.SelectedColor, action, theme.CaptionFontSize);
            UiFactory.SetRect(button.RectTransform, min, max, Vector2.zero, Vector2.zero);
            button.SetAccessibleName(accessibleName);
            button.Label.gameObject.SetActive(showLabel);
            if (showLabel)
            {
                button.Label.alignment = TextAlignmentOptions.MidlineLeft;
                button.Label.margin = new Vector4(22f, 8f, 18f, 8f);
            }
            return button;
        }

        private RectTransform CreateRadioGlyph(AccessibleButton button, string kind)
        {
            RectTransform root = new GameObject("Glyph_" + kind, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(button.transform, false);
            UiFactory.Stretch(root);
            Image icon = UiFactory.Image("Icon", root, CreateRadioIconSprite(kind), services.Settings.TextColor, true);
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bool compact = kind == "playlist" || kind == "online";
            icon.rectTransform.sizeDelta = new Vector2(compact ? 58f : 72f, compact ? 58f : 72f);
            icon.rectTransform.anchoredPosition = kind == "play" ? new Vector2(3f, 0f) : Vector2.zero;
            return root;
        }

        private static void GlyphStroke(Transform parent, Color color, Vector2 center, float width, float height, float angle)
        {
            RectTransform stroke = UiFactory.Panel("Stroke", parent, color, UiFactory.RoundedSprite());
            stroke.anchorMin = stroke.anchorMax = center;
            stroke.pivot = new Vector2(0.5f, 0.5f);
            stroke.sizeDelta = new Vector2(width, height);
            stroke.localRotation = Quaternion.Euler(0f, 0f, angle);
            stroke.GetComponent<Image>().raycastTarget = false;
        }

        private static Sprite CreateRadioIconSprite(string kind)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Radio icon " + kind,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            Color32 ink = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled = kind switch
                    {
                        "pause" => (x >= 29 && x <= 39 || x >= 57 && x <= 67) && y >= 25 && y <= 71,
                        "previous" => (x >= 18 && x <= 25 && y >= 24 && y <= 72) ||
                                      LeftTriangle(x, y, 27, 53, 48, 22) || LeftTriangle(x, y, 46, 72, 48, 22),
                        "next" => (x >= 71 && x <= 78 && y >= 24 && y <= 72) ||
                                  RightTriangle(x, y, 24, 50, 48, 22) || RightTriangle(x, y, 43, 69, 48, 22),
                        "playlist" => PlaylistMark(x, y),
                        "online" => OnlineRadioMark(x, y),
                        _ => RightTriangle(x, y, 27, 70, 48, 30)
                    };
                    pixels[y * size + x] = filled ? ink : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Radio icon " + kind;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool RightTriangle(int x, int y, int left, int right, int centerY, int halfHeight)
        {
            if (x < left || x > right) return false;
            float width = Mathf.Max(1f, right - left);
            return Mathf.Abs(y - centerY) <= halfHeight * (1f - (x - left) / width);
        }

        private static bool LeftTriangle(int x, int y, int left, int right, int centerY, int halfHeight)
        {
            if (x < left || x > right) return false;
            float width = Mathf.Max(1f, right - left);
            return Mathf.Abs(y - centerY) <= halfHeight * ((x - left) / width);
        }

        private static bool PlaylistMark(int x, int y)
        {
            for (int row = 0; row < 3; row++)
            {
                int centerY = 29 + row * 19;
                if (x >= 19 && x <= 28 && y >= centerY - 4 && y <= centerY + 4) return true;
                if (x >= 37 && x <= 77 && y >= centerY - 3 && y <= centerY + 3) return true;
            }
            return false;
        }

        private static bool OnlineRadioMark(int x, int y)
        {
            int dx = x - 48;
            int dy = y - 34;
            int distanceSquared = dx * dx + dy * dy;
            bool lamp = distanceSquared <= 7 * 7;
            bool mast = x >= 45 && x <= 51 && y >= 34 && y <= 67;
            if (y < 34) return lamp || mast;
            float distance = Mathf.Sqrt(distanceSquared);
            bool innerWave = Mathf.Abs(distance - 22f) <= 2.8f;
            bool outerWave = Mathf.Abs(distance - 34f) <= 2.8f;
            return lamp || mast || innerWave || outerWave;
        }

        private void BuildPlaylistEntries()
        {
            AudioClip[] tracks = services.AudioCatalog != null ? services.AudioCatalog.CabRadioPlaylist : System.Array.Empty<AudioClip>();
            int count = tracks.Length;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                string title = tracks[i] != null ? tracks[i].name : "Пустой слот";
                AccessibleButton entry = RadioButton("Track_" + i, radioPlaylistContent, (i + 1) + ". " + title,
                    Vector2.zero, Vector2.one, () => SelectRadioTrack(index),
                    "Включить трек " + (i + 1) + ": " + title, true);
                entry.RectTransform.anchorMin = new Vector2(0f, 1f);
                entry.RectTransform.anchorMax = new Vector2(1f, 1f);
                entry.RectTransform.pivot = new Vector2(0.5f, 1f);
                entry.RectTransform.anchoredPosition = new Vector2(-7f, -8f - i * 68f);
                entry.RectTransform.sizeDelta = new Vector2(-22f, 60f);
                entry.ConfigurePersistentPress(false, 0f);
                entry.Label.enableWordWrapping = false;
                entry.Label.overflowMode = TextOverflowModes.Ellipsis;
                entry.Label.gameObject.SetActive(false);
                TextMeshProUGUI rowLabel = UiFactory.Label("TrackLabel", entry.transform, (i + 1) + ". " + title,
                    services.Settings.CaptionFontSize, services.Settings.TextColor,
                    TextAlignmentOptions.MidlineLeft, UiFontRole.Body);
                rowLabel.enableWordWrapping = false;
                rowLabel.overflowMode = TextOverflowModes.Ellipsis;
                UiFactory.Stretch(rowLabel.rectTransform, 22f, 7f, 18f, 7f);
                radioTrackButtons.Add(entry);
                radioTrackLabels.Add(rowLabel);
            }
            if (radioPlaylistContent != null)
                radioPlaylistContent.sizeDelta = new Vector2(0f, Mathf.Max(220f, 16f + count * 68f));
            if (count == 0)
            {
                TextMeshProUGUI empty = UiFactory.Label("EmptyPlaylist", radioPlaylistViewport, "Список пуст. Треки добавляются в Audio Catalog.", services.Settings.CaptionFontSize,
                    new Color(0.72f, 0.84f, 0.78f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
                UiFactory.SetRect(empty.rectTransform, new Vector2(0.07f, 0.2f), new Vector2(0.93f, 0.82f), Vector2.zero, Vector2.zero);
            }
        }

        private void BuildPlaylistSurface()
        {
            AppSettings theme = services.Settings;
            TextMeshProUGUI title = UiFactory.Label("PlaylistHeading", radioPlaylist, "ТРЕКИ",
                theme.CaptionFontSize, new Color(0.78f, 0.93f, 0.98f, 1f),
                TextAlignmentOptions.MidlineLeft, UiFontRole.Control);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.78f, 0.98f), Vector2.zero, Vector2.zero);

            radioPlaylistViewport = UiFactory.Panel("PlaylistViewport", radioPlaylist,
                new Color(0.008f, 0.032f, 0.042f, 0.94f), UiFactory.RoundedSprite());
            UiFactory.SetRect(radioPlaylistViewport, new Vector2(0.04f, 0.055f), new Vector2(0.91f, 0.85f),
                Vector2.zero, Vector2.zero);
            radioPlaylistViewport.gameObject.AddComponent<RectMask2D>();

            radioPlaylistContent = UiFactory.Panel("PlaylistContent", radioPlaylistViewport, Color.clear);
            radioPlaylistContent.anchorMin = new Vector2(0f, 1f);
            radioPlaylistContent.anchorMax = new Vector2(1f, 1f);
            radioPlaylistContent.pivot = new Vector2(0.5f, 1f);
            radioPlaylistContent.anchoredPosition = Vector2.zero;
            radioPlaylistContent.sizeDelta = new Vector2(0f, 220f);
            radioPlaylistContent.GetComponent<Image>().raycastTarget = false;

            RectTransform scrollbarRect = UiFactory.Panel("PlaylistScrollbar", radioPlaylist,
                new Color(0.14f, 0.28f, 0.32f, 0.95f), UiFactory.RoundedSprite());
            UiFactory.SetRect(scrollbarRect, new Vector2(0.925f, 0.07f), new Vector2(0.972f, 0.84f), Vector2.zero, Vector2.zero);
            RectTransform slidingArea = UiFactory.Panel("SlidingArea", scrollbarRect, Color.clear);
            UiFactory.Stretch(slidingArea, 5f, 5f, 5f, 5f);
            RectTransform handle = UiFactory.Panel("Handle", slidingArea, theme.PrimaryColor, UiFactory.RoundedSprite());
            UiFactory.SetRect(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            radioPlaylistScrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            radioPlaylistScrollbar.handleRect = handle;
            radioPlaylistScrollbar.targetGraphic = handle.GetComponent<Image>();
            radioPlaylistScrollbar.direction = Scrollbar.Direction.BottomToTop;
            radioPlaylistScrollbar.numberOfSteps = 0;

            radioPlaylistScroll = radioPlaylist.gameObject.AddComponent<ScrollRect>();
            radioPlaylistScroll.viewport = radioPlaylistViewport;
            radioPlaylistScroll.content = radioPlaylistContent;
            radioPlaylistScroll.horizontal = false;
            radioPlaylistScroll.vertical = true;
            radioPlaylistScroll.movementType = ScrollRect.MovementType.Clamped;
            radioPlaylistScroll.inertia = true;
            radioPlaylistScroll.decelerationRate = 0.12f;
            radioPlaylistScroll.scrollSensitivity = 28f;
            radioPlaylistScroll.verticalScrollbar = radioPlaylistScrollbar;
            radioPlaylistScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            radioPlaylistScroll.verticalScrollbarSpacing = 8f;
        }

        private void ToggleRadio()
        {
            radio = !radio;
            services.Audio.SetRadio(radio);
            UpdateRadioPlayer();
        }

        private void ToggleOnlineRadio()
        {
            bool enable = !services.Audio.OnlineRadioIsActive;
            services.Audio.SetOnlineRadio(enable);
            radio = false;
            playlistOpen = false;
            if (radioPlaylist != null) radioPlaylist.gameObject.SetActive(false);
            SetStatus(enable ? "Подключаем Детское онлайн-радио" : "Онлайн-радио выключено");
            UpdateRadioPlayer();
        }

        private void ToggleRadioMusicVolume()
        {
            services.Audio.ToggleRadioMusicVolume();
            SetStatus(services.Audio.RadioMusicVolumeMultiplier > 0.7f
                ? "Громкость радио: 90%"
                : "Громкость радио: 60%");
            UpdateRadioPlayer();
        }

        private void NextRadioTrack()
        {
            radio = true;
            services.Audio.NextRadioTrack();
            UpdateRadioPlayer();
        }

        private void PreviousRadioTrack()
        {
            radio = true;
            services.Audio.PreviousRadioTrack();
            UpdateRadioPlayer();
        }

        private void SelectRadioTrack(int index)
        {
            radio = true;
            services.Audio.SelectRadioTrack(index);
            playlistOpen = false;
            if (radioPlaylist != null) radioPlaylist.gameObject.SetActive(false);
            UpdateRadioPlayer();
        }

        private void TogglePlaylist()
        {
            playlistOpen = !playlistOpen;
            if (radioPlaylist != null) radioPlaylist.gameObject.SetActive(playlistOpen);
            if (radioPlaylistButton != null)
            {
                radioPlaylistButton.SetSelected(playlistOpen);
                radioPlaylistButton.SetAccessibleName(playlistOpen ? "Скрыть список треков" : "Открыть список треков");
            }
        }

        private void UpdateRadioPlayer()
        {
            if (radioTrackTitle == null) return;
            bool localPlaying = services.Audio.RadioIsPlaying;
            bool onlinePlaying = services.Audio.OnlineRadioIsPlaying;
            bool onlineConnecting = services.Audio.OnlineRadioIsConnecting;
            bool onlineActive = services.Audio.OnlineRadioIsActive;
            bool playing = localPlaying || onlinePlaying;
            radioTrackTitle.text = onlineActive ? services.Audio.OnlineRadioName :
                localPlaying ? services.Audio.RadioTrackName :
                services.Audio.RadioTrackCount > 0 ? "Выберите трек" : "Нет треков";
            float time = services.Audio.RadioTrackTime;
            float duration = services.Audio.RadioTrackLength;
            radioTime.text = onlineActive
                ? services.Audio.OnlineRadioStatus
                : string.Format("{0:00}:{1:00} — {2:00}:{3:00}", Mathf.FloorToInt(time / 60f), Mathf.FloorToInt(time % 60f), Mathf.FloorToInt(duration / 60f), Mathf.FloorToInt(duration % 60f));
            if (radioPlayGlyph != null) radioPlayGlyph.gameObject.SetActive(!localPlaying);
            if (radioPauseGlyph != null) radioPauseGlyph.gameObject.SetActive(localPlaying);
            if (radioState != null)
            {
                radioState.text = onlineConnecting ? "СЕТЬ…" : onlinePlaying ? "ОНЛАЙН" : onlineActive ? "ANDROID" : localPlaying ? "В ЭФИРЕ" : "ВЫКЛ.";
                radioState.color = playing || onlineActive ? services.Settings.SelectedColor : services.Settings.MutedTextColor;
            }
            if (radioStateLamp != null)
                radioStateLamp.color = playing || onlineActive ? services.Settings.SelectedColor : new Color(0.42f, 0.51f, 0.55f, 1f);
            if (radioProgressFill != null)
            {
                float progress = onlinePlaying ? 1f : duration > 0.01f ? Mathf.Clamp01(time / duration) : 0f;
                Vector2 max = radioProgressFill.rectTransform.anchorMax;
                max.x = progress;
                radioProgressFill.rectTransform.anchorMax = max;
            }
            if (radioPowerButton != null)
            {
                radioPowerButton.SetSelected(localPlaying);
                radioPowerButton.SetAccessibleName(localPlaying ? "Поставить локальное радио на паузу" : "Включить локальное радио");
            }
            if (onlineRadioButton != null)
            {
                onlineRadioButton.SetSelected(onlineActive);
                onlineRadioButton.SetInteractable(services.Audio.OnlineRadioAvailable);
                onlineRadioButton.SetAccessibleName(onlineActive
                    ? "Выключить Детское онлайн-радио"
                    : "Включить Детское онлайн-радио");
            }
            if (radioVolumeButton != null)
            {
                bool loud = services.Audio.RadioMusicVolumeMultiplier > 0.7f;
                radioVolumeButton.SetSelected(loud);
                radioVolumeButton.SetLabel(loud ? "90%" : "60%");
                radioVolumeButton.SetAccessibleName(loud
                    ? "Громкость песен и радио 90 процентов. Нажмите для 60 процентов"
                    : "Громкость песен и радио 60 процентов. Нажмите для 90 процентов");
            }
            if (radioPlaylistButton != null) radioPlaylistButton.SetSelected(playlistOpen);
            Color activeGlyph = services.Settings.TextOnBrightColor;
            Color idleGlyph = services.Settings.TextColor;
            SetRadioGlyphColor(radioPlayGlyph, localPlaying ? activeGlyph : idleGlyph);
            SetRadioGlyphColor(radioPauseGlyph, localPlaying ? activeGlyph : idleGlyph);
            SetRadioGlyphColor(radioPlaylistGlyph, playlistOpen ? activeGlyph : idleGlyph);
            SetRadioGlyphColor(onlineRadioGlyph, onlineActive ? activeGlyph : idleGlyph);
            for (int i = 0; i < radioTrackButtons.Count; i++)
            {
                bool selectedTrack = localPlaying && i == services.Audio.RadioTrackIndex;
                radioTrackButtons[i].SetSelected(selectedTrack);
                if (i < radioTrackLabels.Count)
                    radioTrackLabels[i].color = selectedTrack ? services.Settings.TextOnBrightColor : services.Settings.TextColor;
            }
            UpdateRadioEqualizer(playing);
            if (radioDisplay != null)
                radioDisplay.text = onlineActive
                    ? "Онлайн: " + services.Audio.OnlineRadioName
                    : localPlaying ? "Радио: " + services.Audio.RadioTrackName : "Радио выключено";
        }

        private static void SetRadioGlyphColor(RectTransform root, Color color)
        {
            if (root == null) return;
            Image icon = root.GetComponentInChildren<Image>();
            if (icon != null) icon.color = color;
        }

        private void UpdateRadioEqualizer(bool playing)
        {
            MotionLevel motionLevel = services.Preferences.motionLevel;
            float motionAmount = motionLevel == MotionLevel.Off ? 0f : motionLevel == MotionLevel.Reduced ? 0.35f : 1f;
            for (int i = 0; i < radioEqualizerBars.Count; i++)
            {
                RectTransform bar = radioEqualizerBars[i];
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (4.1f + i * 0.31f) + i * 1.37f);
                float height = playing ? Mathf.Lerp(12f, 38f, Mathf.Lerp(0.45f, wave, motionAmount)) : 7f;
                bar.sizeDelta = new Vector2(bar.sizeDelta.x, height);
                bar.GetComponent<Image>().color = playing ? services.Settings.SelectedColor : new Color(0.34f, 0.45f, 0.49f, 0.82f);
            }
        }

        private void BuildHeader()
        {
            AppSettings theme = services.Settings;
            AccessibleButton back = UiFactory.Button("Back", root, focusGroup, "← Назад",
                WithAlpha(theme.PanelColor, 0.96f), theme.PrimaryColor, ReturnToMenu, theme.StatusFontSize);
            UiFactory.SetRect(back.RectTransform, new Vector2(0.018f, 0.89f), new Vector2(0.16f, 0.985f), Vector2.zero, Vector2.zero);

            RectTransform routeChip = UiFactory.Panel("RouteChip", root, WithAlpha(theme.PanelColor, 0.94f), UiFactory.RoundedSprite());
            UiFactory.SetRect(routeChip, new Vector2(0.31f, 0.91f), new Vector2(0.69f, 0.985f), Vector2.zero, Vector2.zero);
            routeChip.GetComponent<Image>().raycastTarget = false;
            routeDisplay = UiFactory.Label("Route", routeChip, "Маршрут: " + world.CurrentSegmentName,
                theme.StatusFontSize, theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.Stretch(routeDisplay.rectTransform, 18f, 8f, 18f, 8f);
        }

        private bool InterceptCabMovement(Vector2 direction)
        {
            if (direction.y > 0.5f)
            {
                AdjustThrottle(services.CabRide.KeyboardThrottleStep);
                return true;
            }
            if (direction.y < -0.5f)
            {
                AdjustThrottle(-services.CabRide.KeyboardThrottleStep);
                return true;
            }
            return false;
        }

        private void ActivateControl(CabControlAction action)
        {
            switch (action)
            {
                case CabControlAction.Horn:
                    services.Audio.Play(SoundCue.Horn);
                    SetMomentaryGlow(CabControlAction.Horn);
                    SetStatus("Ту-ту! Гудок работает");
                    break;
                case CabControlAction.Headlights:
                    headlights = !headlights;
                    services.Audio.Play(SoundCue.Toggle);
                    UpdateToggleVisual(action, headlights);
                    SetStatus(headlights ? "Фары включены" : "Фары выключены");
                    break;
                case CabControlAction.CabinLight:
                    cabinLight = !cabinLight;
                    services.Audio.Play(SoundCue.Toggle);
                    UpdateToggleVisual(action, cabinLight);
                    SetStatus(cabinLight ? "Свет кабины включён" : "Свет кабины выключен");
                    break;
                case CabControlAction.Wipers:
                    wipers = !wipers;
                    services.Audio.Play(SoundCue.Wiper);
                    UpdateToggleVisual(action, wipers);
                    SetStatus(wipers ? "Дворники включены" : "Дворники выключены");
                    break;
                case CabControlAction.Bell:
                    services.Audio.Play(SoundCue.Bell);
                    SetMomentaryGlow(CabControlAction.Bell);
                    SetStatus("Дзынь! Станционный звонок");
                    break;
                case CabControlAction.Throttle:
                    AdjustThrottle(services.CabRide.KeyboardThrottleStep);
                    break;
                case CabControlAction.Brake:
                    brakePulseUntil = Time.unscaledTime + 0.65f;
                    services.Audio.Play(SoundCue.Brake);
                    SetStatus("Торможение");
                    break;
                case CabControlAction.Radio:
                    radio = !radio;
                    services.Audio.SetRadio(radio);
                    services.Audio.Play(SoundCue.Toggle);
                    UpdateToggleVisual(action, radio);
                    SetStatus(radio ? "Радио включено" : "Радио выключено");
                    break;
                case CabControlAction.Doors:
                    stationStop?.PressDoors();
                    services.Audio.Play(SoundCue.Toggle);
                    UpdateToggleVisual(action, stationStop != null && stationStop.DoorsAreOpen);
                    SetStatus(stationStop != null && stationStop.DoorsAreOpen ? "Двери открыты" : "Двери доступны на станции");
                    break;
                case CabControlAction.WindowHeater:
                    windowHeater = !windowHeater;
                    journey?.SetWindowHeater(windowHeater);
                    services.Audio.Play(SoundCue.Toggle);
                    UpdateToggleVisual(action, windowHeater);
                    SetStatus(windowHeater ? "Обогрев стекла включён" : "Обогрев стекла выключен");
                    break;
                case CabControlAction.DispatcherRadio:
                    services.Audio.Play(SoundCue.Toggle);
                    bool exchangeStarted = services.Audio.PlayDispatcherRadioExchange();
                    SetStatus(exchangeStarted ? "Связь с диспетчером" : "Связь: добавьте пару аудио в Audio Catalog");
                    break;
            }
            ControlActivated?.Invoke(action);
            bool handledInteraction = interactions != null && interactions.NotifyControlActivated(action);
            if (!handledInteraction) NotifyJourneyControl(action);
        }

        private void NotifyJourneyControl(CabControlAction action)
        {
            if (journey == null) return;
            if (action == CabControlAction.Horn) journey.NotifyAction(RouteEventAction.Horn);
            else if (action == CabControlAction.Bell) journey.NotifyAction(RouteEventAction.Bell);
            else if (action == CabControlAction.Headlights && headlights) journey.NotifyAction(RouteEventAction.Headlights);
            else if (action == CabControlAction.Wipers && wipers) journey.NotifyAction(RouteEventAction.Wipers);
        }

        private void AdjustThrottle(float delta)
        {
            if (!departureAuthorized)
            {
                services.Audio.Play(SoundCue.GentleError);
                PromptDeparture();
                return;
            }
            if (automaticStop)
            {
                SetStatus("Поезд остановлен автоматически. Вернитесь в меню и начните новую поездку.");
                return;
            }
            motion.AdjustThrottle(delta);
            services.Audio.Play(SoundCue.Switch);
            UpdateThrottleVisual();
            SetStatus(motion.Throttle01 <= 0.001f ? "Тяга выключена" : "Тяга изменена");
        }

        private void SetThrottleFromPointer(PointerEventData eventData)
        {
            if (!departureAuthorized)
            {
                services.Audio.Play(SoundCue.GentleError);
                PromptDeparture();
                return;
            }
            if (automaticStop) return;
            AccessibleButton throttleButton = controls[CabControlAction.Throttle];
            Camera camera = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(throttleButton.RectTransform,
                    eventData.position, camera, out Vector2 local)) return;
            Rect rect = throttleButton.RectTransform.rect;
            float value = ThrottleFromLocalX(rect, local.x, 74f);
            motion.SetThrottle(value);
            UpdateThrottleVisual();
        }

        public static float ThrottleFromLocalX(Rect rect, float localX, float edgePadding)
        {
            float padding = Mathf.Clamp(edgePadding, 0f, rect.width * 0.45f);
            return Mathf.Clamp01(Mathf.InverseLerp(rect.xMin + padding, rect.xMax - padding, localX));
        }

        private void BeginBrake(PointerEventData eventData)
        {
            pointerBrake = true;
            SetBrakeFromPointer(eventData);
            services.Audio.Play(SoundCue.Brake);
            SetStatus("Тормоз удерживается: " + Mathf.RoundToInt(pointerBrakeStrength * 100f) + "%");
        }

        private void EndBrake(PointerEventData eventData)
        {
            pointerBrake = false;
            pointerBrakeStrength = 0f;
        }

        private void SetBrakeFromPointer(PointerEventData eventData)
        {
            AccessibleButton brakeButton = controls[CabControlAction.Brake];
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(brakeButton.RectTransform,
                    eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Rect rect = brakeButton.RectTransform.rect;
            pointerBrakeStrength = Mathf.Clamp01(Mathf.InverseLerp(rect.yMin + 18f, rect.yMax - 18f, local.y));
            if (pointerBrake) pointerBrakeStrength = Mathf.Max(0.15f, pointerBrakeStrength);
            UpdateBrakeVisual(pointerBrakeStrength);
        }

        private void UpdateThrottleVisual()
        {
            if (!controls.TryGetValue(CabControlAction.Throttle, out AccessibleButton button)) return;
            int percent = Mathf.RoundToInt(motion.Throttle01 * 100f);
            button.SetLabel("ТЯГА " + percent + "%");
            button.SetAccessibleName("Тяга " + percent + " процентов");
            button.SetSelected(motion.Throttle01 > 0.001f);
            UpdateArtworkState(CabControlAction.Throttle, motion.Throttle01 > 0.001f, motion.Throttle01);
            if (throttleGrip != null)
            {
                Vector2 anchor = throttleGrip.rectTransform.anchorMin;
                anchor.x = Mathf.Lerp(0.11f, 0.89f, motion.Throttle01);
                throttleGrip.rectTransform.anchorMin = throttleGrip.rectTransform.anchorMax = anchor;
                UpdateThrottleGripPressVisual();
            }
        }

        private void UpdateThrottleGripPressVisual()
        {
            if (throttleGrip == null) return;
            float scale = throttleGripPressed ? 0.94f : 1f;
            throttleGrip.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateBrakeVisual(float brake01)
        {
            if (!controls.TryGetValue(CabControlAction.Brake, out AccessibleButton button)) return;
            bool active = brake01 > 0.001f;
            int percent = active ? Mathf.RoundToInt(Mathf.Clamp01(brake01) * 100f) : 0;
            if (active == lastBrakeActive && percent == lastBrakePercent) return;
            lastBrakeActive = active;
            lastBrakePercent = percent;
            button.SetSelected(active);
            button.SetLabel(active ? "Тормоз\n" + percent + "%" : "Тормоз\nдержать");
            button.SetAccessibleName(active ? "Тормоз, сила " + percent + " процентов" : "Тормоз, удерживайте для торможения");
            UpdateArtworkState(CabControlAction.Brake, active, active ? brake01 : 0f);
            if (brakeGrip != null)
            {
                Vector2 anchor = brakeGrip.rectTransform.anchorMin;
                anchor.y = Mathf.Lerp(0.16f, 0.84f, active ? brake01 : 0f);
                brakeGrip.rectTransform.anchorMin = brakeGrip.rectTransform.anchorMax = anchor;
            }
        }

        private void HandleDirectShortcuts(Keyboard keyboard)
        {
            if (keyboard == null) return;
            CabControlBinding[] bindings = services.CabRide.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding == null || binding.shortcut == Key.None) continue;
                if (keyboard[binding.shortcut].wasPressedThisFrame) ActivateControl(binding.action);
            }
        }

        private void SetMomentaryGlow(CabControlAction action)
        {
            float until = Time.unscaledTime + 0.22f;
            if (action == CabControlAction.Horn)
            {
                hornGlowUntil = until;
                hornLit = true;
            }
            else if (action == CabControlAction.Bell)
            {
                bellGlowUntil = until;
                bellLit = true;
            }
            if (controls.TryGetValue(action, out AccessibleButton button)) button.SetSelected(true);
            UpdateArtworkState(action, true);
        }

        private void UpdateMomentaryGlow(CabControlAction action, float until, ref bool lit)
        {
            if (!lit || Time.unscaledTime < until) return;
            lit = false;
            if (controls.TryGetValue(action, out AccessibleButton button)) button.SetSelected(false);
            UpdateArtworkState(action, false);
        }

        private void UpdateToggleVisual(CabControlAction action, bool enabled)
        {
            if (!controls.TryGetValue(action, out AccessibleButton button)) return;
            string title = action switch
            {
                CabControlAction.Headlights => "Фары",
                CabControlAction.CabinLight => "Свет кабины",
                CabControlAction.Wipers => "Дворники",
                CabControlAction.Radio => "Радио",
                CabControlAction.Doors => "Двери",
                CabControlAction.WindowHeater => "Обогрев стекла",
                CabControlAction.DispatcherRadio => "Связь",
                _ => action.ToString()
            };
            string state = enabled ? "включены" : "выключены";
            if (action == CabControlAction.Radio || action == CabControlAction.CabinLight ||
                action == CabControlAction.WindowHeater)
            {
                state = enabled ? "включено" : "выключено";
            }
            string visualPrefix = controlArtwork.ContainsKey(action) ? string.Empty : ControlIcon(action) + " ";
            button.SetLabel(visualPrefix + title + "\n" + (enabled ? "● вкл." : "○ выкл."));
            button.SetAccessibleName(title + ", " + state + ShortcutSuffix(action));
            button.SetSelected(enabled);
            UpdateArtworkState(action, enabled);
            if (action == CabControlAction.Radio && radioDisplay != null)
            {
                radioDisplay.text = enabled ? "Радио включено" : "Радио выключено";
            }
        }

        private void UpdateInstruments()
        {
            speedDisplay.SetText("{0:0} км/ч\nТяга {1:0}%", motion.SpeedKph, motion.Throttle01 * 100f);
            UpdateRadioPlayer();
        }

        private void UpdateLighting(float deltaTime)
        {
            float headlightTarget = headlights
                ? Mathf.Lerp(services.CabRide.HeadlightLandscapeAlpha, services.CabRide.HeadlightTunnelAlpha, world.TunnelBlend)
                : 0f;
            float cabinTarget = cabinLight ? services.CabRide.CabinLightAlpha : 0f;
            float transition = services.CabRide.LightTransitionSeconds;
            headlightAlpha = Mathf.MoveTowards(headlightAlpha, headlightTarget, deltaTime / transition);
            cabinAlpha = Mathf.MoveTowards(cabinAlpha, cabinTarget, deltaTime / transition);
            headlightGlow.color = new Color(1f, 0.91f, 0.64f, headlightAlpha);
            cabinGlow.color = new Color(1f, 0.67f, 0.25f, cabinAlpha);
            instrumentGlow.color = new Color(0.25f, 0.72f, 1f,
                services.CabRide.InstrumentIdleAlpha + cabinAlpha * services.CabRide.InstrumentCabinBoost);
            if (radioNightGlow != null)
            {
                float nightGlow = 0.07f + cabinAlpha * 0.44f + world.TunnelBlend * 0.15f + (journey != null ? journey.Night01 * 0.20f : 0f);
                radioNightGlow.color = new Color(0.40f, 0.84f, 0.96f, nightGlow);
            }
        }

        private void AnimateWipers(float deltaTime)
        {
            float motionMultiplier = services.Preferences.motionLevel switch
            {
                MotionLevel.Reduced => 0.45f,
                MotionLevel.Off => 0f,
                _ => 1f
            };
            float wave = wipers && motionMultiplier > 0f
                ? Mathf.Sin(Time.unscaledTime * 4.8f) * 44f * motionMultiplier
                : 0f;
            float leftTarget = 58f - wave;
            float rightTarget = -58f + wave;
            float returnSpeed = wipers ? 360f : 150f;
            float left = Mathf.MoveTowardsAngle(leftWiper.localEulerAngles.z, leftTarget, returnSpeed * deltaTime);
            float right = Mathf.MoveTowardsAngle(rightWiper.localEulerAngles.z, rightTarget, returnSpeed * deltaTime);
            leftWiper.localRotation = Quaternion.Euler(0f, 0f, left);
            rightWiper.localRotation = Quaternion.Euler(0f, 0f, right);
        }

        private void AnimateCabSway(float deltaTime)
        {
            if (cabInterior == null) return;
            if (services.Preferences.motionLevel == MotionLevel.Off)
            {
                cabinSwayOffset = Vector2.zero;
                cabinSwayVelocity = Vector2.zero;
                cabinSwayAngle = 0f;
                cabinSwayAngleVelocity = 0f;
                cabInterior.anchoredPosition = Vector2.zero;
                cabInterior.localRotation = Quaternion.identity;
                return;
            }

            float motionMultiplier = CabSwayMotionMultiplier(services.Preferences.motionLevel,
                Application.platform == RuntimePlatform.Android, services.CabRide.AndroidCabinSwayMultiplier);
            float speedBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.72f, motion.Speed01));
            float phase = Time.unscaledTime * Mathf.Lerp(2.7f, 5.15f, motion.Speed01);
            float secondaryPhase = Time.unscaledTime * Mathf.Lerp(1.35f, 2.25f, motion.Speed01) + 1.1f;
            float amplitude = services.CabRide.CabinSwayPixels * speedBlend * motionMultiplier;
            Vector2 targetOffset = new Vector2(
                (Mathf.Sin(phase) * 0.62f + Mathf.Sin(secondaryPhase) * 0.24f) * amplitude
                    - motion.Acceleration01 * services.CabRide.CabinSwayPixels * 0.24f * motionMultiplier,
                (Mathf.Cos(phase * 1.31f) * 0.20f + Mathf.Sin(secondaryPhase * 0.77f) * 0.10f) * amplitude);
            float targetAngle = ((Mathf.Sin(phase * 0.83f) * 0.72f + Mathf.Sin(secondaryPhase) * 0.20f) * speedBlend
                                 - motion.Acceleration01 * 0.85f) *
                services.CabRide.CabinSwayRotationDegrees * motionMultiplier;
            float smooth = services.CabRide.CabinSwaySmoothSeconds;
            cabinSwayOffset = Vector2.SmoothDamp(cabinSwayOffset, targetOffset, ref cabinSwayVelocity,
                smooth, 60f, Mathf.Clamp(deltaTime, 0f, 0.1f));
            cabinSwayAngle = Mathf.SmoothDampAngle(cabinSwayAngle, targetAngle, ref cabinSwayAngleVelocity,
                smooth, 12f, Mathf.Clamp(deltaTime, 0f, 0.1f));
            cabInterior.anchoredPosition = cabinSwayOffset;
            cabInterior.localRotation = Quaternion.Euler(0f, 0f, cabinSwayAngle);
        }

        public static float CabSwayMotionMultiplier(MotionLevel motionLevel, bool isAndroid, float androidMultiplier)
        {
            if (motionLevel == MotionLevel.Off) return 0f;
            float accessibilityMultiplier = motionLevel == MotionLevel.Reduced ? 0.32f : 1f;
            float platformMultiplier = isAndroid ? Mathf.Clamp(androidMultiplier, 1f, 3f) : 1f;
            return accessibilityMultiplier * platformMultiplier;
        }

        private void AnimateKeychain(float deltaTime)
        {
            if (keychain == null) return;
            float motionMultiplier = services.Preferences.motionLevel switch
            {
                MotionLevel.Reduced => 0.35f,
                MotionLevel.Off => 0f,
                _ => 1f
            };
            float target = 0f;
            if (motionMultiplier > 0f)
            {
                float accelerationSwing = -motion.Acceleration01 * services.CabRide.KeychainAccelerationDegrees;
                float railFrequency = Mathf.Lerp(2.2f, 8.2f, motion.Speed01);
                keychainRailPhase = Mathf.Repeat(keychainRailPhase + railFrequency * deltaTime, Mathf.PI * 2f);
                float railSwing = Mathf.Sin(keychainRailPhase) * motion.Speed01 * services.CabRide.KeychainRailDegrees;
                target = (accelerationSwing + railSwing) * motionMultiplier;
            }
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            float smooth = services.CabRide.KeychainSmoothSeconds;
            float spring = 12f / (smooth * smooth);
            float damping = 3.8f / smooth;
            keychainAngularVelocity += (target - keychainAngle) * spring * dt;
            keychainAngularVelocity /= 1f + damping * dt;
            keychainAngle += keychainAngularVelocity * dt;
            if (Mathf.Abs(keychainAngle) > 32f)
            {
                keychainAngle = Mathf.Clamp(keychainAngle, -32f, 32f);
                keychainAngularVelocity *= -0.18f;
            }
            keychain.localRotation = Quaternion.Euler(0f, 0f, keychainAngle);
        }

        private void OnSegmentChanged(RouteSegmentDefinition segment)
        {
            if (routeDisplay != null) routeDisplay.text = "Маршрут: " + segment.DisplayName;
            SetStatus("Впереди: " + segment.DisplayName.ToLowerInvariant());
        }

        private static string InitialControlLabel(CabControlBinding binding, bool hasArtwork)
        {
            string icon = string.IsNullOrWhiteSpace(binding.icon) ? "●" : binding.icon;
            string prefix = hasArtwork ? string.Empty : icon + " ";
            return binding.action switch
            {
                CabControlAction.Horn => prefix + "Гудок",
                CabControlAction.Headlights => prefix + "Фары\n○ выкл.",
                CabControlAction.CabinLight => prefix + "Свет кабины\n○ выкл.",
                CabControlAction.Wipers => prefix + "Дворники\n○ выкл.",
                CabControlAction.Bell => prefix + "Звонок",
                CabControlAction.Radio => prefix + "Радио\n○ выкл.",
                CabControlAction.Throttle => "Тяга\n0%",
                CabControlAction.Brake => "Тормоз\nдержать",
                CabControlAction.Doors => prefix + "Двери\n○ закрыты",
                CabControlAction.WindowHeater => prefix + "Обогрев\n○ выкл.",
                CabControlAction.DispatcherRadio => prefix + "Связь",
                _ => binding.label
            };
        }

        private void UpdateArtworkState(CabControlAction action, bool active, float leverValue = -1f)
        {
            if (!controlArtwork.TryGetValue(action, out Image artwork) || artwork == null) return;
            artwork.color = active ? Color.white : new Color(0.88f, 0.91f, 0.92f, 0.92f);
            artwork.rectTransform.localScale = active ? Vector3.one * 0.94f : Vector3.one;
            if (leverValue >= 0f && (action == CabControlAction.Throttle || action == CabControlAction.Brake))
            {
                artwork.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-7f, 7f, Mathf.Clamp01(leverValue)));
            }
        }

        private string ControlIcon(CabControlAction action)
        {
            CabControlBinding[] bindings = services.CabRide.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding != null && binding.action == action && !string.IsNullOrWhiteSpace(binding.icon))
                {
                    return binding.icon;
                }
            }
            return "●";
        }

        private string ShortcutSuffix(CabControlAction action)
        {
            CabControlBinding[] bindings = services.CabRide.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding != null && binding.action == action && binding.shortcut != Key.None)
                {
                    return ", клавиша " + binding.shortcut;
                }
            }
            return string.Empty;
        }

        private void SetStatus(string value)
        {
            if (status != null) status.text = value;
        }

        private void OnJourneyStatus(string value)
        {
            if (!vigilanceAlarm) SetStatus(value);
        }

        private void OnJourneyWeather(WeatherType weatherType, float intensity)
        {
            services.Audio.SetWeather(weatherType, intensity);
            bool badWeather = weatherType == WeatherType.Rain || weatherType == WeatherType.Fog || weatherType == WeatherType.Snow;
            if (badWeather && weatherType != lastDispatcherWeather)
                services.Audio.PlayDispatcher(DispatcherVoiceCue.BadWeather);
            lastDispatcherWeather = weatherType;
        }

        private void ReturnToMenu()
        {
            services.Audio.StopAllLoops();
            services.Speech.Stop();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        private static Sprite CreateHeadlightSprite()
        {
            const int width = 192;
            const int height = 128;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Cab Headlight Gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                float spread = Mathf.Lerp(0.46f, 0.10f, v);
                for (int x = 0; x < width; x++)
                {
                    float u = Mathf.Abs((float)x / (width - 1) - 0.5f);
                    float horizontal = 1f - Mathf.SmoothStep(spread * 0.55f, spread, u);
                    float vertical = Mathf.SmoothStep(0f, 0.20f, v) * (1f - Mathf.SmoothStep(0.82f, 1f, v));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, horizontal * vertical);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Cab Headlight Gradient";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        private static Sprite CreateRadialGlowSprite()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Cab Radial Glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = ((float)x / (size - 1) - 0.5f) * 1.6f;
                    float dy = ((float)y / (size - 1) - 0.5f) * 2f;
                    float alpha = 1f - Mathf.SmoothStep(0.15f, 1f, Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Cab Radial Glow";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
