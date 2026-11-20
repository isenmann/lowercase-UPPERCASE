using System;
using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Makes the online high score calls with UnityWebRequest, from a hidden runner that survives scene loads.</summary>
    public class WebTransport : IScoreTransport
    {
        OnlineScoresRunner runner;

        public void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done) =>
            Runner.Post(url, body, headers, done);

        public void Get(string url, Action<long, string> done) => Runner.Get(url, done);

        OnlineScoresRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("Online Scores") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    runner = go.AddComponent<OnlineScoresRunner>();
                }
                return runner;
            }
        }
    }
}
