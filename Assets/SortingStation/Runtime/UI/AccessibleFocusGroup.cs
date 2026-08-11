using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortingStation
{
    public sealed class AccessibleFocusGroup : MonoBehaviour
    {
        private readonly List<AccessibleButton> buttons = new List<AccessibleButton>();
        private AccessibleButton current;
        private InputRouter input;
        private bool subscribed;

        public event Action Cancelled;
        public Func<Vector2, bool> MoveInterceptor { get; set; }
        public AccessibleButton Current => current;

        private void Start()
        {
            Subscribe();
            FocusFirst();
        }

        private void OnEnable()
        {
            Subscribe();
            if (current == null) FocusFirst();
        }

        private void OnDisable() => Unsubscribe();

        public void Register(AccessibleButton button)
        {
            if (button == null || buttons.Contains(button)) return;
            buttons.Add(button);
            if (current == null && button.Interactable) SetFocus(button, false);
        }

        public void Unregister(AccessibleButton button)
        {
            if (button == null) return;
            buttons.Remove(button);
            if (current == button)
            {
                current.SetFocused(false);
                current = null;
                FocusFirst();
            }
        }

        public void SetFocus(AccessibleButton button, bool playSound = true)
        {
            if (button == null || !button.Interactable || !button.isActiveAndEnabled) return;
            if (current == button) return;
            current?.SetFocused(false);
            current = button;
            current.SetFocused(true);
            if (playSound) AppServices.Instance?.Audio.Play(SoundCue.Focus);
        }

        public void FocusFirst()
        {
            AccessibleButton first = buttons.FirstOrDefault(button => button != null && button.isActiveAndEnabled && button.Interactable);
            if (first != null) SetFocus(first, false);
        }

        public void FocusNearestTo(Vector2 screenPosition)
        {
            AccessibleButton next = buttons
                .Where(button => button != null && button.isActiveAndEnabled && button.Interactable)
                .OrderBy(button => Vector2.SqrMagnitude((Vector2)button.RectTransform.position - screenPosition))
                .FirstOrDefault();
            if (next != null) SetFocus(next);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            input = AppServices.Ensure().Input;
            if (input == null) return;
            input.MovePressed += OnMove;
            input.SubmitPressed += OnSubmit;
            input.CancelPressed += OnCancel;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || input == null) return;
            input.MovePressed -= OnMove;
            input.SubmitPressed -= OnSubmit;
            input.CancelPressed -= OnCancel;
            subscribed = false;
        }

        private void OnMove(Vector2 direction)
        {
            if (MoveInterceptor != null && MoveInterceptor(direction)) return;
            List<AccessibleButton> active = buttons
                .Where(button => button != null && button.isActiveAndEnabled && button.Interactable)
                .ToList();
            if (active.Count == 0) return;
            int currentIndex = current != null ? active.IndexOf(current) : -1;
            List<Vector2> positions = active.Select(button => (Vector2)button.RectTransform.position).ToList();
            int nextIndex = SpatialNavigation.FindNextIndex(positions, currentIndex, direction);
            if (nextIndex >= 0 && nextIndex < active.Count) SetFocus(active[nextIndex]);
        }

        private void OnSubmit() => current?.Activate();
        private void OnCancel() => Cancelled?.Invoke();
    }
}
