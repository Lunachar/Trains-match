using System;
using TMPro;
using UnityEngine;

namespace SortingStation
{
    public sealed class SettingsView : MonoBehaviour
    {
        private AccessibleFocusGroup focusGroup;
        private AppServices services;
        private Action closed;

        public static SettingsView Create(Transform parent, Action onClosed)
        {
            AppServices services = AppServices.Ensure();
            RectTransform dim = UiFactory.Panel("SettingsOverlay", parent, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(dim);
            SettingsView view = dim.gameObject.AddComponent<SettingsView>();
            view.services = services;
            view.closed = onClosed;
            view.Build();
            return view;
        }

        private void Build()
        {
            AppSettings theme = services.Settings;
            RectTransform card = UiFactory.Panel("SettingsCard", transform, theme.PanelColor);
            UiFactory.StyleSurface(card);
            UiFactory.SetRect(card, new Vector2(0.16f, 0.07f), new Vector2(0.84f, 0.93f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI title = UiFactory.Label("Title", card, "Настройки", theme.DisplayFontSize,
                theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);

            focusGroup = card.gameObject.AddComponent<AccessibleFocusGroup>();
            focusGroup.Cancelled += Close;

            CreateVolumeRow(card, "Общая громкость", 0.72f, () => services.Preferences.masterVolume,
                value => services.Preferences.masterVolume = value);
            CreateVolumeRow(card, "Музыка", 0.57f, () => services.Preferences.musicVolume,
                value => services.Preferences.musicVolume = value);
            CreateVolumeRow(card, "Звуки", 0.42f, () => services.Preferences.effectsVolume,
                value => services.Preferences.effectsVolume = value);
            CreateVolumeRow(card, "Голос", 0.27f, () => services.Preferences.speechVolume,
                value => services.Preferences.speechVolume = value);

            AccessibleButton speech = null;
            speech = UiFactory.Button("Speech", card, focusGroup, SpeechLabel(), theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    services.Preferences.speechEnabled = !services.Preferences.speechEnabled;
                    speech.SetLabel(SpeechLabel());
                    speech.SetSelected(services.Preferences.speechEnabled);
                    services.SavePreferences();
                    if (services.Preferences.speechEnabled) services.Speech.Speak("Голосовые подсказки включены");
                }, 27);
            UiFactory.SetRect(speech.RectTransform, new Vector2(0.04f, 0.15f), new Vector2(0.34f, 0.265f), Vector2.zero, Vector2.zero);
            speech.SetSelected(services.Preferences.speechEnabled);

            AccessibleButton motion = null;
            motion = UiFactory.Button("Motion", card, focusGroup, MotionLabel(), theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    services.Preferences.motionLevel = (MotionLevel)(((int)services.Preferences.motionLevel + 1) % 3);
                    motion.SetLabel(MotionLabel());
                    motion.SetSelected(services.Preferences.motionLevel != MotionLevel.Normal);
                    services.SavePreferences();
                }, 27);
            UiFactory.SetRect(motion.RectTransform, new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.265f), Vector2.zero, Vector2.zero);
            motion.SetSelected(services.Preferences.motionLevel != MotionLevel.Normal);

            AccessibleButton prompts = null;
            prompts = UiFactory.Button("RoutePrompts", card, focusGroup, RoutePromptsLabel(), theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    services.Preferences.routePromptsEnabled = !services.Preferences.routePromptsEnabled;
                    prompts.SetLabel(RoutePromptsLabel());
                    prompts.SetSelected(services.Preferences.routePromptsEnabled);
                    services.SavePreferences();
                }, 24);
            UiFactory.SetRect(prompts.RectTransform, new Vector2(0.66f, 0.15f), new Vector2(0.96f, 0.265f), Vector2.zero, Vector2.zero);
            prompts.SetSelected(services.Preferences.routePromptsEnabled);

