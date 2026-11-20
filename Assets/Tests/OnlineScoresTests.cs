using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// The game's side of the online high scores, against a fake server: what is sent, in which order, signed how, and
    /// that anything going wrong turns the run quietly offline.
    /// </summary>
    public class OnlineScoresTests
    {
        const string Key = "test key";
        const string RunId = "0123456789abcdef0123456789abcdef";

        class Sent
        {
            public string url;
            public string body;
            public IDictionary<string, string> headers;
            public Action<long, string> done;
        }

        /// <summary>Keeps every call; answers at once with <see cref="Answer"/>, or later by hand when it is null.</summary>
        class FakeServer : IScoreTransport
        {
            public readonly List<Sent> calls = new List<Sent>();
            public Func<Sent, (long, string)> Answer = sent => sent.url.EndsWith("/api/runs")
                ? (201, $"{{\"id\":\"{RunId}\"}}")
                : sent.url.EndsWith("/finish")
                    ? (200, "{\"rank\":3,\"count\":12,\"total\":700,\"name\":\"zoe\",\"nameRefused\":false}")
                    : (200, "{\"levelsDone\":1}");

            public void Get(string url, Action<long, string> done) => done(200, "{\"count\":0,\"offset\":0,\"scores\":[]}");

            public void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done)
            {
                var sent = new Sent { url = url, body = body, headers = headers, done = done };
                calls.Add(sent);
                if (Answer == null) return;
                (long status, string text) = Answer(sent);
                done(status, text);
            }
        }

        FakeServer server;
        bool wasEnabled;

        [SetUp]
        public void UseAFakeServer()
        {
            wasEnabled = OnlineScores.Enabled;
            OnlineScores.Enabled = true;
            server = new FakeServer();
            OnlineScores.Transport = server;
            OnlineScores.AllowInEditor = true;
            OnlineScores.KeyOverride = Key;
            OnlineScores.BeginRun();
            OnlineScores.Abandon();
            server.calls.Clear();
        }

        [TearDown]
        public void PutTheRealOneBack()
        {
            OnlineScores.Abandon();
            OnlineScores.Transport = null;
            OnlineScores.AllowInEditor = false;
            OnlineScores.KeyOverride = null;
            OnlineScores.Enabled = wasEnabled;
        }

        static string Path(Sent sent) => sent.url.Substring(OnlineScores.BaseUrl.Length);

        static string Signature(string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Key)))
            {
                var hex = new StringBuilder();
                foreach (byte b in hmac.ComputeHash(Encoding.UTF8.GetBytes(message))) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        [Test]
        public void ARunStartsWithTheLevelsHashAndSignsEveryCall()
        {
            OnlineScores.BeginRun();
            Assert.AreEqual(1, server.calls.Count);
            Sent start = server.calls[0];
            Assert.AreEqual("/api/runs", Path(start));
            StringAssert.Contains($"\"hash\":\"{RunLimits.Hash}\"", start.body);
            StringAssert.Contains("\"platform\":\"", start.body);
            Assert.AreEqual(Signature($"POST /api/runs\n{start.body}"), start.headers["X-Signature"]);
            Assert.AreEqual(OnlineScores.RunState.Online, OnlineScores.State);
            Assert.AreEqual(RunId, OnlineScores.RunId);
        }

        [Test]
        public void EachLevelIsReportedWithItsPointsAndTheClockInHundredths()
        {
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Overworld", 700, 40.004f);
            OnlineScores.LevelFinished("Crack", 450, 0.5f);

            Assert.AreEqual(3, server.calls.Count);
            Assert.AreEqual($"/api/runs/{RunId}/level", Path(server.calls[1]));
            Assert.AreEqual("{\"level\":1,\"points\":700,\"centisLeft\":4000}", server.calls[1].body);
            Assert.AreEqual("{\"level\":2,\"points\":450,\"centisLeft\":50}", server.calls[2].body);
            Assert.AreEqual(Signature($"POST /api/runs/{RunId}/level\n{server.calls[2].body}"),
                server.calls[2].headers["X-Signature"]);
        }

        [Test]
        public void CallsWaitForTheOneBefore()
        {
            server.Answer = null;
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Overworld", 700, 40f);
            Assert.AreEqual(1, server.calls.Count, "The level waits for the run's id.");

            server.calls[0].done(201, $"{{\"id\":\"{RunId}\"}}");
            Assert.AreEqual(2, server.calls.Count);
            StringAssert.EndsWith($"/{RunId}/level", server.calls[1].url);
        }

        [Test]
        public void TheNameEndsTheRunWithThePointsOfTheLevelAGameOverStoppedIn()
        {
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Overworld", 700, 40f);
            OnlineScores.RunOver(150);
            OnlineScores.Result result = null;
            OnlineScores.Finish("zoe", r => result = r);

            Sent finish = server.calls[2];
            Assert.AreEqual($"/api/runs/{RunId}/finish", Path(finish));
            Assert.AreEqual("{\"name\":\"zoe\",\"partialPoints\":150}", finish.body);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.rank);
            Assert.AreEqual(12, result.count);
            Assert.AreEqual(OnlineScores.RunState.Finished, OnlineScores.State);
        }

        [Test]
        public void ARefusalTurnsTheRestOfTheRunOfflineAndTheNameIsAnsweredWithNothing()
        {
            server.Answer = sent => sent.url.EndsWith("/api/runs") ? (201, $"{{\"id\":\"{RunId}\"}}") : (422, "{\"error\":\"TooFast\"}");
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Overworld", 700, 40f);
            OnlineScores.LevelFinished("Crack", 700, 40f);

            Assert.AreEqual(2, server.calls.Count, "Nothing more goes out after a refusal.");
            Assert.AreEqual(OnlineScores.RunState.Offline, OnlineScores.State);
            Assert.AreEqual("TooFast", OnlineScores.LastError);
            bool answered = false;
            OnlineScores.Finish("zoe", r => { answered = true; Assert.IsNull(r); });
            Assert.IsTrue(answered);
        }

        [Test]
        public void NoAnswerAtAllIsOfflineToo()
        {
            server.Answer = _ => (0, "");
            OnlineScores.BeginRun();
            Assert.AreEqual(OnlineScores.RunState.Offline, OnlineScores.State);
            Assert.AreEqual("Offline", OnlineScores.LastError);
        }

        [Test]
        public void AnOldBuildIsToldSo()
        {
            server.Answer = _ => (409, "{\"error\":\"OldVersion\"}");
            OnlineScores.BeginRun();
            Assert.AreEqual("OldVersion", OnlineScores.LastError);
        }

        [Test]
        public void ANameWaitingBehindALevelThatFailsIsStillAnswered()
        {
            server.Answer = null;
            OnlineScores.BeginRun();
            server.calls[0].done(201, $"{{\"id\":\"{RunId}\"}}");
            OnlineScores.LevelFinished("Overworld", 700, 40f);
            bool answered = false;
            OnlineScores.Finish("zoe", r => { answered = true; Assert.IsNull(r); });

            server.calls[1].done(0, "");
            Assert.IsTrue(answered);
            Assert.AreEqual(2, server.calls.Count);
        }

        [Test]
        public void ALevelOutOfOrderTakesTheRunOffline()
        {
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Crack", 700, 40f);
            Assert.AreEqual(1, server.calls.Count);
            Assert.AreEqual("WrongLevel", OnlineScores.LastError);
        }

        [Test]
        public void OffInOptionsOrWithoutAKeyOrInTheEditorNothingIsSent()
        {
            OnlineScores.Enabled = false;
            OnlineScores.BeginRun();
            OnlineScores.Enabled = true;
            OnlineScores.KeyOverride = "";
            OnlineScores.BeginRun();
            OnlineScores.KeyOverride = Key;
            OnlineScores.AllowInEditor = false;
            OnlineScores.BeginRun();
            OnlineScores.LevelFinished("Overworld", 700, 40f);

            Assert.AreEqual(0, server.calls.Count);
            Assert.AreEqual(OnlineScores.RunState.None, OnlineScores.State);
        }

        [Test]
        public void TheMenuDropsARunButLetsANameOnItsWayGo()
        {
            OnlineScores.BeginRun();
            OnlineScores.Abandon();
            OnlineScores.LevelFinished("Overworld", 700, 40f);
            Assert.AreEqual(1, server.calls.Count, "Levels after leaving aren't reported.");

            server.Answer = null;
            OnlineScores.BeginRun();
            server.calls[1].done(201, $"{{\"id\":\"{RunId}\"}}");
            OnlineScores.Finish("zoe", null);
            OnlineScores.Abandon();
            server.calls[2].done(200, "{\"rank\":1,\"count\":1,\"total\":0,\"name\":\"zoe\",\"nameRefused\":false}");
            Assert.AreEqual(OnlineScores.RunState.Finished, OnlineScores.State);
        }

        [Test]
        public void AnAnswerToAnOlderRunIsIgnored()
        {
            server.Answer = null;
            OnlineScores.BeginRun();
            Sent old = server.calls[0];
            OnlineScores.BeginRun();
            old.done(201, "{\"id\":\"ffffffffffffffffffffffffffffffff\"}");
            Assert.IsNull(OnlineScores.RunId);
        }
    }
}
