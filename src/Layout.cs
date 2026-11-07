using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAIMulti
{
    /// <summary>
    /// UI 설계판(웹)에서 만든 화면 배치. 1920×1080 기준 좌표.
    /// 파일: Mods/ADOFAIMulti/layout.json  ·  최신본: release/layout.json (시작할 때와 5분마다 받아서 바로 적용)
    /// 개발: Mods/ADOFAIMulti/layout.dev.json 이 있으면 그것만 쓰고 원격을 받지 않음 (Debug 빌드가 design/layout.json 을 복사)
    /// </summary>
    public static class Layout
    {
        public class El
        {
            public string Id = "", Type = "panel", Name = "", Text = "", Font = "game", Func = "", FuncText = "", Align = "left";
            public float X, Y, W, H, Size = 18, Radius, Opacity = 1;
            public int Weight = 500;
            public Color Color = Color.white, Fill = Color.clear, Stroke = Color.clear;
        }
        public class Screen { public string Id = "", Name = ""; public List<El> Els = new List<El>(); }

        public static readonly Dictionary<string, Screen> Screens = new Dictionary<string, Screen>();
        public static int Version;               // 바뀔 때마다 +1 (UI 캐시 무효화용)
        public static bool Enabled = true;       // 설정에서 끌 수 있음 (끄면 예전 UI)
        static string _lastText;

        static string FilePath => Path.Combine(Main.Mod.Path, "layout.json");
        static string DevPath => Path.Combine(Main.Mod.Path, "layout.dev.json");
        static bool Dev => File.Exists(DevPath);
        static DateTime _devStamp;
        const string Url = "https://raw.githubusercontent.com/444happy/ADOFAIMulti/main/release/layout.json";

        public static Screen Get(string id) => Enabled && Screens.TryGetValue(id, out var s) && s.Els.Count > 0 ? s : null;

        public static void LoadLocal()
        {
            try
            {
                if (Dev) { _devStamp = File.GetLastWriteTimeUtc(DevPath); Apply(File.ReadAllText(DevPath)); Main.Log("layout: 개발용 layout.dev.json 사용"); }
                else if (File.Exists(FilePath)) Apply(File.ReadAllText(FilePath));
            }
            catch (Exception ex) { Main.Log("layout load: " + ex.Message); }
        }

        public static IEnumerator FetchLoop()
        {
            while (true)
            {
                // 개발용 배치: 원격 대신 파일이 바뀌면 바로 다시 읽음
                if (Dev)
                {
                    try
                    {
                        var t = File.GetLastWriteTimeUtc(DevPath);
                        if (t != _devStamp) { _devStamp = t; if (Apply(File.ReadAllText(DevPath))) Main.Log("layout.dev.json 다시 읽음 v" + Version); }
                    }
                    catch { }
                    yield return new WaitForSecondsRealtime(1);
                    continue;
                }
                using (var req = UnityWebRequest.Get(Url + "?t=" + DateTime.UtcNow.Ticks))
                {
                    req.timeout = 15;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var text = req.downloadHandler.text;
                        if (text != _lastText && Apply(text))
                        {
                            try { File.WriteAllText(FilePath, text); } catch { }
                            Main.Log("layout updated v" + Version);
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(300);
            }
        }

        static bool Apply(string text)
        {
            try
            {
                var root = JToken.Parse(text);
                var arr = root is JArray a ? a : (JArray)root["screens"];
                var next = new Dictionary<string, Screen>();
                foreach (var sj in arr)
                {
                    var s = new Screen { Id = (string)sj["id"] ?? "", Name = (string)sj["name"] ?? "" };
                    foreach (var ej in (JArray)sj["elements"] ?? new JArray())
                    {
                        var e = new El
                        {
                            Id = (string)ej["id"] ?? "", Type = (string)ej["type"] ?? "panel", Name = (string)ej["name"] ?? "",
                            Text = (string)ej["text"] ?? "", Font = (string)ej["font"] ?? "game", Func = (string)ej["func"] ?? "",
                            FuncText = (string)ej["funcText"] ?? "", Align = (string)ej["align"] ?? "left",
                            X = (float?)ej["x"] ?? 0, Y = (float?)ej["y"] ?? 0, W = (float?)ej["w"] ?? 0, H = (float?)ej["h"] ?? 0,
                            Size = (float?)ej["size"] ?? 18, Radius = (float?)ej["radius"] ?? 0, Opacity = (float?)ej["opacity"] ?? 1,
                            Weight = (int?)ej["weight"] ?? 500,
                            Color = Col((string)ej["color"]), Fill = Col((string)ej["fill"]), Stroke = Col((string)ej["stroke"]),
                        };
                        if (e.Func == "(없음)") e.Func = "";
                        s.Els.Add(e);
                    }
                    next[s.Id] = s;
                }
                Screens.Clear();
                foreach (var kv in next) Screens[kv.Key] = kv.Value;
                _lastText = text;
                Version++;
                return true;
            }
            catch (Exception ex) { Main.Log("layout parse: " + ex.Message); return false; }
        }

        /// <summary>#rgb, #rrggbb, #rrggbbaa, transparent</summary>
        public static Color Col(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "transparent") return Color.clear;
            if (ColorUtility.TryParseHtmlString(s.Trim(), out var c)) return c;
            return Color.clear;
        }
    }
}
