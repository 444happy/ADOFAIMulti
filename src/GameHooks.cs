using System;
using HarmonyLib;

namespace ADOFAIMulti
{
    /// <summary>얼불춤 내부와 맞닿는 부분은 전부 여기에 모아둠 (게임 업데이트로 깨지면 여기만 고치면 됨)</summary>
    public static class Game
    {
        /// <summary>true면 "아무 키나 눌러 시작"이 막힌다</summary>
        public static bool GateClosed;
        /// <summary>true면 다음 프레임에 자동으로 시작 입력을 넣는다 (GO 신호)</summary>
        public static bool AutoStartOnce;
        /// <summary>레벨 로딩이 끝나고 시작 입력을 기다리는 중인 걸 감지했는지</summary>
        public static bool WaitingSeen;
        /// <summary>이번 판에서 레벨 클리어(Won)를 감지했는지</summary>
        public static bool WonSeen;

        static bool? _savedNoFail;
        static Difficulty? _savedDiff;

        /// <summary>멀티 로비가 메인 메뉴 위에 떠 있는 상태</summary>
        public static bool MenuFrozen => (Island.InIsland || Island.Busy) && !IsScnGame;
        /// <summary>메뉴 컨트롤러 Update 자체를 멈출지 (섬 안에 있을 때만 — 레벨 로딩 중엔 멈추면 안 됨)</summary>
        public static bool MenuPaused => Island.InIsland && !IsScnGame;

        public static bool IsScnGame { get { try { return ADOBase.isScnGame; } catch { return false; } } }
        public static bool IsMainMenu { get { try { return ADOBase.isLevelSelect; } catch { return false; } } }

        public static void QuitToMenu()
        {
            try { var c = ADOBase.controller; if (c != null && IsScnGame) c.QuitToMainMenu(); } catch (Exception ex) { Main.Log("QuitToMenu: " + ex.Message); }
        }

        public static bool InGame
        {
            get { try { return ADOBase.controller != null && ADOBase.customLevel != null; } catch { return false; } }
        }

        public static string DefaultLevelPath()
        {
            try
            {
                if (GCS.customLevelPaths != null && GCS.customLevelPaths.Length > 0 && !string.IsNullOrEmpty(GCS.customLevelPaths[0]))
                    return GCS.customLevelPaths[0];
                return ADOBase.levelPath ?? "";
            }
            catch { return ""; }
        }

        /// <summary>무적 + 배속/연습 해제 후 커스텀 레벨 로드</summary>
        public static bool LoadLevelNow(string path, out string error)
        {
            error = null;
            var ctrl = ADOBase.controller;
            if (ctrl == null) { error = "메인 메뉴나 커스텀 레벨 화면에서 다시 시도"; return false; }
            if (_savedNoFail == null) _savedNoFail = GCS.useNoFail;
            if (_savedDiff == null) _savedDiff = GCS.difficulty;
            GCS.difficulty = (Difficulty)Session.Judge;
            GCS.useNoFail = true;
            GCS.speedTrialMode = false;
            GCS.practiceMode = false;
            GateClosed = true;
            AutoStartOnce = false;
            WaitingSeen = false;
            WonSeen = false;
            ctrl.LoadCustomLevel(path, "", false);
            return true;
        }

        /// <summary>레벨 안에서 매 프레임 무적 유지</summary>
        public static void KeepNoFail()
        {
            try { var c = ADOBase.controller; if (c != null) c.noFail = true; } catch { }
            try { if (GCS.difficulty != (Difficulty)Session.Judge) GCS.difficulty = (Difficulty)Session.Judge; } catch { }
        }

        public static void RestoreSettings()
        {
            GateClosed = false;
            AutoStartOnce = false;
            if (_savedNoFail.HasValue) { GCS.useNoFail = _savedNoFail.Value; _savedNoFail = null; }
            if (_savedDiff.HasValue) { try { GCS.difficulty = _savedDiff.Value; } catch { } _savedDiff = null; }
        }

        public static bool IsWon()
        {
            if (WonSeen) return true;
            try { var c = ADOBase.controller; return c != null && c.currentState == States.Won; } catch { return false; }
        }

