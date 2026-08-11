using System;
using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip cabRadioMusic;
        [SerializeField] private AudioClip[] cabRadioPlaylist = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip railLoop;
        [SerializeField] private SoundEventBank[] events = Array.Empty<SoundEventBank>();

        public AudioClip MenuMusic => menuMusic;
        public AudioClip CabRadioMusic
        {
            get
            {
                if (cabRadioMusic != null) return cabRadioMusic;
                AudioClip[] playlist = CabRadioPlaylist;
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (playlist[i] != null) return playlist[i];
                }
                return null;
            }
        }
        public AudioClip[] CabRadioPlaylist => cabRadioPlaylist ?? Array.Empty<AudioClip>();
        public AudioClip RailLoop => railLoop;
        public SoundEventBank[] Events => events ?? Array.Empty<SoundEventBank>();

        public SoundEventBank Find(SoundCue cue)
        {
            foreach (SoundEventBank bank in Events)
            {
                if (bank != null && bank.cue == cue)
                {
                    return bank;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void Configure(AudioClip menu, AudioClip radio, AudioClip rails, SoundEventBank[] banks)
        {
            menuMusic = menu;
            cabRadioMusic = radio;
            cabRadioPlaylist = radio != null ? new[] { radio } : Array.Empty<AudioClip>();
            railLoop = rails;
            events = banks ?? Array.Empty<SoundEventBank>();
        }

        public void ConfigureMissing(AudioClip menu, AudioClip radio, AudioClip rails, SoundEventBank[] banks)
        {
            if (menuMusic == null) menuMusic = menu;
            if (cabRadioMusic == null) cabRadioMusic = radio;
            if (cabRadioPlaylist == null || cabRadioPlaylist.Length == 0)
            {
                cabRadioPlaylist = radio != null ? new[] { radio } : Array.Empty<AudioClip>();
            }
            if (railLoop == null) railLoop = rails;

            System.Collections.Generic.List<SoundEventBank> merged =
                new System.Collections.Generic.List<SoundEventBank>(events ?? Array.Empty<SoundEventBank>());
            SoundEventBank[] defaults = banks ?? Array.Empty<SoundEventBank>();
            for (int i = 0; i < defaults.Length; i++)
            {
                SoundEventBank candidate = defaults[i];
                if (candidate == null) continue;
                bool exists = false;
                for (int j = 0; j < merged.Count; j++)
                {
                    if (merged[j] != null && merged[j].cue == candidate.cue)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) merged.Add(candidate);
            }
            events = merged.ToArray();
        }
#endif
    }
}
