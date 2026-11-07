using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Steamworks;
using Steamworks.Data;
using UnityEngine.Networking;

namespace ADOFAIMulti
{
    /// <summary>Steam 콜백 등 다른 스레드에서 온 일을 메인 스레드에서 처리하기 위한 큐</summary>
    public static class MainThread
    {
        static readonly Queue<Action> _q = new Queue<Action>();
        public static void Post(Action a) { lock (_q) _q.Enqueue(a); }
        public static void Drain()
        {
            while (true)
            {
                Action a;
                lock (_q) { if (_q.Count == 0) return; a = _q.Dequeue(); }
                try { a(); } catch (Exception ex) { Main.Log("MainThread error: " + ex); }
            }
        }
    }

    /// <summary>
    /// 연결 계층.
    /// - 게임 데이터는 전부 중계 서버(방 코드)로 주고받는다 → Steam 오프라인이어도 됨, 포트포워딩 불필요.
    /// - Steam이 온라인이면 "친구 초대"용으로만 Steam 로비를 하나 만들어 방 코드를 실어 보낸다.
    ///   초대를 수락한 친구는 로비에서 방 코드를 읽어 중계 서버로 들어온다.
    /// </summary>
    public static class Net
    {
        // 중계 서버 주소: 시작할 때 GitHub release/relay.txt 에서 받아옴 (서버를 바꿔도 모드 업데이트 불필요)
        public const string DefaultRelay = "wss://adofai-multi-relay.444happy123.workers.dev";
        public static string RelayUrl = DefaultRelay;
        public static string HttpBase => RelayUrl.Replace("wss://", "https://").Replace("ws://", "http://");
        const string LobbyKey = "adofaimulti";
        const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // 헷갈리는 0/O/1/I 제외

        public static string Status = "";
        public static string RoomCode;                 // 현재 방 코드 (방에 있을 때)
        public static ulong MyId, HostId;
        public static bool InRoom => _relay != null && _welcomed;
        public static bool IsHost => InRoom && MyId == HostId;
        public static readonly Dictionary<ulong, string> Members = new Dictionary<ulong, string>();

        public static bool SteamOnline { get { try { return SteamClient.IsValid && SteamClient.IsLoggedOn; } catch { return false; } } }
        public static string MyName
        {
            get
            {
                if (!string.IsNullOrEmpty(Profile.Nick)) return Profile.Nick;
                return DefaultName;
            }
        }
        public static string DefaultName
        {
            get
            {
                try { if (SteamClient.IsValid && !string.IsNullOrEmpty(SteamClient.Name)) return SteamClient.Name; } catch { }
                return Environment.UserName;
            }
        }

        static RelayClient _relay;
        static bool _welcomed, _hostMode, _steamHooked;
        static Lobby? _inviteLobby;                    // 초대용 Steam 로비 (방장)
        public static bool CanInvite => _inviteLobby.HasValue;

