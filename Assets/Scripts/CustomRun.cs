using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The custom level being played, handed from whatever starts it (the editor, the My levels list) to the
    /// custom level's scene, which builds it as it loads. Null means the scene was opened directly and shows its
    /// sample level.
    /// </summary>
    public static class CustomRun
    {
        public static CustomLevelData Level { get; set; }

        // Domain reload is off in this project's Play mode settings, so statics survive from one session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Level = null;
    }
}
