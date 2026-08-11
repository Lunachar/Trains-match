using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SortingStation.Tests
{
    public sealed class CabAccessibilityAndAudioTests
    {
        [Test]
        public void ThemePairs_MeetNormalTextContrast()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            Assert.That(settings, Is.Not.Null);
            AssertContrast(settings.TextColor, settings.PanelColor, 4.5f);
            AssertContrast(settings.MutedTextColor, settings.PanelColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.PrimaryColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.SelectedColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.FocusColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.BrakeColor, 4.5f);
        }

        [Test]
        public void CabControls_AreLargeUniqueAndDoNotOverlap()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            CabRideDefinition cab = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            Assert.That(settings, Is.Not.Null);
            Assert.That(cab, Is.Not.Null);
            CabControlBinding[] controls = cab.Controls;
            Assert.That(controls.Length, Is.EqualTo(8));
            for (int i = 0; i < controls.Length; i++)
            {
                Vector2 pixels = Vector2.Scale(controls[i].normalizedSize, new Vector2(1500f, 1000f));
                float required = controls[i].action == CabControlAction.Throttle || controls[i].action == CabControlAction.Brake
                    ? 144f
                    : settings.MinimumTargetSize;
                Assert.That(pixels.x, Is.GreaterThanOrEqualTo(required), controls[i].action.ToString());
                Assert.That(pixels.y, Is.GreaterThanOrEqualTo(required), controls[i].action.ToString());
                Assert.That(controls[i].label, Is.Not.Empty, controls[i].action.ToString());
                Assert.That(controls[i].icon, Is.Not.Empty, controls[i].action.ToString());
                Assert.That(controls[i].artwork, Is.Not.Null, controls[i].action + " artwork");
                Rect artwork = new Rect(controls[i].artworkCenter - controls[i].artworkSize * 0.5f, controls[i].artworkSize);
                Assert.That(artwork.xMin, Is.GreaterThanOrEqualTo(0f), controls[i].action.ToString());
                Assert.That(artwork.yMin, Is.GreaterThanOrEqualTo(0f), controls[i].action.ToString());
                Assert.That(artwork.xMax, Is.LessThanOrEqualTo(1f), controls[i].action.ToString());
                Assert.That(artwork.yMax, Is.LessThanOrEqualTo(1f), controls[i].action.ToString());
                Rect first = RectFor(controls[i]);
                for (int j = i + 1; j < controls.Length; j++)
                {
                    Assert.That(first.Overlaps(RectFor(controls[j])), Is.False,
                        controls[i].action + " overlaps " + controls[j].action);
                }
            }
            Key[] shortcuts = controls.Where(binding => binding.shortcut != Key.None).Select(binding => binding.shortcut).ToArray();
            Assert.That(shortcuts.Distinct().Count(), Is.EqualTo(shortcuts.Length));
        }

        [Test]
        public void SceneryCatalog_ContainsExpandedEnvironmentSet()
        {
            CabSceneryCatalog catalog = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            Assert.That(catalog, Is.Not.Null);
            string[] required =
            {
                "tractor", "horses", "hay_bales", "windmill",
                "rural_station", "level_crossing", "railway_signal", "water_tower",
                "birch_grove", "sunflower_field", "waterfall", "castle_ruins"
            };
            foreach (string id in required)
            {
                CabSceneryDefinition entry = catalog.Find(id);
                Assert.That(entry, Is.Not.Null, id);
                Assert.That(entry.sprite, Is.Not.Null, id + " sprite");
            }
        }

        [Test]
        public void SelectedCabToggle_StaysVisiblyPressedUntilTurnedOff()
        {
            GameObject go = new GameObject("Pressed toggle", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline), typeof(AccessibleButton));
            AccessibleButton button = go.GetComponent<AccessibleButton>();
            button.Initialize(null, null, "Фары", Color.gray, Color.green, Color.yellow, 0.96f,
                Color.white, Color.black, null);
            button.ConfigurePersistentPress(true, 5f);

            button.SetSelected(true);
            Assert.That(go.transform.localScale.x, Is.EqualTo(0.96f).Within(0.0001f));
            Assert.That(((RectTransform)go.transform).anchoredPosition.y, Is.EqualTo(-5f).Within(0.0001f));

            button.SetSelected(false);
            Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(((RectTransform)go.transform).anchoredPosition, Is.EqualTo(Vector2.zero));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ZeroSpeed_ProducesNoWorldDistance()
        {
            Assert.That(CabWorldRenderer.CalculateDistanceDelta(0f, 28f, 0.1f), Is.Zero);
            Assert.That(CabWorldRenderer.CalculateDistanceDelta(0f, 28f, 10f), Is.Zero);
        }

        [Test]
        public void RadioPitchAndVolume_DoNotChangeWithTrainSpeed()
        {
            GameObject go = new GameObject("Audio Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip music = AudioClip.Create("radio", 2205, 1, 44100, false);
            AudioClip rails = AudioClip.Create("rails", 2205, 1, 44100, false);
            catalog.Configure(music, music, rails, System.Array.Empty<SoundEventBank>());
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, new UserPreferences { masterVolume = 0.8f, musicVolume = 0.6f, effectsVolume = 0.7f });
            audio.SetRadio(true);
            float expectedVolume = audio.MusicVolume;
            audio.SetRails(0.15f);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(expectedVolume).Within(0.0001f));
            audio.SetRails(1f);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(expectedVolume).Within(0.0001f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(music);
            Object.DestroyImmediate(rails);
        }

        [Test]
        public void ProjectSetupWindow_UsesSpecializedConfigurationInspectors()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            CabRideDefinition cab = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            AudioCatalog audio = Resources.Load<AudioCatalog>("Configuration/AudioCatalog");

            UnityEditor.Editor settingsEditor = UnityEditor.Editor.CreateEditor(settings);
            UnityEditor.Editor cabEditor = UnityEditor.Editor.CreateEditor(cab);
            UnityEditor.Editor audioEditor = UnityEditor.Editor.CreateEditor(audio);
            Assert.That(settingsEditor.GetType().Name, Is.EqualTo("AppSettingsInspector"));
            Assert.That(cabEditor.GetType().Name, Is.EqualTo("CabRideDefinitionInspector"));
            Assert.That(audioEditor.GetType().Name, Is.EqualTo("AudioCatalogInspector"));

            SortingStation.EditorTools.SortingStationSetupWindow.Open();
            SortingStation.EditorTools.SortingStationSetupWindow window =
                Resources.FindObjectsOfTypeAll<SortingStation.EditorTools.SortingStationSetupWindow>().FirstOrDefault();
            Assert.That(window, Is.Not.Null);

            Object.DestroyImmediate(settingsEditor);
            Object.DestroyImmediate(cabEditor);
            Object.DestroyImmediate(audioEditor);
            window.Close();
        }

        private static Rect RectFor(CabControlBinding binding)
        {
            return new Rect(binding.normalizedCenter - binding.normalizedSize * 0.5f, binding.normalizedSize);
        }

        private static void AssertContrast(Color foreground, Color background, float minimum)
        {
            Assert.That(AppSettings.ContrastRatio(foreground, background), Is.GreaterThanOrEqualTo(minimum));
        }
    }
}
