using UnityEngine;

namespace SortingStation
{
    public interface IAudioService
    {
        void Play(SoundCue cue);
        void PlayAmbient(CabAmbientSound cue, float suggestedDurationSeconds = 0f);
        bool PlayDispatcher(DispatcherVoiceCue cue);
        bool HasInteractionAudio(string interactionId, bool requireDispatcherPair = false);
        bool PlayInteractionReaction(string interactionId);
        bool PlayInteractionDispatcherCall(string interactionId);
        bool PlayInteractionDispatcherResponse(string interactionId);
        bool PlayDispatcherRadioExchange();
        void PlayClip(AudioClip clip, float volume = 1f);
        void PlayMenuMusic();
        void SetRadio(bool enabled);
        void SetOnlineRadio(bool enabled);
        void ToggleRadioMusicVolume();
        void NextRadioTrack();
        void PreviousRadioTrack();
        void SelectRadioTrack(int index);
        void SetRails(float speed01);
        void SetWeather(WeatherType weather, float intensity);
        void ApplyPreferences(UserPreferences preferences);
        void StopAllLoops();
        bool RadioIsPlaying { get; }
        bool OnlineRadioIsPlaying { get; }
        bool OnlineRadioIsConnecting { get; }
        bool OnlineRadioIsActive { get; }
        bool OnlineRadioAvailable { get; }
        string OnlineRadioName { get; }
        string OnlineRadioStatus { get; }
        string RadioTrackName { get; }
        int RadioTrackIndex { get; }
        int RadioTrackCount { get; }
        float RadioTrackTime { get; }
        float RadioTrackLength { get; }
        float RadioMusicVolumeMultiplier { get; }
    }
}
