using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class WagonView : MonoBehaviour
    {
        public int PieceId { get; private set; }
        public MatchToken Token { get; private set; }
        public AccessibleButton Button { get; private set; }

        public static WagonView Create(Transform parent, AccessibleFocusGroup group, BoardPiece piece, Vector2 normalizedPosition,
            Vector2 normalizedSize, System.Action<int> selected)
        {
            AppServices services = AppServices.Ensure();
            Color body = piece.Token.Kind == MatchTokenKind.Color ? piece.Token.Color : new Color(0.24f, 0.43f, 0.55f, 1f);
            string label = piece.Token.Kind == MatchTokenKind.Color
                ? piece.Token.Symbol + "\n" + piece.Token.Label
                : piece.Token.Label;
            AccessibleButton button = UiFactory.Button("Wagon_" + piece.Id, parent, group, label, body,
                services.Settings.SelectedColor, () => selected(piece.Id), piece.Token.Kind == MatchTokenKind.Color ? 27 : 58);
            UiFactory.SetRect(button.RectTransform, normalizedPosition - normalizedSize * 0.5f,
                normalizedPosition + normalizedSize * 0.5f, Vector2.zero, Vector2.zero);
            button.RectTransform.pivot = new Vector2(0.5f, 0.5f);

            Image background = button.GetComponent<Image>();
            if (services.Visuals.wagon != null)
            {
                background.sprite = services.Visuals.wagon;
                background.preserveAspect = false;
                background.color = Color.Lerp(body, Color.white, 0.18f);
            }

            CreateWheel(button.transform, new Vector2(0.25f, -0.08f));
            CreateWheel(button.transform, new Vector2(0.75f, -0.08f));

            WagonView view = button.gameObject.AddComponent<WagonView>();
            view.PieceId = piece.Id;
            view.Token = piece.Token;
            view.Button = button;
            return view;
        }

        public void SetSelected(bool selected) => Button.SetSelected(selected);

        private static void CreateWheel(Transform parent, Vector2 anchor)
        {
            RectTransform wheel = UiFactory.Panel("Wheel", parent, new Color(0.035f, 0.045f, 0.05f, 1f));
            wheel.anchorMin = anchor;
            wheel.anchorMax = anchor;
            wheel.pivot = new Vector2(0.5f, 0.5f);
            wheel.sizeDelta = new Vector2(30f, 30f);
            wheel.anchoredPosition = Vector2.zero;
            wheel.SetAsFirstSibling();
        }
    }
}
