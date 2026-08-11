using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortingStation
{
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Outline))]
    public sealed class AccessibleButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Image background;
        private Graphic stateGraphic;
        private Outline focusOutline;
        private TMP_Text label;
        private AccessibleFocusGroup group;
        private Action activated;
        private Color idleColor;
        private Color selectedColor;
        private Color disabledColor;
        private Color idleTextColor = Color.white;
        private Color selectedTextColor = Color.white;
        private float pressedScale = 0.96f;
        private bool latchSelectedTransform;
        private float selectedOffset = 5f;
        private Vector2 restingAnchoredPosition;
        private bool focused;
        private bool selected;
        private bool pressed;
        private bool dragged;

        public event Action<PointerEventData> PointerPressed;
        public event Action<PointerEventData> PointerReleased;
        public event Action<PointerEventData> PointerDragged;

        public RectTransform RectTransform => (RectTransform)transform;
        public bool Interactable { get; private set; } = true;
        public string AccessibleName { get; private set; }
        public TMP_Text Label => label;

        public void Initialize(
            AccessibleFocusGroup focusGroup,
            TMP_Text buttonLabel,
            string accessibleName,
            Color normalColor,
            Color activeColor,
            Color focusColor,
            float pressScale,
            Color normalTextColor,
            Color activeTextColor,
            Action onActivated)
        {
            background = GetComponent<Image>();
            stateGraphic = background;
            focusOutline = GetComponent<Outline>();
            label = buttonLabel;
            group = focusGroup;
            activated = onActivated;
            AccessibleName = accessibleName ?? string.Empty;
            idleColor = normalColor;
            selectedColor = activeColor;
            disabledColor = Color.Lerp(normalColor, Color.gray, 0.62f);
            idleTextColor = normalTextColor;
            selectedTextColor = activeTextColor;
            pressedScale = Mathf.Clamp(pressScale, 0.95f, 1f);
            restingAnchoredPosition = RectTransform.anchoredPosition;
            background.color = idleColor;
            focusOutline.effectColor = focusColor;
            focusOutline.effectDistance = new Vector2(6f, -6f);
            focusOutline.useGraphicAlpha = false;
            focusOutline.enabled = false;
            group?.Register(this);
        }

        public void Activate()
        {
            if (!Interactable) return;
            AppServices.Instance?.Audio.Play(SoundCue.Tap);
            activated?.Invoke();
        }

        public void SetFocused(bool value)
        {
            focused = value;
            if (focusOutline != null) focusOutline.enabled = value;
            RefreshVisual();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            RefreshVisual();
        }

        public void ConfigurePersistentPress(bool latchWhenSelected, float downwardOffset = 5f)
        {
            latchSelectedTransform = latchWhenSelected;
            selectedOffset = Mathf.Clamp(downwardOffset, 0f, 10f);
            restingAnchoredPosition = RectTransform.anchoredPosition;
            RefreshTransform();
        }

        public void SetInteractable(bool value)
        {
            Interactable = value;
            if (!value)
            {
                pressed = false;
            }
            RefreshVisual();
        }

        public void SetLabel(string value)
        {
            if (label != null) label.text = value;
            AccessibleName = value ?? AccessibleName;
        }

        public void SetAccessibleName(string value)
        {
            AccessibleName = value ?? string.Empty;
        }

        public void SetIdleColor(Color value)
        {
            idleColor = value;
            RefreshVisual();
        }

        /// <summary>
        /// Uses a smaller child graphic for visual state while the full transparent
        /// rectangle remains available as the accessible hit target and focus ring.
        /// </summary>
        public void SetStateGraphic(Graphic value)
        {
            stateGraphic = value != null ? value : background;
            if (stateGraphic != background) background.color = Color.clear;
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragged) return;
            group?.SetFocus(this, false);
            Activate();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            dragged = false;
            pressed = true;
            RefreshTransform();
            PointerPressed?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            RefreshTransform();
            PointerReleased?.Invoke(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            dragged = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            PointerDragged?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            pressed = false;
            RefreshTransform();
            PointerReleased?.Invoke(eventData);
        }

        public void OnPointerEnter(PointerEventData eventData) => group?.SetFocus(this, false);

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pressed)
            {
                pressed = false;
                RefreshTransform();
                PointerReleased?.Invoke(eventData);
            }
        }

        private void OnDisable()
        {
            group?.Unregister(this);
        }

        private void RefreshVisual()
        {
            if (background == null) return;
            Color stateColor = !Interactable ? disabledColor : selected ? selectedColor : idleColor;
            if (stateGraphic == null) stateGraphic = background;
            stateGraphic.color = stateColor;
            if (stateGraphic != background) background.color = Color.clear;
            if (label != null)
            {
                label.alpha = Interactable ? 1f : 0.55f;
                label.color = selected ? selectedTextColor : idleTextColor;
            }
            if (focused && focusOutline != null) focusOutline.enabled = true;
            RefreshTransform();
        }

        private void RefreshTransform()
        {
            MotionLevel motion = AppServices.Instance != null ? AppServices.Instance.Preferences.motionLevel : MotionLevel.Normal;
            bool latched = Interactable && selected && latchSelectedTransform;
            bool transient = Interactable && pressed && motion == MotionLevel.Normal;
            bool visuallyPressed = latched || transient;
            transform.localScale = visuallyPressed ? Vector3.one * pressedScale : Vector3.one;
            float offset = latched ? selectedOffset : transient ? selectedOffset * 0.45f : 0f;
            RectTransform.anchoredPosition = restingAnchoredPosition + Vector2.down * offset;
        }
    }
}
