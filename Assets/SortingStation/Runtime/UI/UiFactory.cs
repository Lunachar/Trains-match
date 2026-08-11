using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortingStation
{
    public enum UiFontRole
    {
        Body,
        Control,
        Display
    }

    public static class UiFactory
    {
        private static TMP_FontAsset cachedRegularFont;
        private static TMP_FontAsset cachedMediumFont;
        private static TMP_FontAsset cachedBoldFont;
        private static TMP_FontAsset cachedSymbolFont;
        private static Sprite cachedRoundedSprite;

        public static RectTransform CreateScreen(string name, out Canvas canvas)
        {
            EnsureEventSystem();
            AppSettings settings = AppServices.Ensure().Settings;
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = settings.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = Panel("SafeArea", canvasObject.transform, Color.clear);
            Stretch(root);
            root.gameObject.AddComponent<SafeAreaFitter>();
            return root;
        }

        public static RectTransform Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
            return go.GetComponent<RectTransform>();
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool preserveAspect = true)
        {
            RectTransform rect = Panel(name, parent, color, sprite);
            Image image = rect.GetComponent<Image>();
            image.preserveAspect = preserveAspect;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, int fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, UiFontRole fontRole = UiFontRole.Body)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            SetFontRole(label, fontRole);
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static void SetFontRole(TMP_Text label, UiFontRole role)
        {
            if (label == null) return;
            TMP_FontAsset font = FontForRole(role);
            if (font != null) label.font = font;
            label.fontStyle = role == UiFontRole.Display ? FontStyles.Bold : FontStyles.Normal;
        }

        public static AccessibleButton Button(
            string name,
            Transform parent,
            AccessibleFocusGroup group,
            string text,
            Color normal,
            Color selected,
            Action activated,
            int fontSize = 38)
        {
            AppSettings settings = AppServices.Instance.Settings;
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow), typeof(AccessibleButton));
            go.transform.SetParent(parent, false);
            Image background = go.GetComponent<Image>();
            background.color = normal;
            background.sprite = RoundedSprite();
            background.type = UnityEngine.UI.Image.Type.Sliced;
            Shadow shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;

            Color normalText = settings.TextForBackground(normal);
            Color selectedText = settings.TextForBackground(selected);
            TextMeshProUGUI label = Label("Label", go.transform, text, fontSize, normalText,
                TextAlignmentOptions.Center, UiFontRole.Control);
            Stretch(label.rectTransform, 18f, 14f, 18f, 14f);
            AccessibleButton button = go.GetComponent<AccessibleButton>();
            button.Initialize(group, label, text, normal, selected, settings.FocusColor, settings.PressedScale,
                normalText, selectedText, activated);
            return button;
        }

        public static Sprite RoundedSprite()
        {
            if (cachedRoundedSprite != null) return cachedRoundedSprite;
            const int size = 64;
            const float radius = 18f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Runtime Rounded Surface",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            Vector2 half = new Vector2(size * 0.5f - radius, size * 0.5f - radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y));
                    Vector2 q = new Vector2(Mathf.Max(p.x - half.x, 0f), Mathf.Max(p.y - half.y, 0f));
                    float distance = q.magnitude - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            cachedRoundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0u, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
            cachedRoundedSprite.name = "Runtime Rounded Surface";
            cachedRoundedSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return cachedRoundedSprite;
        }

        public static void StyleSurface(RectTransform rect, bool elevated = true)
        {
            if (rect == null) return;
            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = RoundedSprite();
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            if (!elevated || rect.GetComponent<Shadow>() != null) return;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;
        }

        public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static TMP_FontAsset FontForRole(UiFontRole role)
        {
            switch (role)
            {
                case UiFontRole.Display:
                    return cachedBoldFont ??= CreateRuntimeFont("Roboto-Bold", "Roboto-Bold SDF", "Bold");
                case UiFontRole.Control:
                    return cachedMediumFont ??= CreateRuntimeFont("Roboto-Medium", "Roboto-Bold SDF", "Medium");
                default:
                    return cachedRegularFont ??= CreateRuntimeFont("Roboto-Regular", "Roboto-Bold SDF", "Regular");
            }
        }

        private static TMP_FontAsset CreateRuntimeFont(string sourceName, string fallbackName, string role)
        {
            Font sourceFont = Resources.Load<Font>("Fonts/" + sourceName);
            if (sourceFont != null)
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(sourceFont);
                asset.name = "Sorting Station " + role + " Runtime Font";
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                TMP_FontAsset symbols = SymbolFont();
                if (symbols != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { symbols };
                return asset;
            }
            return Resources.Load<TMP_FontAsset>("Fonts/" + fallbackName);
        }

        private static TMP_FontAsset SymbolFont()
        {
            if (cachedSymbolFont != null) return cachedSymbolFont;
            Font sourceFont = Resources.Load<Font>("Fonts/LiberationSans");
            if (sourceFont == null) return null;
            cachedSymbolFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            cachedSymbolFont.name = "Sorting Station Symbol Fallback Font";
            cachedSymbolFont.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return cachedSymbolFont;
        }
    }
}
