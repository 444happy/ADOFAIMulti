using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ADOFAIMulti
{
    /// <summary>
    /// "멀티 섬": 게임 화면과 분리된 멀티 전용 공간.
    ///
    /// 메뉴 씬을 내리고 다시 불러오면 게임의 화면 전환(와이프) 처리가 꼬여 검은 화면이 남는다.
    /// 그래서 씬은 그대로 두고, 섬에 있는 동안 메뉴 씬의 루트 오브젝트를 전부 꺼둔다(SetActive false).
    /// → 메뉴의 입력/업데이트/카메라/소리가 완전히 멈추고, 나갈 때는 다시 켜기만 하면 즉시 원래 메뉴로 돌아온다.
    /// </summary>
    public static class Island
    {
        public static bool InIsland;
        /// <summary>전환 중 (화면 가림)</summary>
        public static bool Busy;
        public static float Cover;          // 0~1 화면 가림 정도
        public static string CoverText = "";
        static MonoBehaviour _runner;
        static GameObject _camGo;

        // 꺼둔 메뉴 오브젝트 / 멈춘 소리 (나갈 때 되돌림)
        static readonly List<GameObject> _hidden = new List<GameObject>();
        struct AudioState { public AudioSource src; public float time; }
        static readonly List<AudioState> _audio = new List<AudioState>();

        public static void Init(MonoBehaviour runner) { _runner = runner; }

        // ---------------- 입장 ----------------
        public static void Enter()
        {
            if (InIsland || Busy || Game.IsScnGame) return;
            _runner.StartCoroutine(EnterCo("멀티 섬으로 이동 중..."));
        }

        static IEnumerator EnterCo(string text)
        {
            Busy = true; CoverText = text;
            yield return Fade(1f);
            HideGame();
            InIsland = true;
            yield return Fade(0f);
            Busy = false;
        }

        static void HideGame()
        {
            _hidden.Clear(); _audio.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    if (!root.activeSelf) continue;
                    foreach (var a in root.GetComponentsInChildren<AudioSource>())
                        if (a.isPlaying) _audio.Add(new AudioState { src = a, time = a.time });
                    root.SetActive(false);
                    _hidden.Add(root);
                }
            }

            // 섬 전용 카메라 (DontDestroyOnLoad → 메뉴 씬과 무관)
            _camGo = new GameObject("ADOFAIMulti_IslandCamera");
            UnityEngine.Object.DontDestroyOnLoad(_camGo);
            var cam = _camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Current.Bg;
            cam.orthographic = true;
            cam.depth = 100;
            cam.cullingMask = 0;          // 그릴 3D 오브젝트 없음 (UI는 IMGUI)
            Main.Log($"Island: hid {_hidden.Count} root objects, paused {_audio.Count} audio sources");
        }

        static void ShowGame()
        {
            if (_camGo != null) UnityEngine.Object.Destroy(_camGo);
            _camGo = null;
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            foreach (var a in _audio)
            {
                if (a.src == null || a.src.clip == null) continue;
                try { a.src.time = Mathf.Min(a.time, a.src.clip.length - 0.01f); a.src.Play(); } catch { }
            }
            _hidden.Clear(); _audio.Clear();
            InIsland = false;
        }

        /// <summary>모드를 끌 때 등: 즉시 메뉴 복구</summary>
        public static void Restore()
        {
            if (InIsland) ShowGame();
            Cover = 0; Busy = false;
        }

        // ---------------- 퇴장 (원래 메뉴로) ----------------
        public static void Leave()
        {
            if (!InIsland || Busy) return;
            _runner.StartCoroutine(LeaveCo());
        }

        static IEnumerator LeaveCo()
        {
            Busy = true; CoverText = "메인 메뉴로 돌아가는 중...";
            yield return Fade(1f);
            ShowGame();
            yield return null;
            yield return Fade(0f);
            Busy = false;
        }

        // ---------------- 레벨 플레이 (섬 → 메뉴 다시 켬(가림) → 레벨) ----------------
        public static void PlayLevel(string path, Action<bool, string> done)
        {
            _runner.StartCoroutine(PlayCo(path, done));
        }

        static IEnumerator PlayCo(string path, Action<bool, string> done)
        {
            Busy = true; CoverText = "맵 불러오는 중...";
            yield return Fade(1f);
            if (InIsland) { ShowGame(); yield return null; yield return null; }
            string err;
            bool ok = Game.LoadLevelNow(path, out err);
            done?.Invoke(ok, err);
            if (!ok) { Busy = false; yield return EnterCo("멀티 섬으로 돌아가는 중..."); yield break; }
            // 게임 씬이 뜰 때까지 가림 유지
            float t = 0;
            while (!Game.IsScnGame && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Fade(0f);
            Busy = false;
        }

        // ---------------- 레벨 → 섬 복귀 ----------------
        public static void ReturnFromLevel()
        {
            if (Busy) return;
            _runner.StartCoroutine(ReturnCo());
        }

        static IEnumerator ReturnCo()
        {
            Busy = true; CoverText = "멀티 섬으로 돌아가는 중...";
            yield return Fade(1f);
            if (Game.IsScnGame)
            {
                Game.QuitToMenu();
                float t = 0;
                while (Game.IsScnGame && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
                yield return WaitMenu();
            }
            HideGame();
            InIsland = true;
            yield return Fade(0f);
            Busy = false;
        }

        // ---------------- helpers ----------------
        static IEnumerator WaitMenu()
        {
            float t = 0;
            while (t < 15f)
            {
                t += Time.unscaledDeltaTime;
                bool ready = false;
                try { ready = ADOBase.controller != null && !Game.IsScnGame; } catch { }
                if (ready) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.6f); // 메뉴 등장 연출이 끝날 여유
        }

        static IEnumerator Fade(float target)
        {
            while (!Mathf.Approximately(Cover, target))
            {
                Cover = Mathf.MoveTowards(Cover, target, Time.unscaledDeltaTime * 4f);
                yield return null;
            }
        }
    }
}
