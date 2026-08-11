using UnityEngine;

namespace SortingStation
{
    public interface ISpeechService
    {
        void Speak(string text, AudioClip preferredClip = null);
        void Stop();
    }
}
