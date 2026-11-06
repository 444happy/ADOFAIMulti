using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        const int PageSize = 30;

        public class Level
        {
            public int Id;
            public string Song = "", Artist = "", Creator = "", Diff = "", DlLink = "", FileId = "";
            public Color DiffColor = Color.gray;
            public float Bpm;
            public double LengthMs;
            public bool CanDownload => !string.IsNullOrEmpty(DlLink) && DlLink.Contains("tuforums.com/cdn/");
            /// <summary>캐시 폴더 이름 (같은 파일이면 같은 값)</summary>
            public string Hash => "tuf" + Id + "_" + (FileId ?? "").Replace("-", "").Substring(0, Math.Min(12, (FileId ?? "").Replace("-", "").Length));
            public string LengthText => LengthMs > 0 ? $"{(int)(LengthMs / 60000)}:{(int)(LengthMs / 1000) % 60:00}" : "";
        }

        public static readonly List<Level> Results = new List<Level>();
        public static int Total;
        public static bool Loading, HasMore;
        public static string Error = "", LastQuery = null;
        static int _searchSeq;

        public static void Search(MonoBehaviour runner, string query, bool more = false)
        {
            if (!more) { Results.Clear(); Total = 0; HasMore = false; }
            LastQuery = query;
            int seq = ++_searchSeq;
            runner.StartCoroutine(SearchCo(query, more ? Results.Count : 0, seq));
        }

        static IEnumerator SearchCo(string query, int offset, int seq)
        {
            Loading = true; Error = "";
            string url = $"{Api}?limit={PageSize}&offset={offset}" + (string.IsNullOrWhiteSpace(query) ? "" : "&query=" + Uri.EscapeDataString(query.Trim()));
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (seq != _searchSeq) yield break;          // 그 사이 새 검색이 시작됨
                Loading = false;
                if (req.result != UnityWebRequest.Result.Success) { Error = "TUF에 연결하지 못했어요 (" + req.error + ")"; yield break; }
                try
                {
                    var j = JObject.Parse(req.downloadHandler.text);
                    Total = (int?)j["total"] ?? 0;
                    HasMore = (bool?)j["hasMore"] ?? false;
                    foreach (var x in (JArray)j["results"]) Results.Add(Parse(x));
                }
                catch (Exception ex) { Error = "TUF 응답을 읽지 못했어요"; Main.Log("TUF parse: " + ex.Message); }
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
