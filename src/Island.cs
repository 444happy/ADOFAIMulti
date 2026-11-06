using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ADOFAIMulti
{
    /// <summary>
    /// "멀티 섬": 게임의 메인 메뉴(섬)와 완전히 분리된 런타임 씬.
    /// 게임 씬을 전부 내리고 우리 씬만 남기므로 게임 입력/오브젝트와 섞이지 않는다.
    /// 레벨 플레이는 메인 메뉴를 (화면을 가린 채) 잠깐 거쳐 게임 정식 경로로 로드한다.
    /// </summary>
    public static class Island
    {
        public const string SceneName = "ADOFAIMulti_Island";

        public static bool InIsland;
        /// <summary>전환 중 (화면 가림)</summary>
        public static bool Busy;
        public static float Cover;          // 0~1 화면 가림 정도
        public static string CoverText = "";
        static MonoBehaviour _runner;
        static Camera _cam;

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
            try { DG.Tweening.DOTween.KillAll(); } catch { }

            var prev = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene(SceneName + "_" + Time.frameCount);
            SceneManager.SetActiveScene(scene);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != scene && s.isLoaded) SceneManager.UnloadSceneAsync(s);
            }
            yield return null;
            yield return null;

            var go = new GameObject("MultiIslandCamera");
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.035f, 0.06f, 0.05f);
            _cam.orthographic = true;
            _cam.depth = 100;
            _cam.cullingMask = 0;          // 그릴 3D 오브젝트 없음 (UI는 IMGUI)
            go.AddComponent<AudioListener>();
            Time.timeScale = 1f;
            AudioListener.pause = false;

            InIsland = true;
            if (Session.Results.Count > 0 && Session.Phase == Phase.Results) UI.Page = UI.Tab.Results;
            else if (UI.Page == UI.Tab.Home && Session.InRoom) UI.Page = UI.Tab.Room;
            yield return Fade(0f);
            Busy = false;
        }

        // ---------------- 퇴장 (메인 메뉴로) ----------------
        public static void Leave()
        {
            if (!InIsland || Busy) return;
            _runner.StartCoroutine(LeaveCo());
        }

        static IEnumerator LeaveCo()
        {
            Busy = true; CoverText = "메인 메뉴로 돌아가는 중...";
            yield return Fade(1f);
            InIsland = false;
            GoMenu();
            yield return WaitMenu();
            yield return Fade(0f);
            Busy = false;
        }

        // ---------------- 레벨 플레이 (섬 → 메뉴(가림) → 레벨) ----------------
        public static void PlayLevel(string path, Action<bool, string> done)
        {
            _runner.StartCoroutine(PlayCo(path, done));
        }

        static IEnumerator PlayCo(string path, Action<bool, string> done)
        {
            Busy = true; CoverText = "맵 불러오는 중...";
            yield return Fade(1f);
            if (InIsland) { InIsland = false; GoMenu(); yield return WaitMenu(); }
            string err;
            bool ok = Game.LoadLevelNow(path, out err);
            done?.Invoke(ok, err);
            if (!ok) { yield return EnterCo("멀티 섬으로 돌아가는 중..."); yield break; }
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
            Busy = false;
            yield return EnterCo("멀티 섬으로 돌아가는 중...");
        }

        // ---------------- helpers ----------------
        static void GoMenu()
        {
            try { ADOBase.GoToLevelSelect(); }
            catch { SceneManager.LoadScene(GCNS.sceneLevelSelect); }
        }

        static IEnumerator WaitMenu()
        {
            float t = 0;
            while (t < 15f)
            {
                t += Time.unscaledDeltaTime;
                bool ready = false;
                try { ready = ADOBase.controller != null && !Game.IsScnGame && SceneManager.GetActiveScene().name.StartsWith("scn"); } catch { }
                if (ready) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f); // 메뉴 초기화 여유
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
