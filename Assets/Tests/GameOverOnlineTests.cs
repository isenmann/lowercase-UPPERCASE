using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// The game over screen with the world list: a run that went online always asks for a name, offered as the last
    /// one typed, sends it, and shows its place among the top runs. Against a fake server.
    /// </summary>
    public class GameOverOnlineTests
    {
        const string RunId = "0123456789abcdef0123456789abcdef";

        class FakeServer : IScoreTransport
        {
            public readonly List<string> posted = new List<string>();
            public readonly List<string> fetched = new List<string>();
            public string startAnswer = $"{{\"id\":\"{RunId}\"}}";
            public long startStatus = 201;

            public void Send(string url, string body, IDictionary<string, string> headers, Action<long, string> done)
            {
                posted.Add(url.Substring(url.IndexOf("/api/", StringComparison.Ordinal)) + " " + body);
                if (url.EndsWith("/api/runs")) done(startStatus, startAnswer);
                else if (url.EndsWith("/finish")) done(200, "{\"rank\":37,\"count\":512,\"total\":100,\"name\":\"zoex\",\"nameRefused\":false}");
                else done(200, "{\"levelsDone\":1}");
            }

            public void Get(string url, Action<long, string> done)
            {
                fetched.Add(url);
                IEnumerable<string> top = Enumerable.Range(1, 10).Select(i =>
                    $"{{\"rank\":{i},\"name\":\"top{i}\",\"total\":{10000 - i * 100},\"levels\":15,\"platform\":\"web\",\"date\":\"2026-11-20\",\"marked\":false}}");
                done(200, $"{{\"count\":512,\"offset\":0,\"scores\":[{string.Join(",", top)}],\"you\":{{\"rank\":0}}}}");
            }
        }

        FakeServer server;
        bool wasEnabled;
        PrototypeFlow flow;
        GameOverScreen Screen => flow.gameOver;

        [SetUp]
        public void UseAFakeServer()
        {
            LevelRun.NewGame();
            HighScores.StorageKey = HighScores.DefaultStorageKey + ".tests";
            HighScores.Clear();
            wasEnabled = OnlineScores.Enabled;
            OnlineScores.Enabled = true;
            server = new FakeServer();
            OnlineScores.Transport = server;
            OnlineScores.AllowInEditor = true;
            OnlineScores.KeyOverride = "test key";
            OnlineScores.Abandon();
        }

        [TearDown]
        public void PutTheRealOneBack()
        {
            OnlineScores.Abandon();
            OnlineScores.Transport = null;
            OnlineScores.AllowInEditor = false;
            OnlineScores.KeyOverride = null;
            OnlineScores.Enabled = wasEnabled;
            HighScores.Clear();
            HighScores.StorageKey = HighScores.DefaultStorageKey;
        }

        IEnumerator LoadCrack()
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync("Crack");
            yield return null;
            flow = UnityEngine.Object.FindAnyObjectByType<PrototypeFlow>();
        }

        static void FillTheLocalTable()
        {
            for (int i = 1; i <= HighScores.Size; i++) HighScores.Insert(i * 1000);
        }

        string Heading => Screen.transform.Find("High Scores Heading").GetComponent<TMPro.TMP_Text>().text;

        [UnityTest]
        public IEnumerator AnOnlineRunAsksForANameEveryTimeOfferingTheLastOne()
        {
            FillTheLocalTable();
            HighScores.LastName = "zoe";
            yield return LoadCrack();
            OnlineScores.BeginRun();

            Screen.Show(100, gameOver: true);
            Assert.AreEqual(-1, Screen.Place, "100 doesn't make the player's own table.");
            Assert.IsTrue(Screen.IsEnteringName, "An online run always asks for a name.");
            Assert.AreEqual("zoe", Screen.TypedName, "The last name is offered again.");
            Assert.AreEqual(GameOverScreen.View.World, Screen.Showing);

            Screen.Type('x');
            Screen.ConfirmName();
            Assert.That(server.posted.Last(), Does.StartWith($"/api/runs/{RunId}/finish {{\"name\":\"zoex\""));
            Assert.AreEqual("zoex", HighScores.LastName);
            Assert.AreEqual(1000, HighScores.Load().Last().score, "The player's own table is left alone.");
            Assert.That(server.fetched.Last(), Does.Contain($"run={RunId}"));

            Screen.SendMessage("Update");
            StringAssert.Contains("#37 of 512", Heading);
            string own = Screen.OwnRow.text;
            StringAssert.Contains("37", own);
            StringAssert.Contains("zoex", own);
            Assert.AreEqual(Screen.newRowColor, Screen.OwnRow.color);
            StringAssert.Contains("top1", Screen.rows[0].text);
            StringAssert.Contains("top10", Screen.rows.Last().text, "The whole top 10 stays in view.");

            Screen.SwitchView();
            Assert.AreEqual("", Screen.OwnRow.text, "The player's own table has no extra line.");
        }

        [UnityTest]
        public IEnumerator ALocalHighScoreShowsFirstAndTheWorldPlaceIsOneSwitchAway()
        {
            HighScores.LastName = "zoe";
            yield return LoadCrack();
            OnlineScores.BeginRun();

            Screen.Show(100, gameOver: true);
            Assert.AreEqual(0, Screen.Place);
            Assert.AreEqual(GameOverScreen.View.Local, Screen.Showing);
            Screen.ConfirmName();
            Assert.AreEqual("zoe", HighScores.Load()[0].name);

            Screen.SwitchView();
            Assert.AreEqual(GameOverScreen.View.World, Screen.Showing);
            StringAssert.Contains("#37 of 512", Heading);
            Screen.SwitchView();
            Assert.AreEqual("your high scores", Heading);
        }

        [UnityTest]
        public IEnumerator WithOnlineScoresOffNothingChangesButTheWorldListSaysWhy()
        {
            FillTheLocalTable();
            OnlineScores.Enabled = false;
            yield return LoadCrack();
            OnlineScores.BeginRun();

            Screen.Show(100, gameOver: true);
            Assert.IsFalse(Screen.IsEnteringName);
            Assert.AreEqual(GameOverScreen.View.Local, Screen.Showing);
            Screen.SwitchView();
            Assert.That(Screen.rows.Select(r => r.text), Has.Member("online scores are off (Options)"));
            Assert.IsEmpty(server.posted);
            Assert.IsEmpty(server.fetched);
        }

        [UnityTest]
        public IEnumerator AnOldBuildPlaysLocallyAndIsToldWhy()
        {
            FillTheLocalTable();
            server.startStatus = 409;
            server.startAnswer = "{\"error\":\"OldVersion\"}";
            yield return LoadCrack();
            OnlineScores.BeginRun();

            Screen.Show(100, gameOver: true);
            Assert.IsFalse(Screen.IsEnteringName, "A run that isn't online asks for no name it can't use.");
            Screen.SwitchView();
            StringAssert.Contains("online scores need the latest version", Screen.hint.text);
            StringAssert.Contains("top1", Screen.rows[0].text, "The world list is still shown.");
        }

        [UnityTest]
        public IEnumerator ARunStartedFromALaterChapterSaysOnlyFirstLevelRunsCount()
        {
            FillTheLocalTable();
            yield return LoadCrack();

            Screen.Show(100, gameOver: true);
            Assert.IsFalse(Screen.IsEnteringName);
            Screen.SwitchView();
            StringAssert.Contains("only runs from the first level go online", Screen.hint.text);
        }
    }
}
