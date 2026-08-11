using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class SortingYardController : MonoBehaviour
    {
        private AppServices services;
        private RectTransform root;
        private RectTransform boardRoot;
        private AccessibleFocusGroup focusGroup;
        private SortingBoard board;
        private LevelDefinition level;
        private TextMeshProUGUI status;
        private readonly Dictionary<int, WagonView> wagons = new Dictionary<int, WagonView>();
        private readonly Dictionary<string, AccessibleButton> targets = new Dictionary<string, AccessibleButton>();
        private bool inputLocked;

        private void Start()
        {
            services = AppServices.Ensure();
            services.Audio.StopAllLoops();
            BuildGame();
        }

        private void BuildGame()
        {
            GameMode mode = services.Session.SelectedMode;
            int count = services.Session.OptionCount;
            level = services.Games.Find(mode, count);
            if (level == null)
            {
                Debug.LogError($"Missing level for {mode} with {count} options.");
                SceneManager.LoadScene(SceneNames.MainMenu);
                return;
            }

            board = new SortingBoard(mode, level.Tokens, count, level.GetInstanceID() + services.Session.ReplaySeed * 7919);
            Canvas canvas;
            root = UiFactory.CreateScreen("SortingYardCanvas", out canvas);
            AppSettings theme = services.Settings;

            Image background = UiFactory.Image("YardBackground", root, services.Visuals.yardBackground, Color.white, false);
            UiFactory.Stretch(background.rectTransform);
            if (background.sprite == null) background.color = theme.GroundColor;

            RectTransform shade = UiFactory.Panel("ReadabilityShade", root, new Color(0.02f, 0.04f, 0.04f, 0.18f));
            UiFactory.Stretch(shade);
            RectTransform header = UiFactory.Panel("Header", root, theme.PanelColor);
            UiFactory.StyleSurface(header);
            UiFactory.SetRect(header, new Vector2(0.015f, 0.82f), new Vector2(0.985f, 0.985f), Vector2.zero, Vector2.zero);

            focusGroup = root.gameObject.AddComponent<AccessibleFocusGroup>();
            focusGroup.Cancelled += HandleCancel;

            AccessibleButton back = UiFactory.Button("Back", header, focusGroup, "←\nНазад", theme.PanelAltColor,
                theme.SelectedColor, ReturnToMenu, 25);
            UiFactory.SetRect(back.RectTransform, new Vector2(0.01f, 0.12f), new Vector2(0.12f, 0.88f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI title = UiFactory.Label("Title", header, level.Title, theme.TitleFontSize,
                theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.14f, 0.48f), new Vector2(0.79f, 0.94f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI prompt = UiFactory.Label("Prompt", header, level.Instruction, theme.StatusFontSize,
                theme.MutedTextColor, TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(prompt.rectTransform, new Vector2(0.14f, 0.05f), new Vector2(0.79f, 0.50f), Vector2.zero, Vector2.zero);

            AccessibleButton speak = UiFactory.Button("Speak", header, focusGroup, "Голос\nПовторить", theme.PrimaryColor,
                theme.SelectedColor, SpeakInstruction, 24);
            UiFactory.SetRect(speak.RectTransform, new Vector2(0.81f, 0.12f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);

            boardRoot = UiFactory.Panel("Board", root, new Color(0.08f, 0.12f, 0.10f, 0.18f));
            UiFactory.SetRect(boardRoot, new Vector2(0.015f, 0.075f), new Vector2(0.985f, 0.805f), Vector2.zero, Vector2.zero);
            status = UiFactory.Label("Status", root, InitialStatus(), theme.StatusFontSize,
                theme.TextColor, TextAlignmentOptions.Center, UiFontRole.Body);
            UiFactory.SetRect(status.rectTransform, new Vector2(0.18f, 0.005f), new Vector2(0.82f, 0.073f), Vector2.zero, Vector2.zero);
            RectTransform statusPanel = UiFactory.Panel("StatusPanel", root, theme.PanelColor);
            UiFactory.StyleSurface(statusPanel);
            UiFactory.SetRect(statusPanel, new Vector2(0.17f, 0.002f), new Vector2(0.83f, 0.075f), Vector2.zero, Vector2.zero);
            statusPanel.SetAsFirstSibling();

            DrawTracks(count);
            CreateDepot();
            if (mode == GameMode.Colors) BuildColorBoard();
            else BuildPairBoard();
            SpeakInstruction();
        }

        private void DrawTracks(int count)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 size = boardRoot.rect.size;
            for (int i = 0; i < count; i++)
            {
                float normalizedY = LaneY(i, count);
                Vector2 start = new Vector2(size.x * 0.09f, size.y * 0.50f);
                Vector2 end = new Vector2(size.x * 0.96f, size.y * normalizedY);
                RailPathView.CreateFanPath(boardRoot, start, end, new Color(0.20f, 0.22f, 0.23f, 1f), new Color(0.37f, 0.25f, 0.14f, 1f));
            }
        }

        private void CreateDepot()
        {
            RectTransform depot = UiFactory.Panel("Depot", boardRoot, new Color(0.25f, 0.28f, 0.29f, 0.96f), services.Visuals.depot);
            UiFactory.SetRect(depot, new Vector2(0.015f, 0.35f), new Vector2(0.16f, 0.65f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = UiFactory.Label("Label", depot, "ДЕПО", 25, services.Settings.TextColor);
            UiFactory.Stretch(label.rectTransform, 5f, 5f, 5f, 5f);
        }

        private void BuildColorBoard()
        {
            List<BoardPiece> pieces = board.Pieces.ToList();
            for (int i = 0; i < pieces.Count; i++)
            {
                BoardPiece piece = pieces[i];
                float y = LaneY(i, pieces.Count);
                MatchToken token = piece.Token;
                AccessibleButton target = UiFactory.Button("Target_" + token.Id, boardRoot, focusGroup,
                    token.Symbol + "\n" + token.Label, Color.Lerp(token.Color, Color.black, 0.22f), services.Settings.SelectedColor,
                    () => OnColorTarget(token.Id), 25);
                UiFactory.SetRect(target.RectTransform, new Vector2(0.18f, y - 0.075f), new Vector2(0.34f, y + 0.075f), Vector2.zero, Vector2.zero);
                targets[token.Id] = target;
            }

            int[] shuffledLanes = Enumerable.Range(0, pieces.Count).OrderBy(_ => Random.value).ToArray();
            for (int i = 0; i < pieces.Count; i++)
            {
                float y = LaneY(shuffledLanes[i], pieces.Count);
                WagonView view = WagonView.Create(boardRoot, focusGroup, pieces[i], new Vector2(0.78f, y), new Vector2(0.19f, 0.15f), OnWagonSelected);
                wagons[pieces[i].Id] = view;
            }
        }

        private void BuildPairBoard()
        {
            List<BoardPiece> pieces = board.Pieces.ToList();
            int rows = Mathf.CeilToInt(pieces.Count / 2f);
            for (int i = 0; i < pieces.Count; i++)
            {
                int column = i % 2;
                int row = i / 2;
                float y = LaneY(row, rows);
                float x = column == 0 ? 0.59f : 0.82f;
                WagonView view = WagonView.Create(boardRoot, focusGroup, pieces[i], new Vector2(x, y), new Vector2(0.18f, 0.145f), OnWagonSelected);
                wagons[pieces[i].Id] = view;
            }
        }

        private void OnWagonSelected(int pieceId)
        {
            if (inputLocked) return;
            BoardActionResult result = board.SelectPiece(pieceId);
            RefreshSelection();
            HandleResult(result);
        }

        private void OnColorTarget(string tokenId)
        {
            if (inputLocked) return;
            BoardActionResult result = board.SelectColorTarget(tokenId);
            RefreshSelection();
            HandleResult(result);
        }

        private void HandleResult(BoardActionResult result)
        {
            switch (result.Kind)
            {
                case BoardActionKind.Selected:
                    status.text = services.Session.SelectedMode == GameMode.Colors
                        ? "Теперь выберите путь с таким же символом и цветом"
                        : "Теперь найдите второй такой же вагон";
                    break;
                case BoardActionKind.Deselected:
                    status.text = InitialStatus();
                    break;
                case BoardActionKind.Incorrect:
                    services.Audio.Play(SoundCue.GentleError);
                    status.text = "Не этот. Первый вагон остаётся выбранным — попробуйте ещё раз";
                    StartCoroutine(SoftIncorrectFeedback(result.SecondaryPieceId));
                    break;
                case BoardActionKind.Correct:
                case BoardActionKind.Completed:
                    StartCoroutine(AnimateSuccess(result, result.Kind == BoardActionKind.Completed));
                    break;
            }
        }

        private IEnumerator SoftIncorrectFeedback(int pieceId)
        {
            if (pieceId < 0 || !wagons.TryGetValue(pieceId, out WagonView wagon)) yield break;
            Color original = wagon.Button.GetComponent<Image>().color;
            wagon.Button.GetComponent<Image>().color = services.Settings.ErrorColor;
            yield return new WaitForSecondsRealtime(services.Preferences.motionLevel == MotionLevel.Off ? 0.12f : services.Settings.FeedbackSeconds);
            wagon.Button.GetComponent<Image>().color = original;
        }

        private IEnumerator AnimateSuccess(BoardActionResult result, bool completesBoard)
        {
            inputLocked = true;
            focusGroup.enabled = false;
            services.Audio.Play(SoundCue.Couple);
            status.text = "Верно! Локомотив переставляет вагон";

            List<WagonView> moving = new List<WagonView>();
            if (wagons.TryGetValue(result.PrimaryPieceId, out WagonView primary)) moving.Add(primary);
            if (result.SecondaryPieceId >= 0 && wagons.TryGetValue(result.SecondaryPieceId, out WagonView secondary)) moving.Add(secondary);
            foreach (WagonView wagon in moving) wagon.Button.SetInteractable(false);

            Vector2 firstPosition = moving.Count > 0 ? (Vector2)moving[0].Button.RectTransform.localPosition : Vector2.zero;
            RectTransform locomotive = CreateLocomotive(firstPosition);
            float duration = AnimationDuration();
            float elapsed = 0f;
            Vector2 locomotiveStart = locomotive.localPosition;
            Vector2 locomotiveEnd = new Vector2(-boardRoot.rect.width * 0.32f, 0f);
            Dictionary<WagonView, Vector2> starts = moving.ToDictionary(wagon => wagon, wagon => (Vector2)wagon.Button.RectTransform.localPosition);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                locomotive.localPosition = Vector2.Lerp(locomotiveStart, locomotiveEnd, eased);
                for (int i = 0; i < moving.Count; i++)
                {
                    Vector2 end = locomotiveEnd + new Vector2(150f + i * 185f, 0f);
                    moving[i].Button.RectTransform.localPosition = Vector2.Lerp(starts[moving[i]], end, eased);
                }
                yield return null;
            }

            services.Audio.Play(SoundCue.Correct);
            foreach (WagonView wagon in moving)
            {
                wagon.gameObject.SetActive(false);
            }
            Destroy(locomotive.gameObject);
            if (services.Session.SelectedMode == GameMode.Colors && targets.TryGetValue(result.Token.Id, out AccessibleButton target))
            {
                target.SetSelected(true);
                target.SetInteractable(false);
            }

            if (completesBoard)
            {
                services.Progress.MarkCompleted(services.Session.SelectedMode, services.Session.OptionCount);
                yield return new WaitForSecondsRealtime(0.35f);
                ShowCompletion();
                yield break;
            }

            inputLocked = false;
            focusGroup.enabled = true;
            focusGroup.FocusFirst();
            status.text = InitialStatus();
        }

        private RectTransform CreateLocomotive(Vector2 position)
        {
            RectTransform locomotive = UiFactory.Panel("MovingLocomotive", boardRoot, new Color(0.68f, 0.16f, 0.10f, 1f), services.Visuals.locomotive);
            locomotive.anchorMin = new Vector2(0.5f, 0.5f);
            locomotive.anchorMax = new Vector2(0.5f, 0.5f);
            locomotive.pivot = new Vector2(0.5f, 0.5f);
            locomotive.sizeDelta = new Vector2(210f, 118f);
            locomotive.localPosition = position + new Vector2(-190f, 0f);
            TextMeshProUGUI label = UiFactory.Label("Label", locomotive, "ЛОКОМОТИВ", 18, services.Settings.TextColor);
            UiFactory.Stretch(label.rectTransform, 8f, 8f, 8f, 8f);
            return locomotive;
        }

        private void RefreshSelection()
        {
            foreach (KeyValuePair<int, WagonView> pair in wagons)
            {
                pair.Value.SetSelected(board.SelectedPieceId.HasValue && board.SelectedPieceId.Value == pair.Key);
            }
        }

        private void ShowCompletion()
        {
            inputLocked = true;
            services.Audio.Play(SoundCue.Success);
            services.Speech.Speak("Задание выполнено. Отличная работа!");
            RectTransform overlay = UiFactory.Panel("Completion", root, new Color(0f, 0f, 0f, 0.78f));
            UiFactory.Stretch(overlay);
            RectTransform card = UiFactory.Panel("Card", overlay, services.Settings.PanelColor);
            UiFactory.StyleSurface(card);
            UiFactory.SetRect(card, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI title = UiFactory.Label("Title", card, "Готово!\nОтличная работа!",
                services.Settings.DisplayFontSize, services.Settings.AccentColor, TextAlignmentOptions.Center, UiFontRole.Display);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.08f, 0.51f), new Vector2(0.92f, 0.90f), Vector2.zero, Vector2.zero);

            AccessibleFocusGroup completionFocus = card.gameObject.AddComponent<AccessibleFocusGroup>();
            completionFocus.Cancelled += ReturnToMenu;
            AccessibleButton replay = UiFactory.Button("Replay", card, completionFocus, "Ещё раз", services.Settings.PrimaryColor,
                services.Settings.SelectedColor, Replay, 38);
            UiFactory.SetRect(replay.RectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.46f, 0.43f), Vector2.zero, Vector2.zero);
            AccessibleButton menu = UiFactory.Button("Menu", card, completionFocus, "В меню", services.Settings.PanelAltColor,
                services.Settings.SelectedColor, ReturnToMenu, 38);
            UiFactory.SetRect(menu.RectTransform, new Vector2(0.54f, 0.14f), new Vector2(0.92f, 0.43f), Vector2.zero, Vector2.zero);
        }

        private void SpeakInstruction() => services.Speech.Speak(level.Instruction, level.InstructionClip);

        private void HandleCancel()
        {
            if (inputLocked) return;
            if (board.CancelSelection())
            {
                RefreshSelection();
                status.text = InitialStatus();
                return;
            }
            ReturnToMenu();
        }

        private void Replay()
        {
            services.Session.ReplaySeed++;
            SceneManager.LoadScene(SceneNames.SortingYard);
        }

        private void ReturnToMenu()
        {
            services.Speech.Stop();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private string InitialStatus()
        {
            return services.Session.SelectedMode == GameMode.Colors
                ? "Сначала выберите вагон"
                : "Выберите первый вагон";
        }

        private float AnimationDuration()
        {
            return services.Preferences.motionLevel switch
            {
                MotionLevel.Off => 0.06f,
                MotionLevel.Reduced => 0.28f,
                _ => services.Settings.WagonTravelSeconds
            };
        }

        private static float LaneY(int index, int count)
        {
            if (count <= 1) return 0.5f;
            return Mathf.Lerp(0.16f, 0.84f, (float)index / (count - 1));
        }
    }
}
