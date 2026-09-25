using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SortingStation.EditorTools
{
    [CustomEditor(typeof(GameCatalog))]
    public sealed class GameCatalogInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate all levels", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
            EditorGUILayout.HelpBox("Add future word levels by using MatchTokenKind.Text. No runtime code change is required.", MessageType.Info);
        }
    }

    [CustomEditor(typeof(AudioCatalog))]
    public sealed class AudioCatalogInspector : Editor
    {
        private ReorderableList radioPlaylist;

        private void OnEnable()
        {
            SerializedProperty playlist = serializedObject.FindProperty("cabRadioPlaylist");
            radioPlaylist = new ReorderableList(serializedObject, playlist, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Плейлист радио — треки можно менять местами"),
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    rect.y += 3f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, playlist.GetArrayElementAtIndex(index), new GUIContent($"Трек {index + 1}"));
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Перетащите аудиофайлы в отдельные слоты. Плейлист радио переключает трек при каждом новом включении; скорость поезда не меняет громкость или высоту музыки. Варианты события выбираются без немедленного повтора.", MessageType.Info);
            EditorGUILayout.LabelField("Музыка и петли", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("menuMusic"), new GUIContent("Музыка меню"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cabRadioMusic"), new GUIContent("Резервный трек радио"));
            radioPlaylist?.DoLayoutList();
            DrawRadioDropArea();
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Онлайн-радио", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Отдельная кнопка в плеере подключается к HLS-потоку через системный медиаплеер Android. Адрес страницы указан только как источник; для воспроизведения используется прямой URL трансляции. Интернет-радио и локальный плейлист взаимно отключаются.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onlineRadioEnabled"), new GUIContent("Разрешить онлайн-радио"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onlineRadioName"), new GUIContent("Название станции"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onlineRadioStreamUrl"), new GUIContent("Прямой URL потока"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onlineRadioSourceUrl"), new GUIContent("Страница-источник"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("railLoop"), new GUIContent("Шум рельсов (петля)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rainLoop"), new GUIContent("Дождь (петля)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("snowLoop"), new GUIContent("Мягкий снег (петля)"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Звуковые события", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("events"), new GUIContent("Банки вариантов"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Окружение во время поездки", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Добавьте несколько вариантов для каждого события: коровы, трактор, птицы, река, переезд, деревня, отдельный проезд города и другие объекты. Пустые банки работают бесшумно.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientEvents"), new GUIContent("События окружения"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Реплики диспетчера", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Записи заменяют системную речь для четырёх событий. Можно положить несколько вариантов каждой реплики.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dispatcherAnnouncements"), new GUIContent("Подключаемые реплики"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Связь с диспетчером", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Пары запускаются только кнопкой «Связь» в кабине. В каждой паре первый клип — реплика состава, второй — ответ диспетчера; текстовые поля служат подсказкой для записи.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dispatcherRadioPairs"), new GUIContent("Пары реплик связи"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Спокойные взаимодействия", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Для каждого события можно добавить звуки реакции. Для связи с диспетчером заполните обе группы: вызов и ответ. Пустая обязательная пара автоматически исключает событие.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cabInteractionAudio"), new GUIContent("Звуки взаимодействий"), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Открыть папку аудио", GUILayout.Height(34f)))
                {
                    Object folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/SortingStation/Audio/Imported");
                    if (folder != null) EditorGUIUtility.PingObject(folder);
                }
                if (GUILayout.Button("Убрать пустые слоты", GUILayout.Height(34f))) RemoveNullRadioTracks();
                if (GUILayout.Button("Проверить аудио", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
            }
            if (GUILayout.Button("Добавить все отсутствующие события и реплики", GUILayout.Height(34f)))
            {
                Undo.RecordObject(target, "Add ambient audio events");
                ((AudioCatalog)target).ConfigureAmbientDefaultsIfMissing();
                ((AudioCatalog)target).ConfigureDispatcherDefaultsIfMissing();
                ((AudioCatalog)target).ConfigureDispatcherRadioPairDefaultsIfMissing();
                CabInteractionDefinition[] definitions = CabInteractionCatalog.CreateDefaultInteractions();
                string[] ids = new string[definitions.Length];
                for (int i = 0; i < definitions.Length; i++) ids[i] = definitions[i].id;
                ((AudioCatalog)target).ConfigureInteractionDefaultsIfMissing(ids);
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
        }

        private void DrawRadioDropArea()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Перетащите сюда один или несколько треков радио", EditorStyles.helpBox);
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition) ||
                current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

            bool hasAudio = false;
            foreach (Object item in DragAndDrop.objectReferences)
            {
                if (item is AudioClip)
                {
                    hasAudio = true;
                    break;
                }
            }
            if (!hasAudio) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                SerializedProperty playlist = serializedObject.FindProperty("cabRadioPlaylist");
                foreach (Object item in DragAndDrop.objectReferences)
                {
                    if (!(item is AudioClip clip) || Contains(playlist, clip)) continue;
                    int index = playlist.arraySize;
                    playlist.InsertArrayElementAtIndex(index);
                    playlist.GetArrayElementAtIndex(index).objectReferenceValue = clip;
                }
                serializedObject.ApplyModifiedProperties();
            }
            current.Use();
        }

        private void RemoveNullRadioTracks()
        {
            SerializedProperty playlist = serializedObject.FindProperty("cabRadioPlaylist");
            for (int i = playlist.arraySize - 1; i >= 0; i--)
            {
                if (playlist.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    playlist.DeleteArrayElementAtIndex(i);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static bool Contains(SerializedProperty list, AudioClip clip)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == clip) return true;
            }
            return false;
        }
    }

    [CustomEditor(typeof(CabInteractionCatalog))]
    public sealed class CabInteractionCatalogInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Настройте частоту ненавязчивых событий, редкие остановки и условия каждой реакции. Значок показывается без текста и не требует ответа.", MessageType.Info);
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Восстановить стартовые 12 взаимодействий", GUILayout.Height(34f)))
            {
                Undo.RecordObject(target, "Reset cab interactions");
                ((CabInteractionCatalog)target).ConfigureDefaults();
                EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button("Проверить каталог", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
        }
    }

    [CustomEditor(typeof(AppSettings))]
    public sealed class AppSettingsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Group("Приложение", ("productName", "Название"), ("androidPackageName", "Android package"), ("referenceResolution", "Эталонное разрешение"));
            Group("Доступное управление", ("minimumTargetSize", "Минимальная цель касания"), ("targetGap", "Зазор между целями"), ("inputCooldown", "Защита от повторного ввода"), ("parentHoldSeconds", "Удержание родительской кнопки"));
            Group("Анимация интерфейса", ("wagonTravelSeconds", "Движение вагона, сек."), ("feedbackSeconds", "Обратная связь, сек."), ("pressedScale", "Масштаб при нажатии"));
            Group("Речь", ("speechLanguage", "Язык"), ("speechRate", "Скорость"), ("speechPitch", "Высота голоса"));
            Group("Цветовая тема", ("skyColor", "Небо"), ("groundColor", "Земля"), ("panelColor", "Панель"), ("panelAltColor", "Дополнительная панель"), ("surfaceColor", "Поверхность"), ("primaryColor", "Основное действие"), ("accentColor", "Акцент"), ("focusColor", "Фокус клавиатуры"), ("selectedColor", "Включено"), ("textColor", "Светлый текст"), ("mutedTextColor", "Вторичный текст"), ("textOnBrightColor", "Текст на ярком"), ("errorColor", "Ошибка"), ("brakeColor", "Тормоз"));
            Group("Размеры текста", ("displayFontSize", "Главное число"), ("titleFontSize", "Заголовок"), ("controlFontSize", "Органы управления"), ("statusFontSize", "Статус"), ("captionFontSize", "Подпись"));
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Проверить контраст и размеры", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
        }

        private void Group(string title, params (string property, string label)[] fields)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach ((string property, string label) field in fields)
            {
                SerializedProperty value = serializedObject.FindProperty(field.property);
                if (value != null) EditorGUILayout.PropertyField(value, new GUIContent(field.label));
            }
        }
    }

    [CustomEditor(typeof(VisualCatalog))]
    public sealed class VisualCatalogInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Replace any sprite here to reskin the game. Missing optional sprites use clean procedural shapes.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Validate visual catalog", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
        }
    }

    [CustomEditor(typeof(CabRideDefinition))]
    public sealed class CabRideDefinitionInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Тяга, торможение, перспектива, свет и координаты крупных приборов настраиваются здесь. Координаты указаны относительно изображения кабины.", MessageType.Info);
            serializedObject.Update();
            Group("Движение", ("maximumSpeedKph", "Максимальная скорость, км/ч"), ("accelerationSeconds", "Разгон до максимума, сек."), ("serviceBrakeSeconds", "Полное торможение, сек."), ("coastSeconds", "Выбег, сек."), ("rollingResistancePerSecond", "Сопротивление качению"), ("keyboardThrottleStep", "Шаг тяги с клавиатуры"), ("tractionCurve", "Кривая тяги"), ("brakingCurve", "Кривая торможения"));
            Group("Мир за окном", ("worldUnitsPerSecond", "Скорость движения мира"), ("routeSeed", "Seed маршрута"), ("horizon", "Высота горизонта"), ("perspectiveStrength", "Сила перспективы"));
            Group("Брелок", ("keychainAccelerationDegrees", "Реакция на разгон"), ("keychainRailDegrees", "Вибрация от рельсов"), ("keychainSmoothSeconds", "Затухание"));
            Group("Покачивание кабины", ("cabinSwayPixels", "Смещение, пиксели"), ("cabinSwayRotationDegrees", "Наклон, градусы"), ("cabinSwaySmoothSeconds", "Плавность"), ("androidCabinSwayMultiplier", "Усиление на Android"));
            Group("Освещение", ("headlightLandscapeAlpha", "Фары снаружи"), ("headlightTunnelAlpha", "Фары в туннеле"), ("cabinLightAlpha", "Свет кабины"), ("instrumentIdleAlpha", "Подсветка приборов"), ("instrumentCabinBoost", "Усиление приборов"), ("lightTransitionSeconds", "Плавность включения"));
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Органы управления", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("controls"), new GUIContent("Восемь приборов"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layers"), new GUIContent("Дополнительные слои (необязательно)"), true);
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Дополнить изображения", GUILayout.Height(34f))) ProjectBootstrapper.BuildProject();
                if (GUILayout.Button("Проверить управление", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
            }
        }

        private void Group(string title, params (string property, string label)[] fields)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach ((string property, string label) field in fields)
            {
                SerializedProperty value = serializedObject.FindProperty(field.property);
                if (value != null) EditorGUILayout.PropertyField(value, new GUIContent(field.label));
            }
        }

        public override bool HasPreviewGUI() => true;

        public override GUIContent GetPreviewTitle() => new GUIContent("Зоны управления кабины");

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            CabRideDefinition ride = (CabRideDefinition)target;
            CabSceneryCatalog scenery = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            Sprite overlay = scenery != null ? scenery.CabOverlay : null;
            EditorGUI.DrawRect(rect, new Color(0.035f, 0.055f, 0.065f, 1f));
            Rect imageRect = Fit(rect, 1.5f);
            if (overlay != null) GUI.DrawTexture(imageRect, overlay.texture, ScaleMode.ScaleToFit, true);

            CabControlBinding[] bindings = ride.Controls;
            for (int i = 0; i < bindings.Length; i++)
            {
                CabControlBinding binding = bindings[i];
                if (binding == null) continue;
                Rect zone = new Rect(
                    imageRect.x + (binding.normalizedCenter.x - binding.normalizedSize.x * 0.5f) * imageRect.width,
                    imageRect.y + (1f - binding.normalizedCenter.y - binding.normalizedSize.y * 0.5f) * imageRect.height,
                    binding.normalizedSize.x * imageRect.width,
                    binding.normalizedSize.y * imageRect.height);
                DrawOutline(zone, binding.action == CabControlAction.Brake
                    ? new Color(0.95f, 0.49f, 0.42f, 1f)
                    : new Color(1f, 0.83f, 0.37f, 1f), 2f);
                if (binding.artwork != null)
                {
                    Rect artworkRect = new Rect(
                        zone.x + (binding.artworkCenter.x - binding.artworkSize.x * 0.5f) * zone.width,
                        zone.y + (1f - binding.artworkCenter.y - binding.artworkSize.y * 0.5f) * zone.height,
                        binding.artworkSize.x * zone.width,
                        binding.artworkSize.y * zone.height);
                    Texture2D texture = binding.artwork.texture;
                    Rect textureRect = binding.artwork.textureRect;
                    Rect uv = new Rect(textureRect.x / texture.width, textureRect.y / texture.height,
                        textureRect.width / texture.width, textureRect.height / texture.height);
                    GUI.DrawTextureWithTexCoords(artworkRect, texture, uv, true);
                }
                string icon = string.IsNullOrWhiteSpace(binding.icon) ? "●" : binding.icon;
                string shortcut = binding.shortcut == UnityEngine.InputSystem.Key.None ? string.Empty : " [" + binding.shortcut + "]";
                GUI.Label(zone, icon + " " + binding.label + shortcut, EditorStyles.whiteMiniLabel);
            }
        }

        private static Rect Fit(Rect outer, float aspect)
        {
            float width = outer.width;
            float height = width / aspect;
            if (height > outer.height)
            {
                height = outer.height;
                width = height * aspect;
            }
            return new Rect(outer.x + (outer.width - width) * 0.5f, outer.y + (outer.height - height) * 0.5f, width, height);
        }

        private static void DrawOutline(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }

    [CustomEditor(typeof(CabSceneryCatalog))]
    public sealed class CabSceneryCatalogInspector : Editor
    {
        private float previewDistance;
        private int previewSpeed;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Здесь настраиваются слои травы, положение и движение гор, тени окружения, деревянные и железобетонные участки пути. Изменения можно проверять в Play Mode сцены CabRideLayoutPreview.", MessageType.Info);
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Предпросмотр кабины", EditorStyles.boldLabel);
            previewSpeed = GUILayout.Toolbar(previewSpeed, new[] { "Стоп", "Средняя", "Полная" });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Горы 0%")) Preview(0f);
                if (GUILayout.Button("Горы 50%")) Preview(0.50f);
                if (GUILayout.Button("Горы 90%")) Preview(0.90f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Деревянный путь")) Preview(0.08f);
                if (GUILayout.Button("Бетонный путь")) Preview(0.62f);
            }
            if (GUILayout.Button("Проверить маршрут и графику", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
        }

        private void Preview(float normalizedDistance)
        {
            MountainMotionSettings settings = ((CabSceneryCatalog)target).MountainMotion;
            previewDistance = Mathf.Clamp01(normalizedDistance) * settings.cycleDistance;
            CabRideController controller = FindObjectOfType<CabRideController>();
            if (controller == null)
            {
                Debug.Log("Откройте CabRideLayoutPreview и включите Play Mode для предпросмотра.");
                return;
            }
            float throttle = previewSpeed == 0 ? 0f : previewSpeed == 1 ? 0.50f : 1f;
            controller.ConfigureSceneryPreviewAtSpeed(RouteSegmentType.Forest, 0.48f, previewDistance, throttle);
        }
    }

    [CustomEditor(typeof(CabEnvironmentCatalog))]
    public sealed class CabEnvironmentCatalogInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Все параметры поездки редактируются здесь. Сезон фиксирован на поездку; время суток и погода продолжают меняться даже на остановке. Сигналы работают информативно.",
                MessageType.Info);
            serializedObject.Update();
            Draw("Время суток", "dayCycleSeconds", "startTime01", "dawnSky", "daySky", "sunsetSky", "nightSky", "sun", "moon");
            Draw("Небо, облака и солнечный блик", "cloudVerticalRange", "cloudOpacityMultiplier", "horizonFogRange",
                "sunGlareColor", "sunGlareSize", "sunGlareIntensity", "sunGlareHorizon", "sunGlareHorizonBand");
            Draw("Сезоны и атмосфера", "seasons");
            Draw("Погода", "weatherProfiles", "minimumWeatherSeconds", "maximumWeatherSeconds", "weatherTransitionSeconds");
            Draw("Осенние кленовые листья", "autumnMapleLeaf", "minimumLeafEventSeconds", "maximumLeafEventSeconds",
                "leafEventDuration", "flyingLeafCount", "leafStickChance", "maximumStuckLeaves");
            Draw("Мягкие события маршрута", "routeEvents", "minimumEventMovingSeconds", "maximumEventMovingSeconds");
            Draw("Знаки и светофоры", "tracksideMarkers", "enforcementMode", "minimumMarkerGapSegments", "maximumMarkerGapSegments");
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Проверить сезоны и маршрут", GUILayout.Height(34f))) ProjectBootstrapper.ValidateProjectMenu();
        }

        private void Draw(string title, params string[] properties)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string property in properties)
            {
                SerializedProperty value = serializedObject.FindProperty(property);
                if (value != null) EditorGUILayout.PropertyField(value, true);
            }
        }
    }
}
