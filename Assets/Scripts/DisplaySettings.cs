using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Window mode and resolution for the Options menu. Unity saves both itself and restores them on the next start.
    /// Fullscreen is borderless (FullScreenWindow), the only fullscreen mode every desktop platform supports.
    /// </summary>
    public static class DisplaySettings
    {
        // Smaller modes are listed by some monitors but leave the menu unreadable.
        const int MinWidth = 800;

        public static bool Fullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        public static string ModeLabel => Fullscreen ? "Fullscreen" : "Windowed";

        public static string ResolutionLabel => $"{Screen.width}×{Screen.height}";

        public static void ToggleFullscreen()
        {
            if (Fullscreen)
            {
                // A window as big as the desktop would hide its title bar off screen, so step down a size.
                Vector2Int size = WindowSize(new Vector2Int(Screen.width, Screen.height));
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            }
            else
            {
                Screen.SetResolution(Screen.width, Screen.height, FullScreenMode.FullScreenWindow);
            }
        }

        /// <summary>Steps to the next larger resolution, wrapping round to the smallest.</summary>
        public static void NextResolution()
        {
            List<Vector2Int> sizes = Sizes();
            int index = sizes.IndexOf(new Vector2Int(Screen.width, Screen.height));
            Vector2Int next = sizes[(index + 1) % sizes.Count];
            Screen.SetResolution(next.x, next.y, Screen.fullScreenMode);
        }

        /// <summary>The monitor's modes without duplicate refresh rates, smallest first.</summary>
        static List<Vector2Int> Sizes()
        {
            var sizes = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x >= MinWidth && !sizes.Contains(size)) sizes.Add(size);
            }

            Vector2Int current = new Vector2Int(Screen.width, Screen.height);
            if (!sizes.Contains(current)) sizes.Add(current);

            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        static Vector2Int WindowSize(Vector2Int size)
        {
            Resolution desktop = Screen.currentResolution;
            if (size.x < desktop.width && size.y < desktop.height) return size;

            Vector2Int best = size;
            foreach (Vector2Int candidate in Sizes())
            {
                if (candidate.x < desktop.width && candidate.y < desktop.height) best = candidate;
            }
            return best;
        }
    }
}
