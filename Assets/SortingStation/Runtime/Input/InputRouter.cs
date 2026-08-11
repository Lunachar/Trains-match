using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SortingStation
{
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action<Vector2> MovePressed;
        public event Action SubmitPressed;
        public event Action CancelPressed;

        private float lastAcceptedTime = -10f;
        private float cooldown = 0.18f;

        public void SetCooldown(float seconds) => cooldown = Mathf.Clamp(seconds, 0.05f, 0.8f);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Time.unscaledTime - lastAcceptedTime < cooldown)
            {
                return;
            }

            Vector2 direction = Vector2.zero;
            if (keyboard.leftArrowKey.wasPressedThisFrame) direction = Vector2.left;
            else if (keyboard.rightArrowKey.wasPressedThisFrame) direction = Vector2.right;
            else if (keyboard.upArrowKey.wasPressedThisFrame) direction = Vector2.up;
            else if (keyboard.downArrowKey.wasPressedThisFrame) direction = Vector2.down;

            if (direction != Vector2.zero)
            {
                lastAcceptedTime = Time.unscaledTime;
                MovePressed?.Invoke(direction);
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                lastAcceptedTime = Time.unscaledTime;
                SubmitPressed?.Invoke();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                lastAcceptedTime = Time.unscaledTime;
                CancelPressed?.Invoke();
            }
        }
    }
}
