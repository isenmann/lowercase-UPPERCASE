using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The keyboard key the hints name for shooting. Ctrl shoots everywhere, but in a browser Ctrl+W (W is also jump) closes
    /// the tab, so the Web build also binds F (see LetterInput) and its hints name F instead.
    /// </summary>
    public static class ShootKey
    {
        public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        public static string Name => IsWeb ? "F" : "Ctrl";

        /// <summary>
        /// In the Web build, rewrites the hints the scenes were built with ("Ctrl  shoot", "Ctrl shoots") to name F.
        /// Inactive texts are included, so hints inside closed popups change too.
        /// </summary>
        public static void RewriteHints()
        {
            if (!IsWeb) return;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                string hint = text.text;
                if (string.IsNullOrEmpty(hint) || !hint.Contains("Ctrl")) continue;
                text.text = hint.Replace("Ctrl  shoot", "F  shoot").Replace("Ctrl shoots", "F shoots");
            }
        }
    }
}
