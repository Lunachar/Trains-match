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
        private RectTransform root;
        private RectTransform stage;
        private RectTransform cabInterior;
        private AccessibleFocusGroup focusGroup;
        private readonly Dictionary<CabControlAction, AccessibleButton> controls = new Dictionary<CabControlAction, AccessibleButton>();
        private readonly Dictionary<CabControlAction, Image> controlArtwork = new Dictionary<CabControlAction, Image>();
        private TextMeshProUGUI status;
        private TextMeshProUGUI speedDisplay;
        private TextMeshProUGUI radioDisplay;
        private TextMeshProUGUI radioTrackTitle;
        private TextMeshProUGUI radioTime;
        private RectTransform radioPlaylist;
        private AccessibleButton radioPowerButton;
        private AccessibleButton radioPlaylistButton;
        private Image radioNightGlow;
        private RectTransform radioPlayGlyph;
        private RectTransform radioPauseGlyph;
        private bool playlistOpen;
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
        private float headlightAlpha;
        private float cabinAlpha;
        private bool headlights;
        private bool cabinLight;
        private bool wipers;
        private bool radio;
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
            bool keyboardBrake = false;
            Keyboard keyboard = Keyboard.current;
            HandleDirectShortcuts(keyboard);
            if (keyboard != null && controls.TryGetValue(CabControlAction.Brake, out AccessibleButton brakeButton))
            {
                keyboardBrake = focusGroup.Current == brakeButton &&
                                (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
            }

            UpdateVigilance();
            float brake01 = automaticStop ? 1f : pointerBrake ? pointerBrakeStrength : keyboardBrake || Time.unscaledTime < brakePulseUntil ? 1f : 0f;
            motion.Step(dt, brake01);
            services.Audio.SetRails(motion.Speed01);
            world.Advance(motion.Speed01, motion.Acceleration01, dt);
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
            services.Audio.SetRails(0f);
            services.Audio.SetRadio(false);
            services.Speech.Stop();
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
            UpdateThrottleVisual();
            UpdateToggleVisual(CabControlAction.Headlights, false);
            UpdateToggleVisual(CabControlAction.CabinLight, false);
            UpdateToggleVisual(CabControlAction.Wipers, false);
            UpdateToggleVisual(CabControlAction.Radio, false);
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
            keychain.anchorMin = keychain.anchorMax = new Vector2(0.535f, 0.535f);
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
            status = UiFactory.Label("CabStatus", cabInterior, string.Empty, theme.CaptionFontSize,
                new Color(0.68f, 0.91f, 1f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(status.rectTransform, new Vector2(0.585f, 0.345f), new Vector2(0.705f, 0.445f), Vector2.zero, Vector2.zero);

            speedDisplay = UiFactory.Label("SpeedDisplay", cabInterior, "0 км/ч", theme.ControlFontSize,
                new Color(0.63f, 0.93f, 1f, 1f), TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.SetRect(speedDisplay.rectTransform, new Vector2(0.72f, 0.365f), new Vector2(0.855f, 0.475f), Vector2.zero, Vector2.zero);

            radioDisplay = UiFactory.Label("RadioDisplay", cabInterior, "Радио выключено", theme.CaptionFontSize,
                new Color(0.70f, 0.93f, 0.83f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(radioDisplay.rectTransform, new Vector2(0.365f, 0.335f), new Vector2(0.478f, 0.425f), Vector2.zero, Vector2.zero);
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
                if (binding.action == CabControlAction.Radio) continue;
                Color idle = WithAlpha(theme.PanelColor, 0.48f);
                Color selected = WithAlpha(theme.SelectedColor, 0.82f);
                if (binding.action == CabControlAction.Brake)
                {
                    idle = WithAlpha(theme.BrakeColor, 0.72f);
                    selected = WithAlpha(theme.AccentColor, 0.88f);
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
                Vector2 plateMin = new Vector2(0.04f, 0.04f);
                Vector2 plateMax = hasArtwork ? new Vector2(0.96f, 0.96f)
                    : lever ? new Vector2(0.96f, 0.47f) : new Vector2(0.96f, 0.80f);
                UiFactory.SetRect(statePlate.rectTransform, plateMin, plateMax, Vector2.zero, Vector2.zero);
                Vector2 labelMin = hasArtwork ? new Vector2(0.06f, 0.04f) : plateMin;
                Vector2 labelMax = hasArtwork ? new Vector2(0.94f, lever ? 0.34f : 0.42f) : plateMax;
                UiFactory.SetRect(button.Label.rectTransform, labelMin, labelMax,
                    new Vector2(8f, 5f), new Vector2(-8f, -5f));
                button.SetStateGraphic(statePlate);

                if (hasArtwork)
                {
                    Image artwork = UiFactory.Image("Artwork", button.transform, binding.artwork,
                        new Color(0.72f, 0.78f, 0.82f, 0.76f), true);
                    Vector2 artworkHalf = binding.artworkSize * 0.5f;
                    UiFactory.SetRect(artwork.rectTransform, binding.artworkCenter - artworkHalf,
                        binding.artworkCenter + artworkHalf, Vector2.zero, Vector2.zero);
                    artwork.raycastTarget = false;
                    artwork.transform.SetSiblingIndex(1);
                    controlArtwork[binding.action] = artwork;
                }
                controls[binding.action] = button;
            }

            AccessibleButton throttleButton = controls[CabControlAction.Throttle];
            throttleButton.PointerDragged += SetThrottleFromPointer;
            Image throttleTrack = UiFactory.Image("ThrottleTrack", throttleButton.transform, UiFactory.RoundedSprite(),
                WithAlpha(theme.PanelColor, 0.86f), false);
            throttleTrack.type = Image.Type.Sliced;
            throttleTrack.raycastTarget = false;
            throttleTrack.rectTransform.anchorMin = new Vector2(0.84f, 0.16f);
            throttleTrack.rectTransform.anchorMax = new Vector2(0.84f, 0.84f);
            throttleTrack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            throttleTrack.rectTransform.sizeDelta = new Vector2(10f, 0f);
            throttleTrack.rectTransform.anchoredPosition = Vector2.zero;
            throttleGrip = UiFactory.Image("ThrottleGrip", throttleButton.transform, null, theme.AccentColor, false);
            throttleGrip.sprite = UiFactory.RoundedSprite();
            throttleGrip.type = Image.Type.Sliced;
            throttleGrip.raycastTarget = false;
            throttleGrip.rectTransform.anchorMin = throttleGrip.rectTransform.anchorMax = new Vector2(0.84f, 0.15f);
            throttleGrip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            throttleGrip.rectTransform.sizeDelta = new Vector2(32f, 32f);
            throttleGrip.rectTransform.anchoredPosition = Vector2.zero;

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

            CreateThrottleStepButton("ThrottleMinus", "−", new Vector2(0.455f, 0.135f), new Vector2(0.535f, 0.255f), -1f);
            CreateThrottleStepButton("ThrottlePlus", "+", new Vector2(0.56f, 0.135f), new Vector2(0.64f, 0.255f), 1f);
        }

        private void BuildRadioPlayer()
        {
            AppSettings theme = services.Settings;
            RectTransform player = UiFactory.Panel("RetroRadioPlayer", cabInterior, new Color(0.025f, 0.055f, 0.07f, 0.94f), UiFactory.RoundedSprite());
            UiFactory.SetRect(player, new Vector2(0.018f, 0.275f), new Vector2(0.265f, 0.575f), Vector2.zero, Vector2.zero);
            UiFactory.StyleSurface(player, false);
            radioNightGlow = UiFactory.Image("RadioNightGlow", player, UiFactory.RoundedSprite(), new Color(0.35f, 1f, 0.22f, 0.08f), false);
            radioNightGlow.type = Image.Type.Sliced;
            radioNightGlow.raycastTarget = false;
            UiFactory.SetRect(radioNightGlow.rectTransform, new Vector2(-0.035f, -0.045f), new Vector2(1.035f, 1.045f), Vector2.zero, Vector2.zero);
            radioNightGlow.transform.SetAsFirstSibling();

            TextMeshProUGUI heading = UiFactory.Label("RadioHeading", player, "РАДИО • МАРШРУТ", theme.CaptionFontSize,
                new Color(0.65f, 1f, 0.58f, 1f), TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.SetRect(heading.rectTransform, new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
            radioTrackTitle = UiFactory.Label("TrackTitle", player, "Радио выключено", theme.CaptionFontSize,
                new Color(0.80f, 1f, 0.72f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
            radioTrackTitle.enableWordWrapping = false;
            radioTrackTitle.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.SetRect(radioTrackTitle.rectTransform, new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            radioTime = UiFactory.Label("TrackTime", player, "00:00 - 00:00", theme.CaptionFontSize,
                new Color(0.47f, 0.84f, 0.61f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(radioTime.rectTransform, new Vector2(0.05f, 0.43f), new Vector2(0.95f, 0.58f), Vector2.zero, Vector2.zero);

            radioPowerButton = RadioButton("RadioPower", player, string.Empty, new Vector2(0.38f, 0.12f), new Vector2(0.62f, 0.39f), ToggleRadio, "Радио: включить или поставить на паузу");
            radioPlayGlyph = CreateRadioVisual(radioPowerButton, services.CabScenery != null ? services.CabScenery.RadioPlay : null, "play");
            radioPauseGlyph = CreateRadioGlyph(radioPowerButton, "pause");
            AccessibleButton previous = RadioButton("RadioPrevious", player, string.Empty, new Vector2(0.08f, 0.12f), new Vector2(0.31f, 0.39f), PreviousRadioTrack, "Предыдущий трек");
            CreateRadioVisual(previous, services.CabScenery != null ? services.CabScenery.RadioPrevious : null, "previous");
            AccessibleButton next = RadioButton("RadioNext", player, string.Empty, new Vector2(0.69f, 0.12f), new Vector2(0.92f, 0.39f), NextRadioTrack, "Следующий трек");
            CreateRadioVisual(next, services.CabScenery != null ? services.CabScenery.RadioNext : null, "next");
            radioPlaylistButton = RadioButton("RadioPlaylist", player, string.Empty, new Vector2(0.05f, -0.15f), new Vector2(0.95f, 0.06f), TogglePlaylist, "Открыть список треков");
            CreateRadioVisual(radioPlaylistButton, services.CabScenery != null ? services.CabScenery.RadioPlaylist : null, "playlist");

            radioPlaylist = UiFactory.Panel("RadioPlaylist", cabInterior, new Color(0.02f, 0.04f, 0.055f, 0.96f), UiFactory.RoundedSprite());
            UiFactory.SetRect(radioPlaylist, new Vector2(0.018f, 0.075f), new Vector2(0.265f, 0.27f), Vector2.zero, Vector2.zero);
            UiFactory.StyleSurface(radioPlaylist, false);
            radioPlaylist.gameObject.SetActive(false);
            BuildPlaylistEntries();
            UpdateRadioPlayer();
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
            services.Speech.Speak("Диспетчер. Состав номер " + trainNumber +
                ". Дано разрешение на старт движения. Подтвердите готовность.");
        }

        private void AcknowledgeDispatcher()
        {
            services.Audio.Play(SoundCue.Toggle);
            if (!departureAuthorized)
            {
                departureAuthorized = true;
                nextVigilanceAt = Time.unscaledTime + 60f;
                SetStatus("Диспетчер: движение разрешено. Можно набрать тягу.");
                services.Speech.Speak("Диспетчер. Движение разрешено.");
                dispatcherButton.SetAccessibleName("Красная кнопка диспетчера: ожидание проверки бдительности");
                return;
            }

            if (!vigilanceAlarm) return;
            vigilanceAlarm = false;
            nextVigilanceAt = Time.unscaledTime + 120f;
            SetStatus("Бдительность подтверждена. Следующая проверка через две минуты.");
            services.Speech.Speak("Бдительность подтверждена.");
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
                services.Speech.Speak("Проверка бдительности. Нажмите красную кнопку.");
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
            services.Audio.Play(SoundCue.Brake);
            SetStatus("Нет подтверждения: поезд автоматически останавливается.");
            services.Speech.Speak("Нет подтверждения. Поезд автоматически останавливается.");
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

        private AccessibleButton RadioButton(string name, Transform parent, string label, Vector2 min, Vector2 max, System.Action action, string accessibleName)
        {
            AccessibleButton button = UiFactory.Button(name, parent, focusGroup, label, new Color(0.10f, 0.20f, 0.16f, 1f),
                new Color(0.34f, 0.74f, 0.35f, 1f), action, services.Settings.CaptionFontSize);
            UiFactory.SetRect(button.RectTransform, min, max, Vector2.zero, Vector2.zero);
            button.SetAccessibleName(accessibleName);
            button.Label.gameObject.SetActive(false);
            return button;
        }

        private RectTransform CreateRadioGlyph(AccessibleButton button, string kind)
        {
            RectTransform root = new GameObject("Glyph_" + kind, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(button.transform, false);
            UiFactory.Stretch(root);
            Color color = new Color(0.75f, 1f, 0.70f, 1f);
            switch (kind)
            {
                case "pause":
                    GlyphStroke(root, color, new Vector2(0.41f, 0.5f), 8f, 30f, 0f);
                    GlyphStroke(root, color, new Vector2(0.59f, 0.5f), 8f, 30f, 0f);
                    break;
                case "previous":
                    GlyphStroke(root, color, new Vector2(0.35f, 0.5f), 7f, 34f, 0f);
                    GlyphStroke(root, color, new Vector2(0.60f, 0.62f), 7f, 30f, 45f);
                    GlyphStroke(root, color, new Vector2(0.60f, 0.38f), 7f, 30f, -45f);
                    break;
                case "next":
                    GlyphStroke(root, color, new Vector2(0.65f, 0.5f), 7f, 34f, 0f);
                    GlyphStroke(root, color, new Vector2(0.40f, 0.62f), 7f, 30f, -45f);
                    GlyphStroke(root, color, new Vector2(0.40f, 0.38f), 7f, 30f, 45f);
                    break;
                case "playlist":
                    for (int i = 0; i < 3; i++) GlyphStroke(root, color, new Vector2(0.5f, 0.30f + i * 0.20f), 64f, 6f, 90f);
                    break;
                default:
                    GlyphStroke(root, color, new Vector2(0.43f, 0.62f), 7f, 30f, 45f);
                    GlyphStroke(root, color, new Vector2(0.43f, 0.38f), 7f, 30f, -45f);
                    GlyphStroke(root, color, new Vector2(0.63f, 0.5f), 7f, 31f, 0f);
                    break;
            }
            return root;
        }

        private RectTransform CreateRadioVisual(AccessibleButton button, Sprite artwork, string fallbackKind)
        {
            if (artwork == null) return CreateRadioGlyph(button, fallbackKind);
            Image visual = UiFactory.Image("GeneratedRadio_" + fallbackKind, button.transform, artwork, Color.white, false);
            visual.raycastTarget = false;
            UiFactory.Stretch(visual.rectTransform, 2f, 2f, 2f, 2f);
            return visual.rectTransform;
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

        private void BuildPlaylistEntries()
        {
            AudioClip[] tracks = services.AudioCatalog != null ? services.AudioCatalog.CabRadioPlaylist : System.Array.Empty<AudioClip>();
            int count = Mathf.Min(4, tracks.Length);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                string title = tracks[i] != null ? tracks[i].name : "Пустой слот";
                AccessibleButton entry = RadioButton("Track_" + i, radioPlaylist, (i + 1) + ". " + title,
                    new Vector2(0.05f, 0.72f - i * 0.23f), new Vector2(0.95f, 0.91f - i * 0.23f),
                    () => SelectRadioTrack(index), "Трек " + (i + 1) + ": " + title);
                entry.Label.enableWordWrapping = false;
                entry.Label.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (count == 0)
            {
                TextMeshProUGUI empty = UiFactory.Label("EmptyPlaylist", radioPlaylist, "Добавьте музыку в Audio Catalog", services.Settings.CaptionFontSize,
                    new Color(0.72f, 0.84f, 0.78f, 1f), TextAlignmentOptions.Center, UiFontRole.Body);
                UiFactory.SetRect(empty.rectTransform, new Vector2(0.07f, 0.2f), new Vector2(0.93f, 0.82f), Vector2.zero, Vector2.zero);
            }
        }

        private void ToggleRadio()
        {
            radio = !radio;
            services.Audio.SetRadio(radio);
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
            if (radioPlaylistButton != null) radioPlaylistButton.SetLabel(playlistOpen ? "≡ скрыть" : "≡ треки");
        }

        private void UpdateRadioPlayer()
        {
            if (radioTrackTitle == null) return;
            bool playing = services.Audio.RadioIsPlaying;
            radioTrackTitle.text = playing ? services.Audio.RadioTrackName : "Радио выключено";
            float time = services.Audio.RadioTrackTime;
            float duration = services.Audio.RadioTrackLength;
            radioTime.text = string.Format("{0:00}:{1:00} - {2:00}:{3:00}", Mathf.FloorToInt(time / 60f), Mathf.FloorToInt(time % 60f), Mathf.FloorToInt(duration / 60f), Mathf.FloorToInt(duration % 60f));
            if (radioPlayGlyph != null) radioPlayGlyph.gameObject.SetActive(!playing);
            if (radioPauseGlyph != null) radioPauseGlyph.gameObject.SetActive(playing);
            if (radioDisplay != null) radioDisplay.text = playing ? "Радио: " + services.Audio.RadioTrackName : "Радио выключено";
        }

        private void CreateThrottleStepButton(string name, string label, Vector2 min, Vector2 max, float direction)
        {
            AppSettings theme = services.Settings;
            AccessibleButton button = UiFactory.Button(name, cabInterior, null, label,
                WithAlpha(theme.PanelAltColor, 0.90f), theme.PrimaryColor,
                () => AdjustThrottle(direction * services.CabRide.KeyboardThrottleStep), 42);
            UiFactory.SetRect(button.RectTransform, min, max, Vector2.zero, Vector2.zero);
            button.SetAccessibleName(direction < 0f ? "Уменьшить тягу" : "Увеличить тягу");
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
            }
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
            float value = Mathf.InverseLerp(rect.yMin + 18f, rect.yMax - 18f, local.y);
            motion.SetThrottle(value);
            UpdateThrottleVisual();
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
            button.SetLabel("Тяга\n" + percent + "%");
            button.SetAccessibleName("Тяга " + percent + " процентов");
            button.SetSelected(motion.Throttle01 > 0.001f);
            UpdateArtworkState(CabControlAction.Throttle, motion.Throttle01 > 0.001f, motion.Throttle01);
            if (throttleGrip != null)
            {
                Vector2 anchor = throttleGrip.rectTransform.anchorMin;
                anchor.y = Mathf.Lerp(0.16f, 0.84f, motion.Throttle01);
                throttleGrip.rectTransform.anchorMin = throttleGrip.rectTransform.anchorMax = anchor;
            }
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
                _ => action.ToString()
            };
            string state = enabled ? "включены" : "выключены";
            if (action == CabControlAction.Radio || action == CabControlAction.CabinLight)
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
                float nightGlow = 0.07f + cabinAlpha * 0.44f + world.TunnelBlend * 0.15f;
                radioNightGlow.color = new Color(0.35f, 1f, 0.22f, nightGlow);
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

            float motionMultiplier = services.Preferences.motionLevel == MotionLevel.Reduced ? 0.35f : 1f;
            float phase = Time.unscaledTime * (4.1f + motion.Speed01 * 1.3f);
            float amplitude = services.CabRide.CabinSwayPixels * motion.Speed01 * motionMultiplier;
            Vector2 targetOffset = new Vector2(
                Mathf.Sin(phase) * amplitude * 0.34f - motion.Acceleration01 * services.CabRide.CabinSwayPixels * 0.18f * motionMultiplier,
                Mathf.Cos(phase * 1.37f) * amplitude * 0.12f);
            float targetAngle = (Mathf.Sin(phase * 0.84f) * motion.Speed01 * 0.45f - motion.Acceleration01) *
                services.CabRide.CabinSwayRotationDegrees * motionMultiplier;
            float smooth = services.CabRide.CabinSwaySmoothSeconds;
            cabinSwayOffset = Vector2.SmoothDamp(cabinSwayOffset, targetOffset, ref cabinSwayVelocity,
                smooth, 60f, Mathf.Clamp(deltaTime, 0f, 0.1f));
            cabinSwayAngle = Mathf.SmoothDampAngle(cabinSwayAngle, targetAngle, ref cabinSwayAngleVelocity,
                smooth, 12f, Mathf.Clamp(deltaTime, 0f, 0.1f));
            cabInterior.anchoredPosition = cabinSwayOffset;
            cabInterior.localRotation = Quaternion.Euler(0f, 0f, cabinSwayAngle);
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
                float railSwing = Mathf.Sin(Time.unscaledTime * 5.2f) * motion.Speed01 * services.CabRide.KeychainRailDegrees;
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
                _ => binding.label
            };
        }

        private void UpdateArtworkState(CabControlAction action, bool active, float leverValue = -1f)
        {
            if (!controlArtwork.TryGetValue(action, out Image artwork) || artwork == null) return;
            artwork.color = active ? Color.white : new Color(0.72f, 0.78f, 0.82f, 0.76f);
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
