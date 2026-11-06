using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAIMulti
{
    /// <summary>
    /// GitHub 저장소 release/version.json을 확인하고, 새 버전이면 dll/Info.json을 받아 교체해 둔다.
    /// 확인 시점: 게임 시작 직후 + 이후 10분마다(경기 중 제외) + 설정의 "지금 확인".
    /// 실행 중인 코드는 바꿀 수 없으므로 적용은 게임 재시작 시.
    /// </summary>
    public static class Updater
    {
        // ▼ 저장소만 바꾸면 됨
        public const string Owner = "__OWNER__";
        public const string Repo = "ADOFAIMulti";

        public static string BaseUrl => $"https://raw.githubusercontent.com/{Owner}/{Repo}/main/release";
        public static string VersionUrl => BaseUrl + "/version.json";

        public enum State { Idle, Checking, UpToDate, Downloading, ReadyToRestart, Failed }
        public static State Current = State.Idle;
        public static string Latest = "";
        public static string Notes = "";
        public static string Error = "";
        public static float LastCheck = -999;
        static bool _running;

        public static string StatusText
        {
            get
            {
                switch (Current)
                {
                    case State.Checking: return "업데이트 확인 중...";
                    case State.UpToDate: return "최신 버전";
                    case State.Downloading: return $"v{Latest} 받는 중...";
                    case State.ReadyToRestart: return $"<color=#7CFC00>v{Latest} 설치 완료 — 게임을 재시작하면 적용돼요</color>";
                    case State.Failed: return "<color=#ff8a7a>업데이트 확인 실패</color>";
                }
                return "";
            }
        }

        public static bool IsNewer(string a, string b)
        {
            Version va, vb;
            return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
        }

        /// <summary>지난 업데이트 때 이름 바꿔둔 옛 dll 정리</summary>
        public static void CleanupOld()
        {
            try
            {
                foreach (var f in Directory.GetFiles(Main.Mod.Path, "*.old")) { try { File.Delete(f); } catch { } }
            }
            catch { }
        }

        /// <summary>Runner.Update에서 매 프레임 호출: 10분마다, 경기 중이 아닐 때 다시 확인</summary>
        public static void Tick(MonoBehaviour runner)
        {
            if (_running || Current == State.ReadyToRestart || Session.InMatch) return;
            if (Time.unscaledTime - LastCheck < 600f) return;
            runner.StartCoroutine(CheckAndUpdate());
        }

        /// <summary>설정의 "지금 확인"</summary>
        public static void CheckNow(MonoBehaviour runner)
        {
            if (_running || Current == State.ReadyToRestart) return;
            runner.StartCoroutine(CheckAndUpdate());
        }

        public static IEnumerator CheckAndUpdate()
        {
            if (Owner.StartsWith("__") || _running || Current == State.ReadyToRestart) yield break;
            _running = true;
            LastCheck = Time.unscaledTime;
            Current = State.Checking;

            string json = null;
            using (var req = UnityWebRequest.Get(VersionUrl + "?t=" + DateTime.UtcNow.Ticks))
            {
                req.timeout = 15;
                req.SetRequestHeader("Cache-Control", "no-cache");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("version.json: " + req.error); yield break; }
                json = req.downloadHandler.text;
            }

            Latest = Field(json, "Version");
            Notes = Field(json, "Notes");
            if (!IsNewer(Latest, Main.Version))
            {
                Current = State.UpToDate;
                Main.Log($"Update check: up to date (local {Main.Version}, remote {Latest})");
                _running = false;
                yield break;
            }

            string dllUrl = Field(json, "DllUrl"), infoUrl = Field(json, "InfoUrl"), sha = Field(json, "Sha256").ToLowerInvariant();
            Current = State.Downloading;
            Main.Log($"Update check: downloading {Latest} (local {Main.Version})");
            string bust = "?t=" + DateTime.UtcNow.Ticks;   // CDN 캐시 때문에 옛 dll을 받지 않게

            byte[] dll, info;
            using (var req = UnityWebRequest.Get(dllUrl + bust))
            {
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("dll: " + req.error); yield break; }
                dll = req.downloadHandler.data;
            }
            using (var req = UnityWebRequest.Get(infoUrl + bust))
            {
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("Info.json: " + req.error); yield break; }
                info = req.downloadHandler.data;
            }

            using (var h = SHA256.Create())
            {
                var got = BitConverter.ToString(h.ComputeHash(dll)).Replace("-", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(sha) && got != sha) { Fail("파일 검증 실패 (GitHub 캐시가 아직 갱신 전일 수 있어요. 잠시 뒤 다시 확인)"); yield break; }
            }

            try
            {
                string dir = Main.Mod.Path;
                string cur = Path.Combine(dir, "ADOFAIMulti.dll");
                string old = cur + "." + Main.Version + ".old";
                if (File.Exists(old)) File.Delete(old);
                File.Move(cur, old);              // 사용 중인 dll도 이름 변경은 가능
                File.WriteAllBytes(cur, dll);
                File.WriteAllBytes(Path.Combine(dir, "Info.json"), info);
                Current = State.ReadyToRestart;
                Main.Log($"Updated to {Latest}. Restart to apply.");
            }
            catch (Exception ex) { Fail("파일 교체 실패: " + ex.Message); yield break; }
            _running = false;
        }

        static void Fail(string msg)
        {
            Current = State.Failed; Error = msg; _running = false;
            LastCheck = Time.unscaledTime - 480f;   // 실패하면 2분 뒤 다시 시도
            Main.Log("Update failed: " + msg);
        }

        static string Field(string json, string key)
        {
            var m = Regex.Match(json ?? "", "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : "";
        }
    }
}
