using System;
using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Sends the online high score calls with UnityWebRequest, from a hidden runner that survives scene loads.</summary>
    public class WebTransport : IScoreTransport
    {
        OnlineScoresRunner runner;

        public void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done)
        {
            if (runner == null)
            {
                var go = new GameObject("Online Scores") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<OnlineScoresRunner>();
            }
            runner.Post(url, body, headers, done);
        }
    }
}