        // ---------------- 시작/종료 ----------------
        public static IEnumerator LoadRelayUrl()
        {
            using (var req = UnityWebRequest.Get(Updater.BaseUrl + "/relay.txt?t=" + DateTime.UtcNow.Ticks))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var u = req.downloadHandler.text.Trim();
                    if (u.StartsWith("ws")) { RelayUrl = u.TrimEnd('/'); Main.Log("Relay: " + RelayUrl); }
                }
            }
        }

        /// <summary>Steam 이벤트 연결 (Steam이 늦게 준비될 수 있어 1초마다 재시도)</summary>
        static float _nextHookTry;
        public static void RetryInit()
        {
            if (_steamHooked || UnityEngine.Time.unscaledTime < _nextHookTry) return;
            _nextHookTry = UnityEngine.Time.unscaledTime + 1f;
            bool valid; try { valid = SteamClient.IsValid; } catch { valid = false; }
            if (!valid) return;
            _steamHooked = true;
            SteamFriends.OnGameLobbyJoinRequested += OnInviteAccepted;
            Main.Log($"Net: Steam hooked (online={SteamOnline})");

            // 게임이 꺼진 상태에서 초대 수락 → "+connect_lobby <id>"
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var id))
                    JoinViaSteamLobby(id);
        }

        public static void Shutdown()
        {
            Leave();
            if (_steamHooked) { try { SteamFriends.OnGameLobbyJoinRequested -= OnInviteAccepted; } catch { } }
            _steamHooked = false;
        }

        // ---------------- 방 만들기 / 참가 / 나가기 ----------------
        static ulong NewId()
        {
            var b = new byte[8]; new System.Random().NextBytes(b);
            ulong v = BitConverter.ToUInt64(b, 0) & 0x7FFFFFFFFFFFFFFF;
            return v == 0 ? 1 : v;
        }

        static string NewCode()
        {
            var r = new System.Random();
            return new string(Enumerable.Range(0, 6).Select(_ => CodeChars[r.Next(CodeChars.Length)]).ToArray());
        }

        public static string NormalizeCode(string s) => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        public static bool IsValidCode(string s) { var c = NormalizeCode(s); return c.Length >= 4 && c.Length <= 8; }

        // 방 설정 (방장)
        public static bool RoomPublic;
        public static string RoomName = "";
        public static string RoomPw = "";            // 방장: 비공개 방 비밀번호
        static string _joinPw = "", _joinInv = "";
        public static string InviteCode;             // 방장이 방 안에서 만든 참가 코드 (없으면 코드로 못 들어옴)
        public static bool InviteBusy;

        /// <summary>방장: 참가 코드 만들기 (이미 있으면 같은 코드)</summary>
        public static void RequestInvite()
        {
            if (!IsHost || _relay == null) return;
            InviteBusy = true;
            _relay.SendText("{\"t\":\"invite\"}");
        }

        /// <summary>참가 코드로 들어가기: 서버에서 방을 찾은 뒤 접속 (비밀번호 없이 입장)</summary>
        public static void JoinByCode(string inv)
        {
            inv = NormalizeCode(inv);
            if (!IsValidCode(inv) || Runner.Instance == null) return;
            Runner.Instance.StartCoroutine(JoinByCodeCo(inv));
        }
        static System.Collections.IEnumerator JoinByCodeCo(string inv)
        {
            Status = "코드 확인 중...";
            using (var req = UnityEngine.Networking.UnityWebRequest.Get(HttpBase + "/invite/" + inv))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success && req.responseCode != 200)
                { Status = req.responseCode == 404 ? "서버 업데이트가 필요해요" : "서버에 연결하지 못했어요"; yield break; }
                JObject j; try { j = JObject.Parse(req.downloadHandler.text); } catch { Status = "서버 응답을 읽지 못했어요"; yield break; }
                if (!((bool?)j["ok"] ?? false)) { Status = (string)j["msg"] ?? "없는 코드예요"; yield break; }
                Session.RequestJoin((string)j["room"], "", inv);
            }
        }
        public const string LobbyPwKey = "adofaimultipw";
        static string _lastMeta;

        public static void CreateRoom(bool isPublic, string roomName, string password = "")
        {
            if (_relay != null) return;
            RoomPublic = isPublic;
            RoomPw = isPublic ? "" : (password ?? "").Trim();
            RoomName = (roomName ?? "").Trim();
            Connect(NewCode(), true);
        }

        /// <summary>방장: 공개 여부 바꾸기 (서버의 공개 방 목록에 바로 반영)</summary>
        public static void SetPublic(bool isPublic, string password = null)
        {
            if (!IsHost) return;
            RoomPublic = isPublic;
            RoomPw = isPublic ? "" : (password ?? RoomPw ?? "").Trim();
            _lastMeta = null;
            _relay?.SendText("{\"t\":\"meta\",\"public\":" + (isPublic ? "true" : "false") + ",\"pw\":" + Js(RoomPw) + "}");
            if (_inviteLobby.HasValue) { try { _inviteLobby.Value.SetData(LobbyPwKey, RoomPw); } catch { } }
        }

        static string Js(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ").Replace("\t", " ") + "\"";

        /// <summary>방장: 방 정보(공개·이름·단계·맵)가 바뀌면 서버에 알림 → 공개 방 목록 갱신</summary>
        public static void PublishMeta(string phase, string song, string artist)
        {
            if (!IsHost || _relay == null) return;
            string meta = "{\"t\":\"meta\",\"public\":" + (RoomPublic ? "true" : "false") + ",\"name\":" + Js(RoomName) +
                          ",\"phase\":" + Js(phase) + ",\"song\":" + Js(song) + ",\"artist\":" + Js(artist) + "}";
            if (meta == _lastMeta) return;
            _lastMeta = meta;
            _relay.SendText(meta);
        }

        public static void Join(string code, string password = "", string invite = "")
        {
            _joinPw = (password ?? "").Trim();
            _joinInv = invite ?? "";
            if (_relay != null) Leave();
            Connect(NormalizeCode(code), false);
        }

        static void Connect(string code, bool host)
        {
            MyId = NewId(); HostId = 0; Members.Clear(); _welcomed = false; _hostMode = host; _lastMeta = null; InviteCode = null; InviteBusy = false;
            RoomCode = code;
            Status = host ? "방 만드는 중..." : $"방 {code} 에 들어가는 중...";
            string url = $"{RelayUrl}/room/{code}?id={MyId}&host={(host ? 1 : 0)}&v={Uri.EscapeDataString(Main.Version)}&name={Uri.EscapeDataString(MyName)}";
            if (host) url += $"&pub={(RoomPublic ? 1 : 0)}&rname={Uri.EscapeDataString(RoomName ?? "")}&pw={Uri.EscapeDataString(RoomPw ?? "")}";
            else
            {
                if (!string.IsNullOrEmpty(_joinPw)) url += "&pw=" + Uri.EscapeDataString(_joinPw);
                if (!string.IsNullOrEmpty(_joinInv)) url += "&inv=" + Uri.EscapeDataString(_joinInv);
            }
            Main.Log($"Relay connect {(host ? "host" : "join")} {code}");
            _relay = new RelayClient();
            _relay.Connect(url);
        }

        public static void Leave()
        {
            if (_inviteLobby.HasValue) { try { _inviteLobby.Value.Leave(); } catch { } _inviteLobby = null; }
            if (_relay != null) { _relay.Close(); _relay = null; }
            _welcomed = false; Members.Clear(); HostId = 0; RoomCode = null;
        }

        // ---------------- Steam 초대 ----------------
        public static void InviteOverlay()
        {
            if (_inviteLobby.HasValue) { try { SteamFriends.OpenGameInviteOverlay(_inviteLobby.Value.Id); } catch { } }
            else Status = SteamOnline ? "초대 준비 중..." : "Steam 오프라인: 방 코드를 친구에게 알려주세요";
        }

        static async void CreateInviteLobby()
        {
            if (!SteamOnline || !IsHost) return;
            try
            {
                Lobby? l = await SteamMatchmaking.CreateLobbyAsync(8);
                MainThread.Post(() =>
                {
                    if (!l.HasValue) return;
                    if (!IsHost) { l.Value.Leave(); return; }     // 그 사이 방을 나갔으면 정리
                    var lobby = l.Value;
                    lobby.SetFriendsOnly(); lobby.SetJoinable(true);
                    lobby.SetData(LobbyKey, RoomCode);
                    lobby.SetData(LobbyPwKey, RoomPw ?? "");
                    _inviteLobby = lobby;
                    Main.Log("Invite lobby ready " + lobby.Id.Value);
                });
            }
            catch (Exception ex) { Main.Log("Invite lobby failed: " + ex.Message); }
        }

        static void OnInviteAccepted(Lobby lobby, SteamId friend) => MainThread.Post(() => JoinViaSteamLobby(lobby.Id.Value));

        static async void JoinViaSteamLobby(ulong lobbyId)
        {
            try
            {
                Lobby? l = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
                MainThread.Post(() =>
                {
                    if (!l.HasValue) { Status = "초대받은 방에 들어가지 못했어요"; return; }
                    var code = l.Value.GetData(LobbyKey);
                    var pw = l.Value.GetData(LobbyPwKey);
                    l.Value.Leave();                               // 초대 로비는 코드 전달용일 뿐
                    if (string.IsNullOrEmpty(code)) { Status = "멀티 모드 방이 아니에요"; return; }
                    Session.RequestJoin(code, pw);
                });
            }
            catch (Exception ex) { Main.Log("JoinViaSteamLobby: " + ex.Message); }
        }

        // ---------------- 송수신 ----------------
        public static void Send(ulong to, byte[] data)
        {
            if (to == MyId) { Session.Handle(MyId, data); return; }   // 방장 자신은 루프백
            _relay?.Send(to, data);
        }

        public static void SendToHost(byte[] data) => Send(HostId, data);

        public static void Broadcast(byte[] data)
        {
            _relay?.Send(0, data);
            Session.Handle(MyId, data);
        }

        static float _nextPing;
        public static void Poll()
        {
            var r = _relay;
            if (r == null) return;
            if (r.Open && UnityEngine.Time.unscaledTime > _nextPing) { _nextPing = UnityEngine.Time.unscaledTime + 25f; r.SendText("ping"); }
            int guard = 0;
            while (guard++ < 512 && r.TryDequeue(out var m))
            {
                if (r != _relay) return;   // 처리 중 방을 나감
                if (m.Closed) { OnClosed(m.Error); return; }
                if (m.IsText) OnControl(m.Text);
                else if (_welcomed && Members.ContainsKey(m.From)) Session.Handle(m.From, m.Data);
            }
        }

        static void OnControl(string text)
        {
            JObject j;
            try { j = JObject.Parse(text); } catch { return; }
            string t = (string)j["t"];
            switch (t)
            {
                case "welcome":
                    Members.Clear();
                    foreach (var mm in (JArray)j["members"])
                    {
                        ulong id = ulong.Parse((string)mm["id"]);
                        Members[id] = (string)mm["name"];
                        if ((bool)mm["host"]) HostId = id;
                    }
                    _welcomed = true;
                    Status = _hostMode ? "방을 만들었어요" : "방에 들어왔어요";
                    Session.OnRoomEntered(_hostMode);
                    if (_hostMode) CreateInviteLobby();
                    break;
                case "joined":
                {
                    ulong id = ulong.Parse((string)j["id"]);
                    Members[id] = (string)j["name"];
                    Session.OnMemberJoined(id, (string)j["name"]);
                    break;
                }
                case "left":
                {
                    ulong id = ulong.Parse((string)j["id"]);
                    Members.Remove(id);
                    Session.OnMemberLeft(id);
                    break;
                }
                case "invite":
                    InviteCode = (string)j["code"]; InviteBusy = false;
                    if (!string.IsNullOrEmpty(InviteCode)) { UnityEngine.GUIUtility.systemCopyBuffer = InviteCode; Session.Notice = "참가 코드 " + InviteCode + " — 복사했어요. 이 코드로 비밀번호 없이 들어와요"; }
                    break;
                case "error":
                    Status = (string)j["msg"] ?? "접속 실패";
                    Main.Log("Relay error: " + (string)j["code"]);
                    if ((string)j["code"] == "bad_pw") UI.AskPassword(RoomCode, Status);
                    if (Session.InRoom) Session.LeaveRoom(); else Leave();
                    break;
            }
        }

        static void OnClosed(string error)
        {
            bool was = _welcomed;
            Main.Log("Relay closed: " + error);
            if (!was) Status = "중계 서버에 연결하지 못했어요" + (string.IsNullOrEmpty(error) ? "" : $" ({error})");
            else if (string.IsNullOrEmpty(Status) || Status.StartsWith("방")) Status = "연결이 끊겼어요";
            if (Session.InRoom) Session.LeaveRoom(); else Leave();
        }
    }
}
