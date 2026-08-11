using UnityEngine;

namespace SortingStation
{
    public interface IAudioService
    {
        void Play(SoundCue cue);
        void PlayClip(AudioClip clip, float volume = 1f);
        void PlayMenuMusic();
        void SetRadio(bool enabled);
        void NextRadioTrack();
        void PreviousRadioTrack();
        void SelectRadioTrack(int index);
        void SetRails(float speed01);
        void ApplyPreferences(UserPreferences preferences);
        void StopAllLoops();
        bool RadioIsPlaying { get; }
        string RadioTrackName { get; }
        int RadioTrackIndex { get; }
        int RadioTrackCount { get; }
        float RadioTrackTime { get; }
        float RadioTrackLength { get; }
    }
}
