using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation.EditorTools
{
    [CustomEditor(typeof(CabRideEditableLayout))]
    public sealed class CabRideEditableLayoutEditor : Editor
    {
        private static readonly Color NormalColor = new Color(0.05f, 0.20f, 0.22f, 0.72f);
        private static readonly Color MomentaryColor = new Color(0.16f, 0.16f, 0.09f, 0.78f);
        private static readonly Color BrakeColor = new Color(0.42f, 0.09f, 0.07f, 0.82f);
        private static readonly Color RadioColor = new Color(0.02f, 0.08f, 0.10f, 0.88f);
        private static readonly Color ThrottleColor = new Color(0.02f, 0.16f, 0.13f, 0.82f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CabRideEditableLayout layout = (CabRideEditableLayout)target;
            CabRideDefinition ride = ResolveRideDefinition(layout);

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Эта сцена — редактор раскладки. Реальная CabRide-сцена читает координаты из CabRideDefinition.asset. " +
                "После ручного перемещения нажмите «Сохранить позиции…», иначе изменения останутся только в preview-сцене.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(ride == null))
            {
                if (GUILayout.Button("Пересобрать все 11 органов управления из CabRideDefinition"))
                {
                    RebuildControlRects(layout, ride);
                }

                if (GUILayout.Button("Сохранить позиции в CabRideDefinition"))
                {
                    SaveControlRects(layout, ride);
                }
            }

            if (ride == null)
            {
                EditorGUILayout.HelpBox("Не найден Resources/Configuration/CabRideDefinition.asset.", MessageType.Warning);
            }
        }

        private static CabRideDefinition ResolveRideDefinition(CabRideEditableLayout layout)
        {
            if (layout.RideDefinition != null) return layout.RideDefinition;
            CabRideDefinition ride = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            if (ride != null) layout.SetRideDefinitionForEditor(ride);
            return ride;
        }

        private static void RebuildControlRects(CabRideEditableLayout layout, CabRideDefinition ride)
        {
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Rebuild cab control preview");
            RectTransform root = EnsureRootRect(layout);
            RemoveLegacyControlBlocks(root);

            CabControlBinding[] controls = ride.Controls;
            for (int i = 0; i < controls.Length; i++)
            {
                CabControlBinding binding = controls[i];
                if (binding == null) continue;
                RectTransform rect = EnsureControlRect(root, binding);
                ApplyBindingRect(rect, binding, layout.ReferenceResolution);
                ApplyVisual(rect, binding);
            }

            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        }

        private static void SaveControlRects(CabRideEditableLayout layout, CabRideDefinition ride)
        {
            Undo.RecordObject(ride, "Save cab control layout");
            RectTransform root = EnsureRootRect(layout);
            int saved = 0;

            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == root || !TryParseAction(child.name, out CabControlAction action)) continue;
                Rect normalized = CalculateNormalizedRect(root, child);
                ride.SetBindingLayoutForEditor(action, normalized.center, normalized.size);
                saved++;
            }

            EditorUtility.SetDirty(ride);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Debug.Log($"CabRideLayoutPreview: saved {saved} control rectangles to CabRideDefinition.asset");
        }

        private static RectTransform EnsureRootRect(CabRideEditableLayout layout)
        {
            RectTransform root = layout.GetComponent<RectTransform>();
            if (root == null) root = layout.gameObject.AddComponent<RectTransform>();
            Vector2 resolution = layout.ReferenceResolution;
            if (resolution.x < 100f || resolution.y < 100f) resolution = new Vector2(1500f, 1000f);
            root.localScale = Vector3.one;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = resolution;
            return root;
        }

        private static RectTransform EnsureControlRect(RectTransform root, CabControlBinding binding)
        {
            string objectName = CabRideEditableLayout.ControlObjectName(binding.action, binding.label);
            Transform existing = root.Find(objectName);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Create cab control preview");
                go.transform.SetParent(root, false);
            }
            else
            {
                go = existing.gameObject;
                if (go.GetComponent<Image>() == null) go.AddComponent<Image>();
                if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
            }
            return (RectTransform)go.transform;
        }

        private static void ApplyBindingRect(RectTransform rect, CabControlBinding binding, Vector2 referenceResolution)
        {
            rect.anchorMin = rect.anchorMax = binding.normalizedCenter;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                Mathf.Max(24f, binding.normalizedSize.x * referenceResolution.x),
                Mathf.Max(24f, binding.normalizedSize.y * referenceResolution.y));
        }

        private static void ApplyVisual(RectTransform rect, CabControlBinding binding)
        {
            Image image = rect.GetComponent<Image>();
            image.color = binding.action == CabControlAction.Brake ? BrakeColor :
                binding.action == CabControlAction.Radio ? RadioColor :
                binding.action == CabControlAction.Throttle ? ThrottleColor :
                binding.momentary ? MomentaryColor : NormalColor;
            image.raycastTarget = true;

            TextMeshProUGUI label = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(labelObject, "Create cab control preview label");
                labelObject.transform.SetParent(rect, false);
                label = labelObject.GetComponent<TextMeshProUGUI>();
            }

            label.text = $"{binding.label}\n{binding.action}";
            label.fontSize = binding.action == CabControlAction.Radio || binding.action == CabControlAction.Throttle ? 22f : 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);
        }

        private static void RemoveLegacyControlBlocks(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == CabRideEditableLayout.CabFrameName ||
                    child.name == "WindowWorld_можно_двигать" ||
                    child.name == "InfoScreens_не_перекрывать" ||
                    child.name.StartsWith(CabRideEditableLayout.ControlPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (child.name.EndsWith(CabRideEditableLayout.MovableSuffix, StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static bool TryParseAction(string name, out CabControlAction action)
        {
            action = default;
            if (!name.StartsWith(CabRideEditableLayout.ControlPrefix, StringComparison.Ordinal)) return false;
            int start = CabRideEditableLayout.ControlPrefix.Length;
            int end = name.IndexOf('_', start);
            if (end < 0) return false;
            string actionName = name.Substring(start, end - start);
            return Enum.TryParse(actionName, out action);
        }

        private static Rect CalculateNormalizedRect(RectTransform root, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Rect rootRect = root.rect;
            if (rootRect.width <= 0.01f || rootRect.height <= 0.01f)
            {
                rootRect = new Rect(-750f, -500f, 1500f, 1000f);
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = root.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            Vector2 normalizedMin = new Vector2(
                Mathf.InverseLerp(rootRect.xMin, rootRect.xMax, min.x),
                Mathf.InverseLerp(rootRect.yMin, rootRect.yMax, min.y));
            Vector2 normalizedMax = new Vector2(
                Mathf.InverseLerp(rootRect.xMin, rootRect.xMax, max.x),
                Mathf.InverseLerp(rootRect.yMin, rootRect.yMax, max.y));
            normalizedMin = Vector2.Max(Vector2.zero, normalizedMin);
            normalizedMax = Vector2.Min(Vector2.one, normalizedMax);
            return Rect.MinMaxRect(normalizedMin.x, normalizedMin.y, normalizedMax.x, normalizedMax.y);
        }
    }
}