        public static Stats ReadStats()
        {
            var s = new Stats();
            try
            {
                var ctrl = ADOBase.controller;
                if (ctrl == null) return s;
                var mm = ctrl.mistakesManager;
                if (mm != null)
                {
                    s.Acc = mm.percentAcc;
                    s.XAcc = mm.percentXAcc;
                }
                s.Progress = ctrl.percentComplete;
                var trackers = scrMistakesManager.marginTrackers;
                if (trackers != null && trackers.Length > 0 && trackers[0] != null)
                {
                    var t = trackers[0];
                    s.TooEarly = t.GetHits(HitMargin.TooEarly);
                    s.VeryEarly = t.GetHits(HitMargin.VeryEarly);
                    s.EarlyPerfect = t.GetHits(HitMargin.EarlyPerfect);
                    s.Perfect = t.GetHits(HitMargin.PerfectMinus) + t.GetHits(HitMargin.XPerfect) + t.GetHits(HitMargin.PerfectPlus);
                    s.LatePerfect = t.GetHits(HitMargin.LatePerfect);
                    s.VeryLate = t.GetHits(HitMargin.VeryLate);
                    s.TooLate = t.GetHits(HitMargin.TooLate);
                    s.Miss = t.GetHits(HitMargin.FailMiss) + t.GetDeaths();
                    s.Overload = t.GetHits(HitMargin.FailOverload);
                }
            }
            catch (Exception ex) { Main.Log("ReadStats: " + ex.Message); }
            return s;
        }
    }

    // 레벨 시작 대기(WaitForStartCo)는 이 메서드로 "아무 키" 입력을 확인한다.
    // 실제 플레이 판정은 scrPlayer 쪽을 써서 여기를 막아도 플레이에는 영향 없음.
    [HarmonyPatch(typeof(scrPlayerManager), nameof(scrPlayerManager.AnyValidInputWasTriggered))]
    static class Patch_StartGate
    {
        static bool Prefix(ref bool __result)
        {
            if (Game.AutoStartOnce) { Game.AutoStartOnce = false; __result = true; return false; }
            if (Game.GateClosed) { Game.WaitingSeen = true; __result = false; return false; }
            // 완주 후 결과 대기 중엔 키를 눌러도 레벨을 못 나가게 / 로비가 떠 있으면 메뉴 입력 차단
            if ((Session.Phase == Phase.Results && Game.InGame) || Game.MenuFrozen) { __result = false; return false; }
            return true;
        }
    }

    [HarmonyPatch(typeof(scrController), "Won_Enter")]
    static class Patch_Won
    {
        static void Postfix() { Game.WonSeen = true; }
    }
}

namespace ADOFAIMulti
{
    // ---- 멀티 도중 ESC(일시정지) 막기. 이미 멈춰 있으면 풀리는 건 허용 ----
    [HarmonyPatch(typeof(scrController), nameof(scrController.TogglePauseGame))]
    static class Patch_NoPause
    {
        static bool Prefix(scrController __instance, ref bool __result)
        {
            if (Session.InMatch && Game.InGame && !__instance.paused) { __result = false; return false; }
            return true;
        }
    }

    // ---- 멀티 도중 재시작(R) 막기 ----
    [HarmonyPatch(typeof(scrController), nameof(scrController.Restart))]
    static class Patch_NoRestart
    {
        static bool Prefix()
        {
            return !(Game.InGame && (Session.Phase == Phase.Countdown || Session.Phase == Phase.Playing || Session.Phase == Phase.Results));
        }
    }

    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.ValidInputWasTriggered))]
    static class Patch_FreezeMenuInput
    {
        static bool Prefix(ref bool __result)
        {
            if (Game.MenuFrozen) { __result = false; return false; }
            return true;
        }
    }
}


namespace ADOFAIMulti
{
    // 멀티 섬에 있는 동안 뒤에서 돌고 있는 메뉴가 키 입력(ESC로 종료/일시정지 등)을 처리하지 않게
    [HarmonyPatch(typeof(scrController), "Update")]
    static class Patch_FreezeMenuController
    {
        static bool Prefix() => !Game.MenuPaused;
    }

    [HarmonyPatch(typeof(scnCLS), "Update")]
    static class Patch_FreezeCLS
    {
        static bool Prefix() => !Game.MenuPaused;
    }
}
