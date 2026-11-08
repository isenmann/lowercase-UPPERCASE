using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A small pool of audio sources that lives across scene loads, so a death or level-complete sound keeps
    /// playing while the next scene loads. Created on the first sound; nothing in a scene refers to it.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        const int Voices = 12;
        // The same sound started again within this time is dropped, so two triggers in one step don't double it.
        const float RepeatGuardSeconds = 0.04f;

        readonly AudioSource[] sources = new AudioSource[Voices];
        float[] lastPlayed;
        int next;

        public static SfxPlayer Create()
        {
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            return go.AddComponent<SfxPlayer>();
        }

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
            lastPlayed = new float[System.Enum.GetValues(typeof(Sound)).Length];
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = float.NegativeInfinity;
        }

        public void Play(Sound sound, AudioClip clip, float delay)
        {
            float now = Time.unscaledTime + delay;
            if (now - lastPlayed[(int)sound] < RepeatGuardSeconds) return;
            lastPlayed[(int)sound] = now;

            AudioSource source = FreeSource();
            source.clip = clip;
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        // A free voice if there is one, otherwise the oldest one is cut off.
        AudioSource FreeSource()
        {
            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = sources[(next + i) % Voices];
                if (!source.isPlaying)
                {
                    next = (next + i + 1) % Voices;
                    return source;
                }
            }
            AudioSource oldest = sources[next];
            next = (next + 1) % Voices;
            oldest.Stop();
            return oldest;
        }
    }
}
