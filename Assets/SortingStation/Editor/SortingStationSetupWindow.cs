using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SortingStation.EditorTools
{
    public sealed class SortingStationSetupWindow : EditorWindow
    {
        private enum Section
        {
            General,
            Cab,
            Environment,
            Audio,
            Levels
        }

        private readonly Dictionary<int, UnityEditor.Editor> editors = new Dictionary<int, UnityEditor.Editor>();
        private readonly List<string> validationIssues = new List<string>();
        private Section section;
        private Vector2 scroll;
        private bool validationRun;

        [MenuItem("Sorting Station/Project Setup", priority = 0)]
        public static void Open()
        {
            SortingStationSetupWindow window = GetWindow<SortingStationSetupWindow>("Поезда — настройка");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnDisable()
        {
            ClearEditors();
        }

        private void OnGUI()
        {
            DrawHeader();
            section = (Section)GUILayout.Toolbar((int)section,
                new[] { "Общие", "Кабина", "Окружение", "Звук и радио", "Уровни" }, GUILayout.Height(30f));
            EditorGUILayout.Space(6f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (section)
            {
                case Section.General:
                    DrawAsset("Общие параметры и доступность", Load<AppSettings>("AppSettings"));
                    DrawAsset("Основная графика", Load<VisualCatalog>("VisualCatalog"));
                    break;
                case Section.Cab:
                    EditorGUILayout.HelpBox("Здесь настраиваются скорость, плавность, свет, брелок, клавиши, изображения и координаты всех восьми приборов.", MessageType.Info);
                    DrawAsset("Живая кабина", Load<CabRideDefinition>("CabRideDefinition"));
                    break;
                case Section.Environment:
                    EditorGUILayout.HelpBox("Перетаскивайте новые спрайты в каталог, выбирайте поддерживаемые типы маршрута и регулируйте размеры, слои, плотность, вес и запрет повторов.", MessageType.Info);
                    DrawAsset("Каталог окружения", Load<CabSceneryCatalog>("CabSceneryCatalog"));
                    foreach (RouteSegmentDefinition route in FindAssets<RouteSegmentDefinition>("Assets/SortingStation/Data/CabRoutes"))
                    {
                        DrawAsset("Сегмент: " + route.DisplayName, route);
                    }
                    break;
                case Section.Audio:
                    EditorGUILayout.HelpBox("Все поля поддерживают обычное перетаскивание AudioClip из окна Project. Добавьте несколько файлов в плейлист радио или в variants нужного события.", MessageType.Info);
                    DrawAsset("Музыка, радио, рельсы и эффекты", Load<AudioCatalog>("AudioCatalog"));
                    break;
                case Section.Levels:
                    DrawAsset("Каталог режимов и уровней", Load<GameCatalog>("GameCatalog"));
                    foreach (LevelDefinition level in FindAssets<LevelDefinition>("Assets/SortingStation/Data/Levels"))
                    {
                        DrawAsset(level.Title + " — " + level.OptionCount, level);
                    }
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Поезда: Живая кабина", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 });
            EditorGUILayout.LabelField("Все игровые каталоги в одном окне. Изменения сохраняются как обычные Unity-ассеты.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Сохранить настройки", GUILayout.Height(30f)))
                {
                    AssetDatabase.SaveAssets();
                    ShowNotification(new GUIContent("Настройки сохранены"));
                }
                if (GUILayout.Button("Дополнить отсутствующие данные", GUILayout.Height(30f)))
                {
                    ProjectBootstrapper.BuildProject();
                    ClearEditors();
                    validationRun = false;
                }
                if (GUILayout.Button("Проверить проект", GUILayout.Height(30f))) RunValidation();
            }
            if (validationRun)
            {
                MessageType type = validationIssues.Count == 0 ? MessageType.Info : MessageType.Warning;
                string message = validationIssues.Count == 0
                    ? "Проверка пройдена: обязательные ресурсы и параметры назначены."
                    : "Найдено проблем: " + validationIssues.Count + "\n• " + string.Join("\n• ", validationIssues.Take(8));
                EditorGUILayout.HelpBox(message, type);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAsset(string title, Object asset)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("Показать", GUILayout.Width(78f))) EditorGUIUtility.PingObject(asset);
                }
            }
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Ассет отсутствует. Нажмите «Дополнить отсутствующие данные».", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            int id = asset.GetInstanceID();
            if (!editors.TryGetValue(id, out UnityEditor.Editor editor) || editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(asset);
                editors[id] = editor;
            }
            editor.OnInspectorGUI();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
        }

        private void RunValidation()
        {
            validationIssues.Clear();
            validationIssues.AddRange(ProjectValidator.FindIssues());
            validationRun = true;
            Repaint();
        }

        private void ClearEditors()
        {
            foreach (UnityEditor.Editor editor in editors.Values)
            {
                if (editor != null) DestroyImmediate(editor);
            }
            editors.Clear();
        }

        private static T Load<T>(string name) where T : Object
        {
            return Resources.Load<T>("Configuration/" + name);
        }

        private static IEnumerable<T> FindAssets<T>(string folder) where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .OrderBy(asset => asset.name);
        }
    }
}
