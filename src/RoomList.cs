using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAIMulti
{
    /// <summary>중계 서버의 공개 방 목록 (GET /rooms). 멀티 섬 첫 화면이 보이는 동안 5초마다 갱신</summary>
    public static class RoomList
    {
        public class Room
        {
            public string Code, Name, Host, Phase, Song, Artist, Ver;
            public int Count, Max;
            public bool Full => Count >= Max;
            public bool InGame => Phase == "playing" || Phase == "results";
            public bool SameVersion => Ver == Main.Version;
        }

        public static readonly List<Room> Rooms = new List<Room>();
        public static bool Loading, Loaded;
        public static string Error = "";
        static float _nextAt;

        /// <summary>UI에서 매 프레임 호출 (목록이 보일 때만)</summary>
        public static void Tick(MonoBehaviour runner)
        {
            if (Loading || Time.unscaledTime < _nextAt || runner == null) return;
            _nextAt = Time.unscaledTime + 5f;
            runner.StartCoroutine(Fetch());
        }

        public static void RefreshNow() { _nextAt = 0; }

        static IEnumerator Fetch()
        {
            Loading = true;
            using (var req = UnityWebRequest.Get(Net.HttpBase + "/rooms"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Loading = false;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Error = req.responseCode == 404 ? "서버 업데이트가 필요해요 (방 목록 미지원)" : "방 목록을 불러오지 못했어요";
                    yield break;
                }
                try
                {
                    var j = JObject.Parse(req.downloadHandler.text);
                    Rooms.Clear();
                    foreach (var x in (JArray)j["rooms"])
                        Rooms.Add(new Room
                        {
                            Code = (string)x["code"] ?? "", Name = (string)x["name"] ?? "", Host = (string)x["host"] ?? "",
                            Phase = (string)x["phase"] ?? "lobby", Song = (string)x["song"] ?? "", Artist = (string)x["artist"] ?? "",
                            Ver = (string)x["ver"] ?? "", Count = (int?)x["count"] ?? 0, Max = (int?)x["max"] ?? 8,
                        });
                    // 들어갈 수 있는 방 먼저: 같은 버전 → 대기 중 → 자리 있음
                    Rooms.Sort((a, b) =>
                    {
                        int sa = (a.SameVersion ? 0 : 4) + (a.InGame ? 2 : 0) + (a.Full ? 1 : 0);
                        int sb = (b.SameVersion ? 0 : 4) + (b.InGame ? 2 : 0) + (b.Full ? 1 : 0);
                        return sa.CompareTo(sb);
                    });
                    Error = ""; Loaded = true;
                }
                catch (Exception ex) { Error = "방 목록 형식 오류"; Main.Log("RoomList: " + ex.Message); }
            }
        }
    }
}
