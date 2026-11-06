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
    /// 게임 시작 시 GitHub 최신 릴리스의 version.json을 확인하고,
    /// 새 버전이면 dll/Info.json을 받아 교체해 둔다 (다음 실행부터 적용).
    /// </summary>
    public static class Updater
    {
        // ▼ 저장소만 바꾸면 됨
        public const string Owner = "__OWNER__";
        public const string Repo = "ADOFAIMulti";

        public static string VersionUrl => $"https://github.com/{Owner}/{Repo}/releases/latest/download/version.json";

        public enum State { Idle, Checking, UpToDate, Downloading, ReadyToRestart, Failed }
        public static State Current = State.Idle;
        public static string Latest = "";
        public static string Notes = "";
        public static string Error = "";

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

        public static IEnumerator CheckAndUpdate()
        {
            if (Owner.StartsWith("__")) { Current = State.Idle; yield break; }
            Current = State.Checking;

            string json = null;
            using (var req = UnityWebRequest.Get(VersionUrl + "?t=" + DateTime.UtcNow.Ticks))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("version.json: " + req.error); yield break; }
                json = req.downloadHandler.text;
            }

            Latest = Field(json, "Version");
            Notes = Field(json, "Notes");
            if (!IsNewer(Latest, Main.Version)) { Current = State.UpToDate; yield break; }

            string dllUrl = Field(json, "DllUrl"), infoUrl = Field(json, "InfoUrl"), sha = Field(json, "Sha256").ToLowerInvariant();
            Current = State.Downloading;

            byte[] dll, info;
            using (var req = UnityWebRequest.Get(dllUrl))
            {
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("dll: " + req.error); yield break; }
                dll = req.downloadHandler.data;
            }
            using (var req = UnityWebRequest.Get(infoUrl))
            {
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Fail("Info.json: " + req.error); yield break; }
                info = req.downloadHandler.data;
            }

            using (var h = SHA256.Create())
            {
                var got = BitConverter.ToString(h.ComputeHash(dll)).Replace("-", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(sha) && got != sha) { Fail("파일 검증 실패 (해시 불일치)"); yield break; }
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
            catch (Exception ex) { Fail("파일 교체 실패: " + ex.Message); }
        }

        static void Fail(string msg) { Current = State.Failed; Error = msg; Main.Log("Update failed: " + msg); }

        static string Field(string json, string key)
        {
            var m = Regex.Match(json ?? "", "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : "";
        }
    }
}
