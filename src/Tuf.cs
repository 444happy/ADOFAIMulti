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
    /// The Universal Forums(tuforums.com) 레벨 검색.
    /// GET https://api.tuforums.com/v2/database/levels?limit=&amp;offset=&amp;query=
    /// 다운로드 링크(dlLink)는 cdn.tuforums.com 의 zip 으로 리다이렉트되고 Range(이어받기)를 지원한다.
    /// </summary>
    public static class Tuf
    {
        public const string Api = "https://api.tuforums.com/v2/database/levels";
        public const string DiffApi = "https://api.tuforums.com/v2/database/difficulties";
        const int PageSize = 30;

        public class Level
        {
            public int Id;
            public string Song = "", Artist = "", Creator = "", Diff = "", DlLink = "", FileId = "";
            public Color DiffColor = Color.gray;
            public float Bpm;
            public double LengthMs;
            public int Clears, Likes, Tiles;
            public bool CanDownload => !string.IsNullOrEmpty(DlLink) && DlLink.Contains("tuforums.com/cdn/");
            /// <summary>캐시 폴더 이름 (같은 파일이면 같은 값)</summary>
            public string Hash => "tuf" + Id + "_" + (FileId ?? "").Replace("-", "").Substring(0, Math.Min(12, (FileId ?? "").Replace("-", "").Length));
            public string LengthText => LengthMs > 0 ? (int)(LengthMs / 60000) + ":" + ((int)(LengthMs / 1000) % 60).ToString("00") : "";
        }

        // ---------------- 난이도 목록 (P1~U20 + 특수) ----------------
        public class DiffInfo { public int Id, Order; public string Name = "", Type = "", IconUrl = ""; public Color Color = Color.gray; }
        public static readonly List<DiffInfo> Pgu = new List<DiffInfo>();          // P1..U20 (정렬 순서)
        public static readonly List<string> SpUnranked = new List<string>(), SpQ = new List<string>(), SpEtc = new List<string>();
        static readonly Dictionary<string, DiffInfo> _byName = new Dictionary<string, DiffInfo>();
        public static bool DiffsLoaded;
        static bool _diffsLoading;

        public static DiffInfo FindDiff(string name) => name != null && _byName.TryGetValue(name, out var d) ? d : null;

        public static IEnumerator LoadDiffs()
        {
            if (DiffsLoaded || _diffsLoading) yield break;
            _diffsLoading = true;
            using (var req = UnityWebRequest.Get(DiffApi))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                _diffsLoading = false;
                if (req.result != UnityWebRequest.Result.Success) { Main.Log("TUF diffs: " + req.error); yield break; }
                try
                {
                    var arr = JArray.Parse(req.downloadHandler.text);
                    var list = new List<DiffInfo>();
                    foreach (var x in arr)
                    {
                        var d = new DiffInfo { Id = (int?)x["id"] ?? 0, Order = (int?)x["sortOrder"] ?? 0, Name = (string)x["name"] ?? "", Type = (string)x["type"] ?? "", IconUrl = (string)x["icon"] ?? "" };
                        if (ColorUtility.TryParseHtmlString((string)x["color"] ?? "", out var c)) d.Color = c;
                        list.Add(d); _byName[d.Name] = d;
                    }
                    Pgu.Clear(); Pgu.AddRange(list.Where(d => d.Type == "PGU").OrderBy(d => d.Order));
                    SpUnranked.Clear(); SpQ.Clear(); SpEtc.Clear();
                    foreach (var d in list.Where(d => d.Type == "SPECIAL"))
                    {
                        if (d.Name == "Unranked") SpUnranked.Add(d.Name);
                        else if (d.Name == "Qq" || d.Name.StartsWith("GQ") || d.Name.StartsWith("UQ")) SpQ.Add(d.Name);
                        else SpEtc.Add(d.Name);
                    }
                    if (Pgu.Count > 0) { MinIdx = 0; MaxIdx = Pgu.Count - 1; }
                    DiffsLoaded = true;
                }
                catch (Exception ex) { Main.Log("TUF diffs parse: " + ex.Message); }
            }
        }

        // 난이도 아이콘 (처음 보일 때 받아서 보관)
        static readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> _iconBusy = new HashSet<string>();
        public static Texture2D Icon(string name)
        {
            var d = FindDiff(name);
            if (d == null || string.IsNullOrEmpty(d.IconUrl)) return null;
            if (_icons.TryGetValue(d.IconUrl, out var t)) return t;
            if (_iconBusy.Count < 6 && _iconBusy.Add(d.IconUrl) && Runner.Instance != null) Runner.Instance.StartCoroutine(IconCo(d.IconUrl));
            return null;
        }
        static IEnumerator IconCo(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                Texture2D tex = null;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                    if (!tex.LoadImage(req.downloadHandler.data)) { UnityEngine.Object.Destroy(tex); tex = null; }
                }
                _icons[url] = tex;   // 실패해도 null 로 기록 → 다시 시도 안 함 (글자 표시)
                _iconBusy.Remove(url);
            }
        }

        // ---------------- 필터 · 정렬 ----------------
        public static int MinIdx = 0, MaxIdx = 59;
        public static bool IncUnranked = true, IncQ = true, IncEtc = true;
        public static string Sort = "CLEARS";
        public static bool Asc;
        public static readonly string[] SortKeys = { "RECENT", "DIFF", "CLEARS", "TOTAL_CLEARS", "LIKES", "BASESCORE", "BPM", "TILES", "TIME", "RANDOM" };
        public static readonly string[] SortNames = { "ID", "난이도", "클리어 수", "전체 클리어", "좋아요 수", "기반 점수", "BPM", "타일 수", "길이", "무작위" };
        public static string SortName => SortNames[Math.Max(0, Array.IndexOf(SortKeys, Sort))];
        static long _seed;

        public static string MinName => Pgu.Count > 0 ? Pgu[Mathf.Clamp(MinIdx, 0, Pgu.Count - 1)].Name : "P1";
        public static string MaxName => Pgu.Count > 0 ? Pgu[Mathf.Clamp(MaxIdx, 0, Pgu.Count - 1)].Name : "U20";

        static string FilterQuery()
        {
            var sp = new List<string>();
            if (IncUnranked) sp.AddRange(SpUnranked);
            if (IncQ) sp.AddRange(SpQ);
            if (IncEtc) sp.AddRange(SpEtc);
            string q = "&sort=" + Sort + (Asc ? "_ASC" : "_DESC") + "&deletedFilter=hide&clearedFilter=show&availableDlFilter=only"
                     + "&pguRange=" + Uri.EscapeDataString(MinName + "," + MaxName);
            if (sp.Count > 0) q += "&specialDifficulties=" + Uri.EscapeDataString(string.Join(",", sp));
            if (Sort == "RANDOM" && _seed != 0) q += "&seed=" + _seed;
            return q;
        }

        // ---------------- 검색 ----------------
        public static readonly List<Level> Results = new List<Level>();
        public static int Total;
        public static bool Loading, HasMore;
        public static string Error = "", LastQuery = null;
        static int _searchSeq;

        public static void Search(MonoBehaviour runner, string query, bool more = false)
        {
            if (!more) { Results.Clear(); Total = 0; HasMore = false; _seed = 0; }
            LastQuery = query;
            int seq = ++_searchSeq;
            runner.StartCoroutine(SearchCo(query, more ? Results.Count : 0, seq));
        }

        static IEnumerator SearchCo(string query, int offset, int seq)
        {
            Loading = true; Error = "";
            if (!DiffsLoaded) yield return LoadDiffs();
            if (seq != _searchSeq) yield break;
            string url = Api + "?limit=" + PageSize + "&offset=" + offset + "&query=" + Uri.EscapeDataString((query ?? "").Trim()) + FilterQuery();
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (seq != _searchSeq) yield break;          // 그 사이 새 검색이 시작됨
                Loading = false;
                if (req.result != UnityWebRequest.Result.Success) { Error = "TUF 연결 실패 (" + req.error + ")"; yield break; }
                try
                {
                    var j = JObject.Parse(req.downloadHandler.text);
                    Total = (int?)j["total"] ?? 0;
                    HasMore = (bool?)j["hasMore"] ?? false;
                    if (j["seed"] != null && j["seed"].Type != JTokenType.Null) _seed = (long)j["seed"];
                    var seen = new HashSet<int>(Results.Select(r => r.Id));
                    foreach (var x in (JArray)j["results"]) { var l = Parse(x); if (seen.Add(l.Id)) Results.Add(l); }
                }
                catch (Exception ex) { Error = "TUF 응답 오류"; Main.Log("TUF parse: " + ex.Message); }
            }
        }

        static Level Parse(JToken x)
        {
            var l = new Level
            {
                Id = (int?)x["id"] ?? 0,
                Song = (string)x["song"] ?? "",
                Artist = (string)x["artist"] ?? "",
                Creator = (string)x["creator"] ?? "",
                DlLink = (string)x["dlLink"] ?? "",
                FileId = (string)x["fileId"] ?? "",
                Bpm = (float?)x["bpm"] ?? 0,
                LengthMs = (double?)x["levelLengthInMs"] ?? 0,
                Clears = (int?)x["uniqueClears"] ?? (int?)x["clears"] ?? 0,
                Likes = (int?)x["likes"] ?? 0,
                Tiles = (int?)x["tilecount"] ?? 0,
            };
            var d = x["difficulty"];
            if (d != null && d.Type == JTokenType.Object)
            {
                l.Diff = (string)d["name"] ?? "";
                if (ColorUtility.TryParseHtmlString((string)d["color"] ?? "", out var c)) l.DiffColor = c;
            }
            return l;
        }
    }

    /// <summary>
    /// 이어받기 가능한 파일 다운로드 (일시정지 = 요청 중단 후 받은 만큼 보관, 재개 = Range 요청으로 이어서).
    /// 서버가 Range 를 무시하고 처음부터 보내면(200) 파일을 지우고 다시 받는다.
    /// </summary>
    public class FileDownload
    {
        public readonly string Url, PathOut;
        public long Received, Total = -1;
        public volatile bool Paused, Cancelled;
        public bool Done, Failed, Running;
        public string Error = "";

        public FileDownload(string url, string path) { Url = url; PathOut = path; }

        public float Progress => Total > 0 ? Mathf.Clamp01((float)Received / Total) : 0f;

        public IEnumerator Run(Action<FileDownload> onEnd)
        {
            Running = true;
            Directory.CreateDirectory(Path.GetDirectoryName(PathOut));
            Received = File.Exists(PathOut) ? new FileInfo(PathOut).Length : 0;
            int attempts = 0;
            while (!Cancelled)
            {
                while (Paused && !Cancelled) yield return null;
                if (Cancelled) break;

                long baseLen = Received;
                bool restart = false;
                var dh = new DownloadHandlerFile(PathOut, baseLen > 0) { removeFileOnAbort = false };
                using (var req = new UnityWebRequest(Url, "GET", dh, null))
                {
                    req.redirectLimit = 8;
                    if (baseLen > 0) req.SetRequestHeader("Range", $"bytes={baseLen}-");
                    var op = req.SendWebRequest();
                    bool checkedCode = false;
                    while (!op.isDone)
                    {
                        if (!checkedCode && req.responseCode > 0)
                        {
                            checkedCode = true;
                            if (baseLen > 0 && req.responseCode == 200) { req.Abort(); restart = true; break; }   // Range 무시됨
                            Total = ParseTotal(req, baseLen);
                        }
                        Received = baseLen + (long)req.downloadedBytes;
                        if (Paused || Cancelled) { req.Abort(); break; }
                        yield return null;
                    }
                    if (restart) { TryDelete(); Received = 0; continue; }
                    if (Paused || Cancelled) { Received = File.Exists(PathOut) ? new FileInfo(PathOut).Length : Received; continue; }

                    if (req.responseCode == 416) { Done = true; break; }   // 이미 다 받음
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (++attempts < 3) { Received = File.Exists(PathOut) ? new FileInfo(PathOut).Length : 0; yield return new WaitForSecondsRealtime(1f); continue; }
                        Failed = true; Error = req.error ?? "download failed"; break;
                    }
                    if (Total <= 0) Total = ParseTotal(req, baseLen);
                    Received = File.Exists(PathOut) ? new FileInfo(PathOut).Length : Received;
                    Done = true; break;
                }
            }
            if (Cancelled) TryDelete();
            Running = false;
            onEnd?.Invoke(this);
        }

        static long ParseTotal(UnityWebRequest req, long baseLen)
        {
            var cr = req.GetResponseHeader("Content-Range");          // bytes 100-199/1234
            if (!string.IsNullOrEmpty(cr))
            {
                int slash = cr.LastIndexOf('/');
                if (slash >= 0 && long.TryParse(cr.Substring(slash + 1), out var t)) return t;
            }
            var cl = req.GetResponseHeader("Content-Length");
            if (long.TryParse(cl, out var len)) return baseLen + len;
            return -1;
        }

        void TryDelete() { try { if (File.Exists(PathOut)) File.Delete(PathOut); } catch { } }
    }
}
