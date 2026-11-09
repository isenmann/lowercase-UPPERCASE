using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Every moment that makes a sound. The names match the clip files in Resources/Sfx/&lt;set&gt;/.</summary>
    public enum Sound
    {
        MenuMove, MenuConfirm, MenuBack, MenuStart, PauseOpen, PauseClose, TypeLetter, NameSaved,
        Jump, Land, ToUpper, ToLower, BumpBlock, Bounce, Smash, WallCling, HookGrab, HookRelease, DoorOpen,
        PowerBlock, Shoot, AsteriskPop, AsteriskBlocked, EnemyShot, EnemyStomp,
        CollectHash, Checkpoint, CapsUnlock, LevelComplete, TimerTick, TimeUp,
        DeathEnemy, Burn, FallOff, Respawn, LifeLost, GameOver,
        GameFinished, HighScore,
    }

    /// <summary>The two sound styles the Options menu switches between.</summary>
    public enum SoundSet { Chip, Type }

    /// <summary>
    /// Plays the game's sound effects from the chosen set. The clips are made by Tools/Sfx/make_sfx.py.
    /// The chosen set is kept in PlayerPrefs; Type until the player picks one.
    /// </summary>
    public static class Sfx
    {
        const string SetKey = "SoundSet";

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly string[] fileNames = BuildFileNames();
        static SfxPlayer player;
        static SoundSet? set;

        public static SoundSet Set
        {
            get
            {
                if (set == null) set = PlayerPrefs.GetInt(SetKey, 1) == 1 ? SoundSet.Type : SoundSet.Chip;
                return set.Value;
            }
            set
            {
                Sfx.set = value;
                PlayerPrefs.SetInt(SetKey, value == SoundSet.Type ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void Play(Sound sound, float delay = 0f)
        {
            if (!Application.isPlaying) return;
            AudioClip clip = Clip(sound);
            if (clip == null) return;
            if (player == null) player = SfxPlayer.Create();
            player.Play(sound, clip, delay);
        }

        public static AudioClip Clip(Sound sound)
        {
            string path = $"Sfx/{Set}/{fileNames[(int)sound]}";
            if (!clips.TryGetValue(path, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(path);
                if (clip == null) Debug.LogWarning($"Missing sound clip Resources/{path}");
                clips[path] = clip;
            }
            return clip;
        }

        // MenuMove becomes menu_move, and so on.
        static string[] BuildFileNames()
        {
            var names = (Sound[])System.Enum.GetValues(typeof(Sound));
            var result = new string[names.Length];
            foreach (Sound sound in names)
            {
                string name = sound.ToString();
                var snake = new System.Text.StringBuilder();
                for (int i = 0; i < name.Length; i++)
                {
                    if (i > 0 && char.IsUpper(name[i])) snake.Append('_');
                    snake.Append(char.ToLowerInvariant(name[i]));
                }
                result[(int)sound] = snake.ToString();
            }
            return result;
        }
    }
}
