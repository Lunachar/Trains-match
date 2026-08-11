using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// Runs the normal game in Editor Play Mode and lets AutomatedSmokeCapture
    /// render its reference images. No player build is created.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorSmokeCaptureRunner
    {
        private const string ActiveKey = "SortingStation.EditorSmoke.Active";
        private const string OutputKey = "SortingStation.EditorSmoke.Output";
        private const string StartedKey = "SortingStation.EditorSmoke.Started";
        private const string FailedKey = "SortingStation.EditorSmoke.Failed";
        private const int ExpectedImageCount = 7;
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
        private static readonly string[] ExpectedNames =
        {
            "01-main-menu.png",
            "02-sorting-yard.png",
            "03-cab-ride-1600x1000.png",
            "04-cab-ride-1920x1080.png",
            "05-cab-ride-1280x800.png",
            "06-cab-ride-1024x768.png",
            "07-cab-tunnel-1600x1000.png"
        };
        private static readonly Vector2Int[] ExpectedSizes =
        {
            new Vector2Int(1600, 1000),
            new Vector2Int(1600, 1000),
            new Vector2Int(1600, 1000),
            new Vector2Int(1920, 1080),
            new Vector2Int(1280, 800),
            new Vector2Int(1024, 768),
            new Vector2Int(1600, 1000)
        };

        static EditorSmokeCaptureRunner()
        {
            if (SessionState.GetBool(ActiveKey, false)) AttachUpdate();
        }

        [MenuItem("Sorting Station/Tests/Capture smoke screens in Editor")]
        public static void Run()
        {
            string output = ReadArgument("-smokeOutput");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.GetFullPath(Path.Combine("Logs", "SmokeScreensFinal"));
            }

            Directory.CreateDirectory(output);
            foreach (string oldFile in Directory.EnumerateFiles(output, "*.png")) File.Delete(oldFile);

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FailedKey, false);
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            Environment.SetEnvironmentVariable("SORTING_STATION_SMOKE", "1");
            AttachUpdate();

            EditorSceneManager.OpenScene("Assets/SortingStation/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
            Debug.Log("Editor smoke capture started. Output: " + output);
        }

        private static void AttachUpdate()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            string output = SessionState.GetString(OutputKey, string.Empty);
            bool complete = Directory.Exists(output) && Directory.EnumerateFiles(output, "*.png").Count() >= ExpectedImageCount;
            DateTime started = ReadStartedTime();
            bool timedOut = DateTime.UtcNow - started > Timeout;

            if (complete && !ValidateCaptures(output, out string validationError))
            {
                SessionState.SetBool(FailedKey, true);
                Debug.LogError("Editor smoke capture is invalid: " + validationError);
            }

            if (timedOut && !complete)
            {
                SessionState.SetBool(FailedKey, true);
                Debug.LogError("Editor smoke capture timed out. Output: " + output);
            }

            if (!complete && !timedOut) return;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            bool failed = SessionState.GetBool(FailedKey, false);
            EditorApplication.update -= Update;
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(FailedKey);
            SessionState.EraseString(OutputKey);
            SessionState.EraseString(StartedKey);
            Environment.SetEnvironmentVariable("SORTING_STATION_SMOKE", null);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failed ? 1 : 0);
            }
            else if (!failed)
            {
                EditorUtility.RevealInFinder(output);
            }
        }

        private static bool ValidateCaptures(string output, out string error)
        {
            for (int i = 0; i < ExpectedNames.Length; i++)
            {
                string path = Path.Combine(output, ExpectedNames[i]);
                if (!File.Exists(path))
                {
                    error = "missing " + ExpectedNames[i];
                    return false;
                }

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    {
                        error = "cannot decode " + ExpectedNames[i];
                        return false;
                    }
                    if (texture.width != ExpectedSizes[i].x || texture.height != ExpectedSizes[i].y)
                    {
                        error = ExpectedNames[i] + " has size " + texture.width + "x" + texture.height;
                        return false;
                    }

                    float minimum = 1f;
                    float maximum = 0f;
                    Color first = texture.GetPixelBilinear(0.08f, 0.08f);
                    float maximumColorDistance = 0f;
                    for (int y = 1; y <= 9; y++)
                    {
                        for (int x = 1; x <= 13; x++)
                        {
                            Color sample = texture.GetPixelBilinear(x / 14f, y / 10f);
                            float luminance = 0.2126f * sample.r + 0.7152f * sample.g + 0.0722f * sample.b;
                            minimum = Mathf.Min(minimum, luminance);
                            maximum = Mathf.Max(maximum, luminance);
                            maximumColorDistance = Mathf.Max(maximumColorDistance,
                                Mathf.Abs(sample.r - first.r) + Mathf.Abs(sample.g - first.g) + Mathf.Abs(sample.b - first.b));
                        }
                    }
                    if (maximum - minimum < 0.035f && maximumColorDistance < 0.08f)
                    {
                        error = ExpectedNames[i] + " is blank or nearly uniform";
                        return false;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            error = string.Empty;
            return true;
        }

        private static DateTime ReadStartedTime()
        {
            string ticksValue = SessionState.GetString(StartedKey, string.Empty);
            return long.TryParse(ticksValue, out long ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.UtcNow;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return string.Empty;
        }
    }
}
