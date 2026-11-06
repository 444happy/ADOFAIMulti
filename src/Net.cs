using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

namespace ADOFAIMulti
{
    /// <summary>Steam 콜백이 어느 스레드에서 오든 메인 스레드에서 처리되도록 모아두는 큐</summary>
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

    /// <summary>Steam 로비 + P2P (릴레이 경유, 포트포워딩 불필요)</summary>
    public static class Net
    {
        const int Channel = 7;  // 다른 모드/게임과 겹치지 않게
        const string LobbyKey = "adofaimulti";

        public static Lobby? CurrentLobby;
        public static bool IsHost => CurrentLobby.HasValue && HostId == MyId;
        public static ulong MyId => SteamClient.IsValid ? SteamClient.SteamId.Value : 0;
        public static string MyName => SteamClient.IsValid ? SteamClient.Name : "Me";
        /// <summary>입장 시점의 방장 (Steam은 방장이 나가면 자동 위임하므로 고정해 둠)</summary>
        public static ulong HostId;
        public static string Status = "";

        static byte[] _buf = new byte[1024 * 1024 + 1024];
        static bool _inited;

        public static void Init()
        {
            if (_inited) return;
            if (!SteamClient.IsValid) { Status = "Steam이 초기화되지 않았습니다 (Steam으로 실행해 주세요)"; return; }
            _inited = true;
            SteamNetworking.AllowP2PPacketRelay(true);
            SteamNetworking.OnP2PSessionRequest += OnSessionRequest;
            SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnMemberLeft;
            SteamMatchmaking.OnLobbyMemberDisconnected += OnMemberLeft;

            // 게임이 꺼진 상태에서 초대 수락 → "+connect_lobby <id>" 로 실행됨
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var id))
                    Session.RequestJoin(id);
        }

        public static void Shutdown()
        {
            if (!_inited) return;
            _inited = false;
            SteamNetworking.OnP2PSessionRequest -= OnSessionRequest;
            SteamFriends.OnGameLobbyJoinRequested -= OnJoinRequested;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnMemberLeft;
            SteamMatchmaking.OnLobbyMemberDisconnected -= OnMemberLeft;
            Leave();
        }

        // ---------- Lobby ----------
        public static async void CreateRoom(int maxMembers)
        {
            if (!_inited) Init();
            if (!_inited) return;
            Status = "방 만드는 중...";
            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            MainThread.Post(() =>
            {
                if (!lobby.HasValue) { Status = "방 만들기 실패"; return; }
                var l = lobby.Value;
                l.SetFriendsOnly();
                l.SetJoinable(true);
                l.SetData(LobbyKey, Main.Version);
                CurrentLobby = l;
                HostId = MyId;
                Status = "방 생성 완료";
                Session.OnRoomEntered(true);
            });
        }

        public static async void Join(ulong lobbyId)
        {
            if (!_inited) Init();
            if (!_inited) return;
            if (CurrentLobby.HasValue) Leave();
            Status = "참가하는 중...";
            Lobby? lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            MainThread.Post(() => { if (!lobby.HasValue) Status = "참가 실패"; });
        }

        public static void Leave()
        {
            if (CurrentLobby.HasValue)
            {
                foreach (var m in CurrentLobby.Value.Members)
                    if (m.Id.Value != MyId) SteamNetworking.CloseP2PSessionWithUser(m.Id);
                CurrentLobby.Value.Leave();
            }
            CurrentLobby = null;
            HostId = 0;
        }

        public static void InviteOverlay()
        {
            if (CurrentLobby.HasValue) SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
        }

        public static IEnumerable<Friend> Members => CurrentLobby.HasValue ? CurrentLobby.Value.Members : Enumerable.Empty<Friend>();

        static bool IsMember(ulong id) => Members.Any(m => m.Id.Value == id);

        static void OnSessionRequest(SteamId id) => MainThread.Post(() =>
        {
            if (IsMember(id.Value)) SteamNetworking.AcceptP2PSessionWithUser(id);
        });

        static void OnJoinRequested(Lobby lobby, SteamId friend) => MainThread.Post(() => Session.RequestJoin(lobby.Id.Value));

        static void OnLobbyEntered(Lobby lobby) => MainThread.Post(() =>
        {
            if (lobby.IsOwnedBy(SteamClient.SteamId)) return; // 방장은 CreateRoom에서 처리
            var ver = lobby.GetData(LobbyKey);
            if (string.IsNullOrEmpty(ver)) { Status = "멀티 모드 방이 아닙니다"; lobby.Leave(); return; }
            CurrentLobby = lobby;
            HostId = lobby.Owner.Id.Value;
            Status = "참가 완료";
            Session.OnRoomEntered(false);
        });

        static void OnMemberJoined(Lobby lobby, Friend f) => MainThread.Post(() => Session.OnMemberJoined(f.Id.Value, f.Name));

        static void OnMemberLeft(Lobby lobby, Friend f) => MainThread.Post(() =>
        {
            SteamNetworking.CloseP2PSessionWithUser(f.Id);
            Session.OnMemberLeft(f.Id.Value);
        });

        // ---------- P2P ----------
        public static void Send(ulong to, byte[] data)
        {
            if (to == MyId) { Session.Handle(MyId, data); return; } // 방장 자신은 루프백
            SteamNetworking.SendP2PPacket(to, data, data.Length, Channel, P2PSend.Reliable);
        }

        public static void SendToHost(byte[] data) => Send(HostId, data);

        public static void Broadcast(byte[] data)
        {
            foreach (var m in Members.ToList()) Send(m.Id.Value, data);
        }

        public static void Poll()
        {
            if (!_inited || !CurrentLobby.HasValue) return;
            uint size = 0;
            int guard = 0;
            while (SteamNetworking.IsP2PPacketAvailable(out size, Channel) && guard++ < 256)
            {
                if (size > _buf.Length) _buf = new byte[size];
                SteamId from = default;
                if (!SteamNetworking.ReadP2PPacket(_buf, ref size, ref from, Channel)) break;
                if (!IsMember(from.Value)) continue;
                var data = new byte[size];
                Buffer.BlockCopy(_buf, 0, data, 0, (int)size);
                Session.Handle(from.Value, data);
            }
        }
    }
}