            AccessibleButton interactions = null;
            interactions = UiFactory.Button("GentleInteractions", card, focusGroup, InteractionsLabel(), theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    services.Preferences.gentleInteractionsEnabled = !services.Preferences.gentleInteractionsEnabled;
                    interactions.SetLabel(InteractionsLabel());
                    interactions.SetSelected(services.Preferences.gentleInteractionsEnabled);
                    services.SavePreferences();
                }, 23);
            UiFactory.SetRect(interactions.RectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.34f, 0.135f), Vector2.zero, Vector2.zero);
            interactions.SetSelected(services.Preferences.gentleInteractionsEnabled);

            AccessibleButton hints = null;
            hints = UiFactory.Button("GentleHints", card, focusGroup, HintsLabel(), theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    services.Preferences.gentleHintsEnabled = !services.Preferences.gentleHintsEnabled;
                    hints.SetLabel(HintsLabel());
                    hints.SetSelected(services.Preferences.gentleHintsEnabled);
                    services.SavePreferences();
                }, 23);
            UiFactory.SetRect(hints.RectTransform, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.135f), Vector2.zero, Vector2.zero);
            hints.SetSelected(services.Preferences.gentleHintsEnabled);

            AccessibleButton close = UiFactory.Button("Close", card, focusGroup, "Готово", theme.PrimaryColor,
                theme.SelectedColor, Close, 34);
            UiFactory.SetRect(close.RectTransform, new Vector2(0.66f, 0.02f), new Vector2(0.96f, 0.135f), Vector2.zero, Vector2.zero);
        }

        private void CreateVolumeRow(RectTransform parent, string labelText, float anchorY, Func<float> getter, Action<float> setter)
        {
            AppSettings theme = services.Settings;
            TextMeshProUGUI label = UiFactory.Label(labelText, parent, labelText, theme.ControlFontSize,
                theme.TextColor, TextAlignmentOptions.Left, UiFontRole.Body);
            UiFactory.SetRect(label.rectTransform, new Vector2(0.07f, anchorY), new Vector2(0.43f, anchorY + 0.11f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI value = UiFactory.Label(labelText + "Value", parent, Percent(getter()), theme.ControlFontSize,
                theme.AccentColor, TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.SetRect(value.rectTransform, new Vector2(0.58f, anchorY), new Vector2(0.72f, anchorY + 0.11f), Vector2.zero, Vector2.zero);

            AccessibleButton minus = UiFactory.Button(labelText + "Minus", parent, focusGroup, "−", theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    setter(Mathf.Clamp01(getter() - 0.1f));
                    value.text = Percent(getter());
                    services.SavePreferences();
                }, 48);
            UiFactory.SetRect(minus.RectTransform, new Vector2(0.44f, anchorY), new Vector2(0.56f, anchorY + 0.11f), Vector2.zero, Vector2.zero);

            AccessibleButton plus = UiFactory.Button(labelText + "Plus", parent, focusGroup, "+", theme.PanelAltColor,
                theme.SelectedColor, () =>
                {
                    setter(Mathf.Clamp01(getter() + 0.1f));
                    value.text = Percent(getter());
                    services.SavePreferences();
                }, 48);
            UiFactory.SetRect(plus.RectTransform, new Vector2(0.74f, anchorY), new Vector2(0.86f, anchorY + 0.11f), Vector2.zero, Vector2.zero);
        }

        private string SpeechLabel() => services.Preferences.speechEnabled ? "Голос: включён" : "Голос: выключен";
        private string RoutePromptsLabel() => services.Preferences.routePromptsEnabled ? "Подсказки в поездке: да" : "Подсказки в поездке: нет";
        private string InteractionsLabel() => services.Preferences.gentleInteractionsEnabled ? "Реакции мира: да" : "Реакции мира: нет";
        private string HintsLabel() => services.Preferences.gentleHintsEnabled ? "Значки-подсказки: да" : "Значки-подсказки: нет";

        private string MotionLabel()
        {
            return services.Preferences.motionLevel switch
            {
                MotionLevel.Reduced => "Движение: меньше",
                MotionLevel.Off => "Движение: выключено",
                _ => "Движение: обычное"
            };
        }

        private static string Percent(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";

        private void Close()
        {
            services.SavePreferences();
            closed?.Invoke();
            Destroy(gameObject);
        }
    }
}
