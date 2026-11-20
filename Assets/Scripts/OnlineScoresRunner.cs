using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LowercaseUppercase
{
    /// <summary>Runs <see cref="WebTransport"/>'s requests as coroutines. Added at runtime, never saved in a scene.</summary>
    public class OnlineScoresRunner : MonoBehaviour
    {
        const int TimeoutSeconds = 10;

        public void Post(string url, string body, IDictionary<string, string> headers, Action<long, string> done)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            foreach (KeyValuePair<string, string> header in headers) request.SetRequestHeader(header.Key, header.Value);
            StartCoroutine(Run(request, done));
        }

        public void Get(string url, Action<long, string> done) => StartCoroutine(Run(UnityWebRequest.Get(url), done));

        static IEnumerator Run(UnityWebRequest request, Action<long, string> done)
        {
            using (request)
            {
                request.timeout = TimeoutSeconds;
                yield return request.SendWebRequest();

                bool answered = request.result == UnityWebRequest.Result.Success
                    || request.result == UnityWebRequest.Result.ProtocolError;
                done(answered ? request.responseCode : 0, request.downloadHandler?.text ?? "");
            }
        }
    }
}
