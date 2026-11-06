using System;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAIMulti
{
    public static class Main
    {
        public const string Version = "0.6.1";
        public static UnityModManager.ModEntry Mod;
        public static Harmony Harmony;
        public static bool Enabled;
        static GameObject _host;

        public static void Log(string s) { Mod?.Logger.Log(s); }

        // UMM EntryMethod
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Mod.OnToggle = OnToggle;
            Mod.OnGUI = _ =>
            {
                GUILayout.Label("메인 메뉴 오른쪽 아래 [멀티 섬으로] 또는 Ctrl + M 으로 멀티 섬에 들어갑니다. (v" + Version + ")");
                GUILayout.Label("업데이트: " + System.Text.RegularExpressions.Regex.Replace(Updater.StatusText, "<[^>]+>", ""));
            };
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry e, bool value)
        {
            if (value == Enabled) return true;
            Enabled = value;
            if (value)
            {
                Harmony = new Harmony(e.Info.Id);
                Harmony.PatchAll(typeof(Main).Assembly);
                _host = new GameObject("ADOFAIMulti");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                var runner = _host.AddComponent<Runner>();
                Island.Init(runner);
                Theme.Load();
                Net.Init();
                Updater.CleanupOld();
                runner.StartCoroutine(Updater.CheckAndUpdate());
            }
            else
            {
                Session.LeaveRoom();
                Island.Restore();
                Net.Shutdown();
                Harmony?.UnpatchAll(e.Info.Id);
                if (_host != null) UnityEngine.Object.Destroy(_host);
            }
            return true;
        }
    }

    /// <summary>매 프레임 네트워크/세션을 돌리고 화면을 그리는 컴포넌트</summary>
    public class Runner : MonoBehaviour
    {
        void Update()
        {
            try
            {
                MainThread.Drain();
                Net.Poll();
                Session.Update();
                // 멀티 로비는 메뉴 화면에서만 열 수 있음 (레벨 안에서는 안 열림)
                if (Input.GetKeyDown(KeyCode.M) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !Game.IsScnGame)
                {
                    if (!Island.InIsland) Island.Enter();
                    else if (!Session.InRoom) Island.Leave();
                }
            }
            catch (Exception ex) { Main.Log("Update error: " + ex); }
        }

        void OnGUI()
        {
            try { UI.Draw(); }
            catch (Exception ex) { Main.Log("GUI error: " + ex); }
        }
    }
}
