using System;
using System.Collections.Generic;

namespace LowercaseUppercase
{
    /// <summary>How <see cref="OnlineScores"/> reaches the server. Tests answer from a fake instead of the network.</summary>
    public interface IScoreTransport
    {
        /// <summary>Posts <paramref name="body"/> and calls <paramref name="done"/> with the HTTP status (0 when no answer
        /// came) and the reply's text.</summary>
        void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done);

        /// <summary>Fetches <paramref name="url"/>, answering like <see cref="Send"/>.</summary>
        void Get(string url, Action<long, string> done);
    }
}
