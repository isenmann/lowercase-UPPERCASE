using System;
using System.Collections.Generic;

namespace LowercaseUppercase
{
    /// <summary>How <see cref="OnlineScores"/> sends a POST. Tests answer from a fake instead of the network.</summary>
    public interface IScoreTransport
    {
        /// <summary>Posts <paramref name="body"/> and calls <paramref name="done"/> with the HTTP status (0 when no answer
        /// came) and the reply's text.</summary>
        void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done);
    }
}
