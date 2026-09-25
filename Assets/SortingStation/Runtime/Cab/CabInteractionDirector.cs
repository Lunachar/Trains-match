using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class CabInteractionDirector : MonoBehaviour
    {
        private CabInteractionCatalog catalog;
        private CabInteractionSequence sequence;
        private CabWorldRenderer world;
        private CabJourneyDirector journey;
        private IAudioService audioService;
        private UserPreferences preferences;
        private CabInteractionDefinition active;
        private float movingTimer;
        private float nextAt;
        private float opportunityTime;
        private RectTransform hintRoot;
        private Image hintImage;
        private TextMeshProUGUI hintGlyph;

        public event Action<CabInteractionDefinition> ReactionTriggered;
        public CabInteractionDefinition ActiveOpportunity => active;

        public void Initialize(RectTransform parent, CabInteractionCatalog interactionCatalog, CabWorldRenderer worldRenderer,
            CabJourneyDirector journeyDirector, IAudioService audioService, UserPreferences userPreferences, int seed)
        {
            catalog = interactionCatalog;
            world = worldRenderer;
            journey = journeyDirector;
            this.audioService = audioService;
            preferences = userPreferences ?? new UserPreferences();
            sequence = new CabInteractionSequence(catalog != null ? catalog.Interactions : null, seed,
                catalog != null ? catalog.RecentHistorySize : 3);
            nextAt = sequence.NextInterval(catalog != null ? catalog.MinimumIntervalSeconds : 45f,
                catalog != null ? catalog.MaximumIntervalSeconds : 90f);
            BuildHint(parent);
        }

        public void Step(float speed01, float deltaTime)
        {
            if (!preferences.gentleInteractionsEnabled || sequence == null) { ClearActive(); return; }
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            if (active != null)
            {
                opportunityTime += dt;
                bool show = preferences.gentleHintsEnabled && opportunityTime <= active.hintSeconds;
                if (hintRoot != null) hintRoot.gameObject.SetActive(show);
                bool nearby = world != null && world.HasVisibleScenery(active.sceneryIdContains);
                if (opportunityTime >= active.opportunitySeconds && (!nearby || opportunityTime >= active.opportunitySeconds + 15f)) ClearActive();
                return;
            }
            if (speed01 <= 0.04f) return;
            movingTimer += dt;
            if (movingTimer < nextAt) return;
            RouteSegmentType segment = world != null && world.CurrentSegment != null ? world.CurrentSegment.Type : RouteSegmentType.Meadow;
            WeatherType weather = journey != null ? journey.Weather : WeatherType.Clear;
            active = sequence.Next(item => item.Supports(segment, weather, speed01) &&
                IsContextVisible(item.sceneryIdContains) &&
                (!item.requiresDispatcherPair || audioService != null && audioService.HasInteractionAudio(item.id, true)));
            movingTimer = 0f;
            nextAt = sequence.NextInterval(catalog != null ? catalog.MinimumIntervalSeconds : 45f,
                catalog != null ? catalog.MaximumIntervalSeconds : 90f);
            if (active == null) return;
            opportunityTime = 0f;
            UpdateHint(active);
        }

        public bool NotifyControlActivated(CabControlAction action)
        {
            if (active == null || active.requiredControl != action) return false;
            bool reacted = world != null && world.TryReactToScenery(active.sceneryIdContains, active.reaction, 3f);
            if (!reacted && journey != null) journey.TryReactToActiveEvent(active.sceneryIdContains, active.reaction, 3f);
            if (active.reaction == CabInteractionReaction.ClearWindow) journey?.SetWindowHeater(true);
            if (!active.requiresDispatcherPair) audioService?.PlayInteractionReaction(active.id);
            ReactionTriggered?.Invoke(active);
            ClearActive();
            return true;
        }

        private bool IsContextVisible(string idFragment)
        {
            if (string.IsNullOrWhiteSpace(idFragment)) return true;
            return world != null && world.HasVisibleScenery(idFragment) ||
                   journey != null && journey.HasActiveEvent(idFragment);
        }

        private void BuildHint(RectTransform parent)
        {
            if (parent == null) return;
            hintRoot = UiFactory.Panel("GentleInteractionHint", parent, new Color(0.04f, 0.11f, 0.14f, 0.94f), UiFactory.RoundedSprite());
            hintRoot.anchorMin = hintRoot.anchorMax = new Vector2(0.5f, 0.82f);
            hintRoot.pivot = new Vector2(0.5f, 0.5f);
            hintRoot.sizeDelta = new Vector2(112f, 112f);
            hintRoot.anchoredPosition = Vector2.zero;
            hintRoot.GetComponent<Image>().raycastTarget = false;
            Outline outline = hintRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.24f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            hintImage = UiFactory.Image("Icon", hintRoot, null, Color.white, true);
            UiFactory.Stretch(hintImage.rectTransform, 18f, 18f, 18f, 18f);
            hintImage.raycastTarget = false;
            hintGlyph = UiFactory.Label("Glyph", hintRoot, "●", 52, new Color(0.66f, 0.94f, 1f, 1f), TextAlignmentOptions.Center, UiFontRole.Control);
            UiFactory.Stretch(hintGlyph.rectTransform, 8f, 8f, 8f, 8f);
            hintRoot.gameObject.SetActive(false);
        }

        private void UpdateHint(CabInteractionDefinition item)
        {
            if (hintRoot == null) return;
            bool hasSprite = item.hintIcon != null;
            hintImage.sprite = item.hintIcon;
            hintImage.gameObject.SetActive(hasSprite);
            hintGlyph.gameObject.SetActive(!hasSprite);
            hintGlyph.text = string.IsNullOrWhiteSpace(item.fallbackIcon) ? "●" : item.fallbackIcon;
            hintRoot.gameObject.SetActive(preferences.gentleHintsEnabled);
        }

        private void ClearActive()
        {
            active = null;
            opportunityTime = 0f;
            if (hintRoot != null) hintRoot.gameObject.SetActive(false);
        }
    }
}
