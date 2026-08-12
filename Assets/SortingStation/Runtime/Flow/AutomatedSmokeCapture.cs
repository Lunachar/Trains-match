using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation
{
    public sealed class AutomatedSmokeCapture : MonoBehaviour
    {
        private string outputDirectory;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Debug.Log("Smoke capture started.");
            outputDirectory = ReadArgument("-smokeOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(Application.persistentDataPath, "SmokeScreens");
            }
            Directory.CreateDirectory(outputDirectory);
            Debug.Log("Smoke output: " + outputDirectory);
            Screen.SetResolution(1600, 1000, FullScreenMode.Windowed);

            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-rideTimeline", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureRideTimeline();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-departurePreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureDeparturePreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-trackPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureTrackPreview();
                yield break;
            }

            yield return WaitForScene(SceneNames.MainMenu);
            yield return Capture("01-main-menu.png");
            Debug.Log("Smoke captured main menu.");

            AppServices.Instance.Session.Select(GameMode.Colors, 4);
            SceneManager.LoadScene(SceneNames.SortingYard);
            yield return WaitForScene(SceneNames.SortingYard);
            yield return Capture("02-sorting-yard.png");
            Debug.Log("Smoke captured sorting yard.");

            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureSmokeDemo(false);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Capture("03-cab-ride-1600x1000.png", 1600, 1000);
            yield return Capture("04-cab-ride-1920x1080.png", 1920, 1080);
            yield return Capture("05-cab-ride-1280x800.png", 1280, 800);
            yield return Capture("06-cab-ride-1024x768.png", 1024, 768);
            if (cab != null) cab.ConfigureSmokeDemo(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("07-cab-tunnel-1600x1000.png", 1600, 1000);
            Debug.Log("Smoke captured cab ride.");

            Debug.Log("SMOKE_CAPTURE_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private IEnumerator CaptureRideTimeline()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureSmokeDemo(false);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Capture("01-ride-00m.png");
            yield return new WaitForSecondsRealtime(60f);
            yield return Capture("02-ride-01m.png");
            yield return new WaitForSecondsRealtime(60f);
            yield return Capture("03-ride-02m.png");
            Debug.Log("RIDE_TIMELINE_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private IEnumerator CaptureDeparturePreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("departure-ready.png", 1600, 1000);
            Debug.Log("DEPARTURE_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private IEnumerator CaptureTrackPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Forest, 0.48f);
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Capture("01-straight-track.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Village, 0.55f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("02-track-switch.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Road, 0.55f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("03-level-crossing.png", 1600, 1000);
            Debug.Log("TRACK_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.2f);
        }

        private IEnumerator Capture(string fileName, int width = 1600, int height = 1000)
        {
            string path = Path.Combine(outputDirectory, fileName);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("SmokeCamera", typeof(Camera));
                camera = cameraObject.GetComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>();
            RenderMode[] originalModes = canvases.Select(canvas => canvas.renderMode).ToArray();
            Camera[] originalCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = camera;
                canvases[i].planeDistance = 1f;
            }

            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = texture;
            RenderTexture.active = texture;
            camera.Render();

            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            texture.Release();
            Destroy(texture);
            Destroy(image);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = originalModes[i];
                canvases[i].worldCamera = originalCameras[i];
            }
            yield return null;
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
