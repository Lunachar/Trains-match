using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private AppServices services;
        private RectTransform root;
        private AccessibleFocusGroup mainFocus;
        private GameObject modal;

        private void Start()
        {
            services = AppServices.Ensure();
            services.Audio.StopAllLoops();
            services.Audio.PlayMenuMusic();
            Build();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && modal == null)
            {
                OpenSettings();
            }
        }

        private void Build()
        {
            Canvas canvas;
            root = UiFactory.CreateScreen("MainMenuCanvas", out canvas);
            AppSettings theme = services.Settings;

            Image background = UiFactory.Image("Background", root, services.Visuals.menuBackground != null ? services.Visuals.menuBackground : services.Visuals.yardBackground, Color.white, false);
            UiFactory.Stretch(background.rectTransform);
            if (background.sprite == null) background.color = theme.GroundColor;

            RectTransform shade = UiFactory.Panel("Shade", root, new Color(0.02f, 0.05f, 0.06f, 0.36f));
            UiFactory.Stretch(shade);

            RectTransform titlePanel = UiFactory.Panel("TitlePanel", root, theme.PanelColor);
            UiFactory.StyleSurface(titlePanel);
            UiFactory.SetRect(titlePanel, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI title = UiFactory.Label("Title", titlePanel, "Сортировочная станция", theme.DisplayFontSize,
                theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.02f, 0.38f), new Vector2(0.98f, 0.96f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI subtitle = UiFactory.Label("Subtitle", titlePanel, "Выберите игру", theme.ControlFontSize,
                theme.MutedTextColor);
            UiFactory.SetRect(subtitle.rectTransform, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.40f), Vector2.zero, Vector2.zero);

            mainFocus = root.gameObject.AddComponent<AccessibleFocusGroup>();
            mainFocus.Cancelled += HandleBack;

            CreateModeButton(GameMode.Colors, "Цвета\n□ ● ■", new Vector2(0.09f, 0.42f), new Vector2(0.46f, 0.69f), new Color(0.17f, 0.50f, 0.62f, 0.97f));
            CreateModeButton(GameMode.Numbers, "Цифры\n1  2  3", new Vector2(0.54f, 0.42f), new Vector2(0.91f, 0.69f), new Color(0.36f, 0.43f, 0.66f, 0.97f));
            CreateModeButton(GameMode.Letters, "Буквы\nА  О  М", new Vector2(0.09f, 0.12f), new Vector2(0.46f, 0.39f), new Color(0.53f, 0.37f, 0.58f, 0.97f));
            CreateModeButton(GameMode.CabRide, "Поездка в кабине\nГудок • Свет • Рельсы", new Vector2(0.54f, 0.12f), new Vector2(0.91f, 0.39f), new Color(0.37f, 0.46f, 0.28f, 0.97f));

            RectTransform settings = UiFactory.Panel("SettingsHold", root, theme.PanelColor);
            UiFactory.StyleSurface(settings);
            UiFactory.SetRect(settings, new Vector2(0.01f, 0.01f), new Vector2(0.13f, 0.105f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI settingsText = UiFactory.Label("Label", settings, "Настройки\nудерживать", theme.CaptionFontSize,
                theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.Stretch(settingsText.rectTransform, 6f, 4f, 6f, 4f);
            Image fill = UiFactory.Image("Fill", settings, null, new Color(theme.AccentColor.r, theme.AccentColor.g, theme.AccentColor.b, 0.42f), false);
            UiFactory.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.transform.SetAsFirstSibling();
            settings.gameObject.AddComponent<HoldToOpenButton>().Initialize(theme.ParentHoldSeconds, settingsText, fill, OpenSettings);

            AccessibleButton exit = UiFactory.Button("ExitGame", root, mainFocus, "Выйти\nиз игры", theme.BrakeColor,
                theme.SelectedColor, OpenExitConfirmation, theme.CaptionFontSize);
            UiFactory.SetRect(exit.RectTransform, new Vector2(0.85f, 0.01f), new Vector2(0.99f, 0.105f), Vector2.zero, Vector2.zero);
            exit.SetAccessibleName("Выйти из игры");
        }

        private void CreateModeButton(GameMode mode, string label, Vector2 min, Vector2 max, Color color)
        {
            AccessibleButton button = UiFactory.Button(mode.ToString(), root, mainFocus, label, color, services.Settings.SelectedColor,
                () => SelectMode(mode), 42);
            UiFactory.SetRect(button.RectTransform, min, max, Vector2.zero, Vector2.zero);
        }

        private void SelectMode(GameMode mode)
        {
            if (mode == GameMode.CabRide)
            {
                OpenSeasonSelection();
                return;
            }
            OpenDifficulty(mode);
        }

        private void OpenSeasonSelection()
        {
            if (modal != null) return;
            mainFocus.enabled = false;
            RectTransform overlay = UiFactory.Panel("SeasonOverlay", root, new Color(0f, 0f, 0f, 0.74f));
            UiFactory.Stretch(overlay);
            modal = overlay.gameObject;
            RectTransform card = UiFactory.Panel("SeasonCard", overlay, services.Settings.PanelColor);
            UiFactory.StyleSurface(card);
            UiFactory.SetRect(card, new Vector2(0.045f, 0.10f), new Vector2(0.955f, 0.90f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI title = UiFactory.Label("Title", card, "Выберите время года", services.Settings.TitleFontSize,
                services.Settings.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI hint = UiFactory.Label("Hint", card, "Можно выбрать автоматически — тогда каждая поездка будет новой",
                services.Settings.StatusFontSize, services.Settings.MutedTextColor, TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(hint.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);

            AccessibleFocusGroup focus = card.gameObject.AddComponent<AccessibleFocusGroup>();
            focus.Cancelled += CloseModal;
            SeasonMode[] modes = { SeasonMode.Auto, SeasonMode.Spring, SeasonMode.Summer, SeasonMode.Autumn, SeasonMode.Winter };
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.6f;
            bool oneRow = aspect >= 1.52f;
            for (int i = 0; i < modes.Length; i++)
            {
                SeasonMode selected = modes[i];
                Rect bounds = SeasonCardBounds(i, oneRow);
                AccessibleButton button = UiFactory.Button("Season_" + selected, card, focus, SeasonLabel(selected),
                    SeasonColor(selected), services.Settings.SelectedColor, () => StartCabRide(selected), services.Settings.ControlFontSize);
                UiFactory.SetRect(button.RectTransform, bounds.min, bounds.max, Vector2.zero, Vector2.zero);
                button.SetSelected(services.Preferences.seasonMode == selected);
                button.SetAccessibleName("Время года: " + SeasonLabel(selected));
                SeasonThemeDefinition theme = selected == SeasonMode.Auto ? null : services.CabEnvironment.FindSeason(ToSeason(selected));
                if (theme != null && theme.cardArtwork != null)
                {
                    Image artwork = UiFactory.Image("Artwork", button.transform, theme.cardArtwork, Color.white, false);
                    UiFactory.SetRect(artwork.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);
                    artwork.raycastTarget = false;
                    artwork.preserveAspect = true;
                    artwork.transform.SetAsFirstSibling();
                }
            }

            AccessibleButton back = UiFactory.Button("Back", card, focus, "← Назад", services.Settings.PanelAltColor,
                services.Settings.SelectedColor, CloseModal, services.Settings.StatusFontSize);
            UiFactory.SetRect(back.RectTransform, new Vector2(0.38f, 0.035f), new Vector2(0.62f, 0.15f), Vector2.zero, Vector2.zero);
        }

        private void StartCabRide(SeasonMode selected)
        {
            services.Preferences.seasonMode = selected;
            services.SavePreferences();
            services.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
        }

        private static Rect SeasonCardBounds(int index, bool oneRow)
        {
            if (oneRow)
            {
                float cardLeft = 0.025f + index * 0.195f;
                return new Rect(cardLeft, 0.20f, 0.17f, 0.52f);
            }
            int row = index < 3 ? 1 : 0;
            int column = row == 1 ? index : index - 3;
            int count = row == 1 ? 3 : 2;
            float width = 0.25f;
            float gap = 0.035f;
            float groupWidth = count * width + (count - 1) * gap;
            float layoutLeft = 0.5f - groupWidth * 0.5f + column * (width + gap);
            return new Rect(layoutLeft, row == 1 ? 0.45f : 0.17f, width, 0.25f);
        }

        private static string SeasonLabel(SeasonMode mode)
        {
            return mode switch
            {
                SeasonMode.Spring => "Весна",
                SeasonMode.Summer => "Лето",
                SeasonMode.Autumn => "Осень",
                SeasonMode.Winter => "Зима",
                _ => "Авто\nКаждый раз новое"
            };
        }

        private static Color SeasonColor(SeasonMode mode)
        {
            return mode switch
            {
                SeasonMode.Spring => new Color(0.42f, 0.70f, 0.45f, 1f),
                SeasonMode.Summer => new Color(0.24f, 0.57f, 0.34f, 1f),
                SeasonMode.Autumn => new Color(0.72f, 0.40f, 0.18f, 1f),
                SeasonMode.Winter => new Color(0.48f, 0.66f, 0.78f, 1f),
                _ => new Color(0.28f, 0.46f, 0.62f, 1f)
            };
        }

        private static SeasonType ToSeason(SeasonMode mode)
        {
            return mode switch
            {
                SeasonMode.Spring => SeasonType.Spring,
                SeasonMode.Autumn => SeasonType.Autumn,
                SeasonMode.Winter => SeasonType.Winter,
                _ => SeasonType.Summer
            };
        }

        private void OpenDifficulty(GameMode mode)
        {
            if (modal != null) return;
            mainFocus.enabled = false;
            RectTransform overlay = UiFactory.Panel("DifficultyOverlay", root, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(overlay);
            modal = overlay.gameObject;
            RectTransform card = UiFactory.Panel("DifficultyCard", overlay, services.Settings.PanelColor);
            UiFactory.StyleSurface(card);
            UiFactory.SetRect(card, new Vector2(0.19f, 0.20f), new Vector2(0.81f, 0.80f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI title = UiFactory.Label("Title", card, ModeTitle(mode) + " — сколько вариантов?",
                services.Settings.TitleFontSize, services.Settings.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.94f), Vector2.zero, Vector2.zero);

            AccessibleFocusGroup focus = card.gameObject.AddComponent<AccessibleFocusGroup>();
            focus.Cancelled += CloseModal;
            for (int count = 2; count <= 4; count++)
            {
                int selectedCount = count;
                string check = services.Progress.IsCompleted(mode, count) ? "  ✓" : string.Empty;
                AccessibleButton level = UiFactory.Button("Level" + count, card, focus, count + " варианта" + check,
                    services.Settings.PrimaryColor, services.Settings.SelectedColor, () =>
                    {
                        services.Session.Select(mode, selectedCount);
                        SceneManager.LoadScene(SceneNames.SortingYard);
                    }, 36);
                float left = 0.08f + (count - 2) * 0.30f;
                UiFactory.SetRect(level.RectTransform, new Vector2(left, 0.32f), new Vector2(left + 0.24f, 0.62f), Vector2.zero, Vector2.zero);
            }

            AccessibleButton back = UiFactory.Button("Back", card, focus, "Назад", services.Settings.PanelAltColor,
                services.Settings.SelectedColor, CloseModal, 30);
            UiFactory.SetRect(back.RectTransform, new Vector2(0.35f, 0.08f), new Vector2(0.65f, 0.24f), Vector2.zero, Vector2.zero);
        }

        private void OpenSettings()
        {
            if (modal != null) return;
            mainFocus.enabled = false;
            SettingsView view = SettingsView.Create(root, () =>
            {
                modal = null;
                mainFocus.enabled = true;
                mainFocus.FocusFirst();
            });
            modal = view.gameObject;
        }

        private void OpenExitConfirmation()
        {
            if (modal != null) return;
            mainFocus.enabled = false;
            RectTransform overlay = UiFactory.Panel("ExitOverlay", root, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(overlay);
            modal = overlay.gameObject;
            RectTransform card = UiFactory.Panel("ExitCard", overlay, services.Settings.PanelColor);
            UiFactory.StyleSurface(card);
            UiFactory.SetRect(card, new Vector2(0.25f, 0.31f), new Vector2(0.75f, 0.69f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI title = UiFactory.Label("Title", card, "Выйти из игры?", services.Settings.TitleFontSize,
                services.Settings.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.07f, 0.60f), new Vector2(0.93f, 0.89f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI hint = UiFactory.Label("Hint", card, "Можно вернуться в игру позже.", services.Settings.ControlFontSize,
                services.Settings.MutedTextColor, TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(hint.rectTransform, new Vector2(0.08f, 0.41f), new Vector2(0.92f, 0.61f), Vector2.zero, Vector2.zero);
            AccessibleFocusGroup focus = card.gameObject.AddComponent<AccessibleFocusGroup>();
            focus.Cancelled += CloseModal;
            AccessibleButton cancel = UiFactory.Button("CancelExit", card, focus, "Остаться", services.Settings.PrimaryColor,
                services.Settings.SelectedColor, CloseModal, services.Settings.ControlFontSize);
            UiFactory.SetRect(cancel.RectTransform, new Vector2(0.08f, 0.10f), new Vector2(0.46f, 0.34f), Vector2.zero, Vector2.zero);
            AccessibleButton confirm = UiFactory.Button("ConfirmExit", card, focus, "Выйти", services.Settings.BrakeColor,
                services.Settings.SelectedColor, ExitGame, services.Settings.ControlFontSize);
            UiFactory.SetRect(confirm.RectTransform, new Vector2(0.54f, 0.10f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero);
        }

        private void ExitGame()
        {
            services.Audio.StopAllLoops();
            Application.Quit();
        }

        private void CloseModal()
        {
            if (modal != null) Destroy(modal);
            modal = null;
            mainFocus.enabled = true;
            mainFocus.FocusFirst();
        }

        private void HandleBack()
        {
            if (modal != null)
            {
                CloseModal();
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.Quit();
#endif
        }

        private static string ModeTitle(GameMode mode)
        {
            return mode switch
            {
                GameMode.Colors => "Цвета",
                GameMode.Numbers => "Цифры",
                GameMode.Letters => "Буквы",
                _ => "Игра"
            };
        }
    }
}
