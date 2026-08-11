using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Text;
#endif

namespace SortingStation
{
    public sealed class SpeechService : MonoBehaviour, ISpeechService
    {
        private AppSettings settings;
        private IAudioService audioService;
        private UserPreferences preferences;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private Process speechProcess;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject textToSpeech;
        private bool androidReady;
        private string pendingText;
        private sealed class TtsInitListener : AndroidJavaProxy
        {
            private readonly SpeechService owner;
            public TtsInitListener(SpeechService owner) : base("android.speech.tts.TextToSpeech$OnInitListener") => this.owner = owner;
            public void onInit(int status) { if (status == 0) owner.OnAndroidReady(); }
        }
#endif

        public void Initialize(AppSettings appSettings, IAudioService service, UserPreferences userPreferences)
        {
            settings = appSettings;
            audioService = service;
            preferences = userPreferences ?? new UserPreferences();
        }

        public void SetPreferences(UserPreferences value) => preferences = value ?? new UserPreferences();

        public void Speak(string text, AudioClip preferredClip = null)
        {
            Stop();
            if (!preferences.speechEnabled)
            {
                return;
            }

            if (preferredClip != null)
            {
                audioService?.PlayClip(preferredClip, 1f);
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            SpeakAndroid(text);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            SpeakWindows(text);
#else
            UnityEngine.Debug.Log("Speech is unavailable on this platform: " + text);
#endif
        }

        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            pendingText = null;
            if (textToSpeech != null) textToSpeech.Call<int>("stop");
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (speechProcess != null && !speechProcess.HasExited) speechProcess.Kill();
            }
            catch { }
            speechProcess?.Dispose();
            speechProcess = null;
#endif
        }

        private void OnDestroy()
        {
            Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (textToSpeech != null)
            {
                textToSpeech.Call("shutdown");
                textToSpeech.Dispose();
                textToSpeech = null;
            }
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void SpeakWindows(string text)
        {
            int volume = Mathf.RoundToInt(Mathf.Clamp01(preferences.masterVolume * preferences.speechVolume) * 100f);
            int rate = Mathf.RoundToInt(Mathf.Lerp(-3f, 3f, Mathf.InverseLerp(0.5f, 2f, settings != null ? settings.SpeechRate : 0.9f)));
            string escaped = text.Replace("'", "''");
            string script = "Add-Type -AssemblyName System.Speech; " +
                            "$s=New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                            "$s.Volume=" + volume + "; $s.Rate=" + rate + "; " +
                            "$s.Speak('" + escaped + "'); $s.Dispose();";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            speechProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void SpeakAndroid(string text)
        {
            pendingText = text;
            if (textToSpeech == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    textToSpeech = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new TtsInitListener(this));
                }
                return;
            }

            FlushAndroidSpeech();
        }

        private void OnAndroidReady()
        {
            androidReady = true;
            string language = settings != null ? settings.SpeechLanguage : "ru_RU";
            string[] parts = language.Split('_');
            using (AndroidJavaObject locale = parts.Length > 1
                       ? new AndroidJavaObject("java.util.Locale", parts[0], parts[1])
                       : new AndroidJavaObject("java.util.Locale", language))
            {
                textToSpeech.Call<int>("setLanguage", locale);
            }
            FlushAndroidSpeech();
        }

        private void FlushAndroidSpeech()
        {
            if (!androidReady || textToSpeech == null || string.IsNullOrWhiteSpace(pendingText)) return;
            textToSpeech.Call<int>("setSpeechRate", settings != null ? settings.SpeechRate : 0.9f);
            textToSpeech.Call<int>("setPitch", settings != null ? settings.SpeechPitch : 1f);
            using (AndroidJavaObject bundle = new AndroidJavaObject("android.os.Bundle"))
            {
                bundle.Call("putFloat", "volume", Mathf.Clamp01(preferences.masterVolume * preferences.speechVolume));
                textToSpeech.Call<int>("speak", pendingText, 0, bundle, "sorting-station-tts");
            }
            pendingText = null;
        }
#endif
    }
}
