using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public static class RailPathView
    {
        public static void CreateFanPath(RectTransform parent, Vector2 start, Vector2 end, Color railColor, Color sleeperColor)
        {
            Vector2 elbow = new Vector2(Mathf.Lerp(start.x, end.x, 0.22f), end.y);
            CreateSegment(parent, start, elbow, railColor, sleeperColor);
            CreateSegment(parent, elbow, end, railColor, sleeperColor);
        }

        private static void CreateSegment(RectTransform parent, Vector2 from, Vector2 to, Color railColor, Color sleeperColor)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1f) return;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;

            CreateBar(parent, "Rail", from + normal * 12f, to + normal * 12f, 6f, railColor);
            CreateBar(parent, "Rail", from - normal * 12f, to - normal * 12f, 6f, railColor);

            int sleepers = Mathf.Max(2, Mathf.FloorToInt(length / 46f));
            for (int i = 0; i <= sleepers; i++)
            {
                float t = (float)i / sleepers;
                Vector2 center = Vector2.Lerp(from, to, t);
                RectTransform sleeper = UiFactory.Panel("Sleeper", parent, sleeperColor);
                sleeper.anchorMin = Vector2.zero;
                sleeper.anchorMax = Vector2.zero;
                sleeper.pivot = new Vector2(0.5f, 0.5f);
                sleeper.anchoredPosition = center;
                sleeper.sizeDelta = new Vector2(8f, 48f);
                sleeper.localRotation = Quaternion.Euler(0f, 0f, angle);
                sleeper.SetAsFirstSibling();
            }
        }

        private static void CreateBar(RectTransform parent, string name, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            RectTransform bar = UiFactory.Panel(name, parent, color);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.zero;
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = (from + to) * 0.5f;
            bar.sizeDelta = new Vector2(delta.magnitude, width);
            bar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            bar.SetAsFirstSibling();
        }
    }
}
