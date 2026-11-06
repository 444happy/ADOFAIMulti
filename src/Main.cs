using System;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAIMulti
{
    public static class Main
    {
        public const string Version = "0.10.0";
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
                GUILayout.Label("메인 섬에 생기는 \"멀티\" 포탈로 이동하면 멀티 섬에 들어갑니다. (v" + Version + ")");
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
                Updater.CleanupOld();
                runner.StartCoroutine(Net.LoadRelayUrl());
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
        public static Runner Instance;
        void Awake() { Instance = this; }

        void Update()
        {
            try
            {
                MainThread.Drain();
                Net.RetryInit();
                Net.Poll();
                Theme.SaveIfDirty();
                Updater.Tick(this);
                Session.Update();
                // 멀티 섬 입장은 메인 섬의 멀티 포탈로만
                PortalTile.Tick();
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
