using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortingStation
{
    [RequireComponent(typeof(Image))]
    public sealed class HoldToOpenButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float holdSeconds = 2f;
        private float heldSince;
        private bool holding;
        private bool fired;
        private Image fill;
        private TMP_Text label;
        private Action completed;

        public void Initialize(float seconds, TMP_Text text, Image progressFill, Action onCompleted)
        {
            holdSeconds = Mathf.Max(0.5f, seconds);
            label = text;
            fill = progressFill;
            completed = onCompleted;
            if (fill != null) fill.fillAmount = 0f;
        }

        private void Update()
        {
            if (!holding || fired) return;
            float progress = Mathf.Clamp01((Time.unscaledTime - heldSince) / holdSeconds);
            if (fill != null) fill.fillAmount = progress;
            if (label != null) label.text = progress < 1f ? "Настройки\nудерживайте" : "Открываю…";
            if (progress >= 1f)
            {
                fired = true;
                holding = false;
                completed?.Invoke();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            holding = true;
            fired = false;
            heldSince = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData) => ResetHold();
        public void OnPointerExit(PointerEventData eventData) => ResetHold();

        private void ResetHold()
        {
            holding = false;
            if (fill != null) fill.fillAmount = 0f;
            if (label != null) label.text = "Настройки\n(удерживать)";
        }
    }
}
