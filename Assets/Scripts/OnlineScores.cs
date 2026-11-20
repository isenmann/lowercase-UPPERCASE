using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Sends a run through the 15 levels to the world high scores (Web/src/scores.js), besides the player's own table.
    /// A run starts with the first level, reports each level as it is finished and ends with a name. Calls go out one
    /// after another, in order. Anything that goes wrong (no network, a refusal, an old build) quietly turns the rest of
    /// the run offline: the game never waits for the server and never shows an error while playing.
    /// The calls are signed with a key kept out of git (OnlineScores.Key.cs, see Web/README.md); a build without it,
    /// the editor (unless <see cref="AllowInEditor"/>) and the Options switch turned off send nothing.
    /// </summary>
    public static partial class OnlineScores
    {
        public const string BaseUrl = "https://lowercase-uppercase-levels.devplayrepeat.workers.dev";
        const string EnabledKey = "lowercaseUppercase.onlineScores";
        const int MaxVersionLength = 20;

        public enum RunState
        {
            /// <summary>No run, or one that doesn't go online (switched off, no key, the editor, a chapter start).</summary>
            None,
            /// <summary>Started and reporting; calls may still be on their way.</summary>
            Online,
            /// <summary>Something went wrong; the rest of the run stays local. See <see cref="LastError"/>.</summary>
            Offline,
            /// <summary>The name went out and the server answered with a place.</summary>
            Finished
        }

        public class Result
        {
            public int rank;
            public int count;
            public int total;
            public string name;
            public bool nameRefused;
        }

        [Serializable] class StartBody { public string hash; public string platform; public string version; }
        [Serializable] class StartReply { public string id; }
        [Serializable] class LevelBody { public int level; public int points; public int centisLeft; }
        [Serializable] class FinishBody { public string name; public int partialPoints; }
        [Serializable] class ErrorReply { public string error; }

        class Call
        {
            public string action;   // "" for the start, else "level" or "finish"
            public string body;
            public Action<string> answered;
        }

        /// <summary>Where calls go. Tests put a fake here; null means the real network.</summary>
        public static IScoreTransport Transport;
        /// <summary>Off in the editor, so trying the game there never reaches the live list.</summary>
        public static bool AllowInEditor;
        /// <summary>Tests sign with their own key; null means the key the build was made with.</summary>
        public static string KeyOverride;

        public static RunState State { get; private set; }
        /// <summary>Why the run went offline: the server's error ("OldVersion", "TooFast", …) or "Offline" for no answer.</summary>
        public static string LastError { get; private set; }
        /// <summary>The run's secret id once the server gave it, so the world list can show the run's own row.</summary>
        public static string RunId { get; private set; }

        static readonly Queue<Call> queue = new Queue<Call>();
        static bool sending;
        static int levelsReported;
        static int partialPoints;
        static bool finishing;
        // Bumped by every new run, so an answer to an older run's call is dropped.
        static int generation;

        /// <summary>The Options switch, on by default.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static string buildKey;

        /// <summary>Filled in by OnlineScores.Key.cs, which is kept out of git. Without it nothing is sent.</summary>
        static partial void ReadBuildKey(ref string key);

        static string Key
        {
            get
            {
                if (KeyOverride != null) return KeyOverride;
                if (buildKey == null)
                {
                    string key = "";
                    ReadBuildKey(ref key);
                    buildKey = key;
                }
                return buildKey;
            }
        }

        public static bool CanGoOnline => Enabled && !string.IsNullOrEmpty(Key) && (!Application.isEditor || AllowInEditor);

        /// <summary>
        /// Leaving a run without a name, as the menu opens. A run whose name is already on its way is left to finish
        /// sending; it has nothing left to wait for.
        /// </summary>
        public static void Abandon()
        {
            if (State == RunState.Online && finishing) return;
            Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            generation++;
            finishing = false;
            failedFinish = null;
            queue.Clear();
            sending = false;
            State = RunState.None;
            LastError = null;
            RunId = null;
            levelsReported = 0;
            partialPoints = 0;
        }

        /// <summary>A run starts on the first level. Anything already going on is dropped.</summary>
        public static void BeginRun()
        {
            Reset();
            if (!CanGoOnline) return;
            State = RunState.Online;
            var body = new StartBody { hash = RunLimits.Hash, platform = Platform, version = Version };
            Enqueue(new Call
            {
                action = "",
                body = JsonUtility.ToJson(body),
                answered = text => RunId = JsonUtility.FromJson<StartReply>(text)?.id
            });
        }

        /// <summary>
        /// A level of the run was finished. Call it before the time left is banked: <paramref name="points"/> is what
        /// the level paid including the bonus about to be banked, <paramref name="timeLeft"/> the clock.
        /// </summary>
        public static void LevelFinished(string scene, int points, float timeLeft)
        {
            if (State != RunState.Online) return;
            int number = Array.IndexOf(RunLimits.LevelScenes, scene) + 1;
            if (number != levelsReported + 1)
            {
                GoOffline("WrongLevel");
                return;
            }
            levelsReported = number;
            int clock = RunLimits.LevelSeconds[number - 1] * 100;
            var body = new LevelBody
            {
                level = number,
                points = points,
                centisLeft = Mathf.Clamp(Mathf.FloorToInt(timeLeft * 100f), 0, clock)
            };
            Enqueue(new Call { action = "level", body = JsonUtility.ToJson(body) });
        }

        /// <summary>The run is over: a game over with what the level it stopped in had paid so far, or 0 after the last level.</summary>
        public static void RunOver(int pointsInUnfinishedLevel)
        {
            if (State == RunState.Online) partialPoints = Mathf.Max(0, pointsInUnfinishedLevel);
        }

        /// <summary>
        /// Sends the name. <paramref name="done"/> gets the run's place, or null if the run isn't online (see
        /// <see cref="State"/> and <see cref="LastError"/>). It may be called at once or after the network answers.
        /// </summary>
        public static void Finish(string name, Action<Result> done)
        {
            if (State != RunState.Online || finishing)
            {
                done?.Invoke(null);
                return;
            }
            var body = new FinishBody { name = name ?? "", partialPoints = partialPoints };
            // A call that fails while this one waits in the queue has to answer it too.
            failedFinish = done;
            finishing = true;
            Enqueue(new Call
            {
                action = "finish",
                body = JsonUtility.ToJson(body),
                answered = text =>
                {
                    State = RunState.Finished;
                    done?.Invoke(JsonUtility.FromJson<Result>(text));
                }
            });
        }

        static Action<Result> failedFinish;

        static void Enqueue(Call call)
        {
            queue.Enqueue(call);
            SendNext();
        }

        static void SendNext()
        {
            if (sending || queue.Count == 0 || State != RunState.Online) return;
            Call call = queue.Peek();
            if (call.action.Length > 0 && string.IsNullOrEmpty(RunId))
            {
                GoOffline("Offline");
                return;
            }

            sending = true;
            int sentFor = generation;
            string path = call.action.Length == 0 ? "/api/runs" : $"/api/runs/{RunId}/{call.action}";
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "X-Signature", Sign($"POST {path}\n{call.body}") }
            };
            Transport ??= new WebTransport();
            Transport.Send(BaseUrl + path, call.body, headers, (status, text) =>
            {
                if (sentFor != generation) return;
                sending = false;
                queue.Dequeue();
                if (status < 200 || status >= 300)
                {
                    GoOffline(status == 0 ? "Offline" : ErrorOf(text));
                    return;
                }
                try
                {
                    call.answered?.Invoke(text);
                }
                catch (ArgumentException)
                {
                    GoOffline("Offline");
                    return;
                }
                if (call.action == "finish") failedFinish = null;
                SendNext();
            });
        }

        static string ErrorOf(string text)
        {
            try
            {
                string error = JsonUtility.FromJson<ErrorReply>(text)?.error;
                return string.IsNullOrEmpty(error) ? "Offline" : error;
            }
            catch (ArgumentException)
            {
                return "Offline";
            }
        }

        static void GoOffline(string error)
        {
            // An answer still on its way belongs to the run as it was and is dropped.
            generation++;
            State = RunState.Offline;
            LastError = error;
            queue.Clear();
            sending = false;
            Action<Result> waiting = failedFinish;
            failedFinish = null;
            waiting?.Invoke(null);
        }

        static string Sign(string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Key)))
            {
                byte[] digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                var hex = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        static string Platform
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        return "windows";
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        return "mac";
                    case RuntimePlatform.WebGLPlayer:
                        return "web";
                    default:
                        return "linux";
                }
            }
        }

        /// <summary>Application.version in the characters the server takes.</summary>
        static string Version
        {
            get
            {
                var version = new StringBuilder();
                foreach (char c in Application.version)
                {
                    if (version.Length == MaxVersionLength) break;
                    version.Append(char.IsLetterOrDigit(c) && c < 128 || c == '.' || c == '_' || c == '-' ? c : '-');
                }
                return version.Length > 0 ? version.ToString() : "0";
            }
        }
    }
}
