using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    public enum Phase { None, Lobby, Packing, Distributing, Loading, WaitingStart, Countdown, Playing, Results }

    /// <summary>방 진행 로직. 방장은 권한을 가지고, 멤버는 방장 메시지를 따른다.</summary>
    public static class Session
    {
        const int ChunkSize = 256 * 1024;
        const int CountdownMs = 3000;

        public static Phase Phase = Phase.None;

        /// <summary>레벨 로딩~결과 대기까지 (ESC/재시작 막는 구간)</summary>
        public static bool InMatch => Phase == Phase.Loading || Phase == Phase.WaitingStart || Phase == Phase.Countdown || Phase == Phase.Playing || Phase == Phase.Results;

        static string _pendingJoin;
        /// <summary>초대 수락/코드 입력. 레벨 플레이 중이면 메뉴로 나왔을 때 입장</summary>
        public static void RequestJoin(string code)
        {
            if (Game.IsScnGame) { _pendingJoin = code; Notice = "레벨을 나가면 초대받은 방에 들어갑니다"; return; }
            if (Updater.UpdateRequired) { Island.Enter(); Notice = "새 버전으로 업데이트한 뒤 들어갈 수 있어요"; return; }
            if (InRoom) LeaveRoom();
            Island.Enter();
            Net.Join(code);
        }

        /// <summary>Ctrl+Shift+Q: 기권하고 로비로</summary>
        public static void Forfeit()
        {
            if (!(Phase == Phase.Countdown || Phase == Phase.Playing || Phase == Phase.WaitingStart || Phase == Phase.Loading)) return;
            var st = Game.InGame ? Game.ReadStats() : (LastStats ?? new Stats());
            st.Finished = true;
            LastStats = st;
            Net.SendToHost(Packet.Build(Msg.Progress, w => st.Write(w)));
            Phase = Phase.Results;
            Notice = "기권했습니다. 다른 플레이어를 기다리는 중...";
            Game.RestoreSettings();
            Island.ReturnFromLevel();
        }
        public static bool InRoom => Phase != Phase.None;
        public static string Notice = "";

        // 현재 맵
        public static string MapHash, MapRel, MapTitle, MapSong = "", MapArtist = "", MapAuthor = "";
        public static string HostLevelPath => _hostLevelPath;
        // 맵 출처: 0 = 방장 PC (방장이 중계로 전송), 1 = TUF (각자 CDN에서 직접 받고, 실패하면 방장 중계)
        public static byte MapSource;
        public static int MapTufId;
        public static string MapDlUrl = "", MapDiff = "";
        public static Color MapDiffColor = Color.gray;
        static Tuf.Level _lastTuf;
        static FileDownload _tufDl;
        static float _nextTufReport;
        /// <summary>방장이 TUF에서 맵을 받는 중일 때 진행률 (UI 표시용, 없으면 -1)</summary>
        public static float HostFetchProgress = -1;
        public static long MapSize;
        static string _hostZip, _hostLevelPath;

        // 방장: 모든 참가자 상태 (자신 포함)
        public static readonly Dictionary<ulong, Entry> Peers = new Dictionary<ulong, Entry>();
        // 화면 표시용 (방장이 보내준 것)
        public static List<Entry> Board = new List<Entry>();
        public static List<Entry> Results = new List<Entry>();

        // 다운로드 (멤버)
        public static long DlReceived;
        public static bool DlPaused, DlCancelled, DlActive;
        static FileStream _dlStream;

        // 진행
        public static bool MyReady;
        static bool _resultsSent;
        static float _goAt, _nextProgress, _nextBoard, _notInGameSince = -1;
        public static float CountdownLeft => Mathf.Max(0, _goAt - Time.realtimeSinceStartup);

        // ===================== 방 입장/퇴장 =====================
        public static void OnRoomEntered(bool host)
        {
            ResetMatch();
            Peers.Clear();
            Phase = Phase.Lobby;
            if (host) Peers[Net.MyId] = new Entry { Id = Net.MyId, Name = Net.MyName, State = PState.Lobby };
            else Net.SendToHost(Packet.Build(Msg.Hello, w => { w.Write(Net.MyName); w.Write(Main.Version); }));
        }

        public static void LeaveRoom()
        {
            CloseDownload(true);
            Game.RestoreSettings();
            Net.Leave();
            Phase = Phase.None;
            Peers.Clear();
            Board.Clear();
            Results.Clear();
            MapHash = null;
        }

        public static void OnMemberJoined(ulong id, string name) { /* Hello를 받아야 정식 참가 */ }

        public static void OnMemberLeft(ulong id)
        {
            if (id == Net.HostId && !Net.IsHost) { Notice = "방장이 나갔습니다"; LeaveRoom(); return; }
            Peers.Remove(id);
        }

        static void ResetMatch()
        {
            MyReady = false;
            _resultsSent = false;
            Results.Clear();
            Game.RestoreSettings();
        }

        // ===================== 방장 동작 =====================
        public static async void HostDistribute(string levelPath)
        {
            if (!Net.IsHost) return;
            if (!File.Exists(levelPath)) { Notice = ".adofai 파일을 찾을 수 없습니다"; return; }
            Phase = Phase.Packing;
            Notice = "맵 압축 중...";
            try
            {
                var p = await MapPack.PackAsync(levelPath);
                MainThread.Post(() =>
                {
                    MapHash = p.Hash; MapRel = p.RelLevelFile; MapTitle = p.Title; MapSize = p.Size;
                    MapSource = 0; MapTufId = 0; MapDlUrl = ""; MapDiff = ""; _lastTuf = null;
                    var info = MapBrowser.ReadInfo(levelPath);
                    MapSong = info?.Song ?? ""; MapArtist = info?.Artist ?? ""; MapAuthor = info?.Author ?? "";
                    _hostZip = p.ZipFile; _hostLevelPath = levelPath;
                    foreach (var e in Peers.Values) { e.State = PState.Lobby; e.Download = 0; e.Stats = new Stats(); }
                    Peers[Net.MyId].State = PState.HasMap;
                    Peers[Net.MyId].Download = 1;
                    Phase = Phase.Distributing;
                    Notice = $"맵 배포 중: {MapTitle} ({MapSize / 1048576f:0.0}MB)";
                    Net.Broadcast(OfferPacket());
                });
            }
            catch (Exception ex) { MainThread.Post(() => { Phase = Phase.Lobby; Notice = "압축 실패: " + ex.Message; }); }
        }

        static byte[] OfferPacket() => Packet.Build(Msg.MapOffer, w =>
        {
            w.Write(MapHash); w.Write(MapSize); w.Write(MapRel); w.Write(MapTitle); w.Write(MapSong ?? ""); w.Write(MapArtist ?? ""); w.Write(MapAuthor ?? "");
            w.Write(MapSource); w.Write(MapTufId); w.Write(MapDlUrl ?? ""); w.Write(MapDiff ?? ""); w.Write(ColorUtility.ToHtmlStringRGB(MapDiffColor));
        });

        /// <summary>방장: TUF 레벨을 받아서(캐시 있으면 건너뜀) 압축 풀고 모두에게 배포</summary>
        public static void HostDistributeTuf(Tuf.Level lv)
        {
            if (!Net.IsHost || lv == null || !lv.CanDownload) return;
            if (Phase == Phase.Packing || (InMatch && Phase != Phase.Results)) return;
            Runner.Instance.StartCoroutine(HostTufCo(lv));
        }

        static IEnumerator HostTufCo(Tuf.Level lv)
        {
            Phase = Phase.Packing;
            string hash = lv.Hash;
            string zip = MapPack.ZipPath(hash), dir = MapPack.MapDir(hash);
            Directory.CreateDirectory(MapPack.Root);
            if (!File.Exists(zip))
            {
                Notice = $"TUF에서 받는 중: {lv.Song}";
                var dl = new FileDownload(lv.DlLink, zip + ".part");
                HostFetchProgress = 0;
                var co = Runner.Instance.StartCoroutine(dl.Run(null));
                while (dl.Running || (!dl.Done && !dl.Failed && !dl.Cancelled)) { HostFetchProgress = dl.Progress; yield return null; if (!InRoom) { dl.Cancelled = true; } }
                HostFetchProgress = -1;
                if (!dl.Done) { Phase = Phase.Lobby; Notice = "TUF에서 받지 못했어요: " + dl.Error; yield break; }
                if (File.Exists(zip)) File.Delete(zip);
                File.Move(zip + ".part", zip);
            }
            Notice = "압축 푸는 중...";
            string rel = null; Exception err = null;
            var t = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    rel = MapPack.FindLevelFile(dir);
                    if (rel == null) { MapPack.SafeExtract(zip, dir); rel = MapPack.FindLevelFile(dir); }
                }
                catch (Exception ex) { err = ex; }
            });
            while (!t.IsCompleted) yield return null;
            if (err != null || rel == null) { Phase = Phase.Lobby; Notice = rel == null && err == null ? "zip 안에 .adofai 파일이 없어요" : "압축을 풀지 못했어요: " + err?.Message; yield break; }
            if (!InRoom || !Net.IsHost) yield break;

            MapHash = hash; MapRel = rel; MapTitle = lv.Song; MapSize = new FileInfo(zip).Length;
            MapSong = lv.Song; MapArtist = lv.Artist; MapAuthor = lv.Creator;
            MapSource = 1; MapTufId = lv.Id; MapDlUrl = lv.DlLink; MapDiff = lv.Diff; MapDiffColor = lv.DiffColor;
            _hostZip = zip; _hostLevelPath = MapPack.LevelPath(hash, rel); _lastTuf = lv;
            foreach (var e in Peers.Values) { e.State = PState.Lobby; e.Download = 0; e.Stats = new Stats(); }
            Peers[Net.MyId].State = PState.HasMap; Peers[Net.MyId].Download = 1;
            Phase = Phase.Distributing;
            Notice = $"맵 배포 중 (TUF): {lv.Song}";
            Net.Broadcast(OfferPacket());
        }

        /// <summary>방장: 같은 맵으로 한 판 더 (TUF 맵이면 캐시에서 바로)</summary>
        public static void HostRedo()
        {
            if (_lastTuf != null && MapSource == 1) HostDistributeTuf(_lastTuf);
            else if (!string.IsNullOrEmpty(_hostLevelPath)) HostDistribute(_hostLevelPath);
        }

        /// <summary>맵 없는 사람(취소한 사람)은 빼고 강제로 레벨 로드</summary>
        public static void HostStartLoad()
        {
            if (!Net.IsHost || MapHash == null) return;
            foreach (var e in Peers.Values) if (e.State == PState.HasMap) e.State = PState.Loading;
            var pkt = Packet.Build(Msg.LoadLevel, w => w.Write(MapHash));
            foreach (var e in Peers.Values.ToList()) if (e.State == PState.Loading) Net.Send(e.Id, pkt);
        }

        public static void HostShowResults()
        {
            if (!Net.IsHost) return;
            var list = Participants().ToList();
            list.Sort(Entry.Compare);
            Net.Broadcast(Packet.Build(Msg.Results, w => Packet.WriteEntries(w, list)));
        }

        public static void HostBackToLobby()
        {
            if (!Net.IsHost) return;
            Net.Broadcast(Packet.Build(Msg.BackToLobby));
        }

        static IEnumerable<Entry> Participants() =>
            Peers.Values.Where(e => e.State >= PState.Loading && e.State != PState.Cancelled);

        static void HostTick()
        {
            // 모두 맵을 받으면 자동으로 개인 플레이 화면으로
            if (Phase == Phase.Distributing && Peers.Count > 0 &&
                Peers.Values.All(e => e.State == PState.HasMap || e.State == PState.Cancelled))
                HostStartLoad();

            // 모두 준비되면 GO
            var parts = Participants().ToList();
            if (Phase == Phase.WaitingStart && parts.Count > 0 && parts.All(e => e.State == PState.Ready))
            {
                foreach (var e in parts) e.State = PState.Playing;
                var pkt = Packet.Build(Msg.Go, w => w.Write(CountdownMs));
                foreach (var e in parts) Net.Send(e.Id, pkt);
            }

            // 모두 끝나면 결과
            if (!_resultsSent && parts.Count > 0 && parts.All(e => e.State == PState.Finished))
            {
                _resultsSent = true;
                HostShowResults();
            }

            if (Time.realtimeSinceStartup >= _nextBoard)
            {
                _nextBoard = Time.realtimeSinceStartup + 0.3f;
                var list = Peers.Values.ToList();
                list.Sort(Entry.Compare);
                Net.Broadcast(Packet.Build(Msg.Board, w => Packet.WriteEntries(w, list)));
            }
        }

        static void HostSendChunk(ulong to, long offset)
        {
            if (_hostZip == null || offset >= MapSize) return;
            int len = (int)Math.Min(ChunkSize, MapSize - offset);
            var data = new byte[len];
            using (var fs = File.OpenRead(_hostZip)) { fs.Seek(offset, SeekOrigin.Begin); fs.Read(data, 0, len); }
            Net.Send(to, Packet.Build(Msg.MapChunk, w => { w.Write(MapHash); w.Write(offset); w.Write(len); w.Write(data); }));
        }

        // ===================== 멤버 동작 =====================
        public static void DlPause()
        {
            DlPaused = true;
            if (_tufDl != null) _tufDl.Paused = true;
            Net.SendToHost(Packet.Build(Msg.MapPause));
        }
        public static void DlResume()
        {
            DlPaused = false;
            if (_tufDl != null) { _tufDl.Paused = false; Net.SendToHost(Packet.Build(Msg.MapProgress, w => w.Write(_tufDl.Progress))); }
            else Net.SendToHost(Packet.Build(Msg.MapNeed, w => { w.Write(MapHash); w.Write(DlReceived); }));
        }
        public static void DlCancel()
        {
            DlCancelled = true;
            if (_tufDl != null) { _tufDl.Cancelled = true; _tufDl = null; }
            CloseDownload(true);
            Net.SendToHost(Packet.Build(Msg.MapCancel));
        }
        public static void DlRetry() { DlCancelled = false; StartDownload(); }

        public static void ToggleReady()
        {
            if (Phase != Phase.WaitingStart) return;
            MyReady = !MyReady;
            Net.SendToHost(Packet.Build(Msg.Ready, w => w.Write(MyReady)));
        }

        /// <summary>멤버: TUF CDN에서 직접 받기. 실패하면 방장에게서 받기로 전환</summary>
        static void StartTufDownload()
        {
            CloseDownload(true);
            Directory.CreateDirectory(MapPack.Root);
            DlReceived = 0; DlPaused = false; DlActive = true;
            var hash = MapHash;
            _tufDl = new FileDownload(MapDlUrl, MapPack.PartPath(hash));
            Runner.Instance.StartCoroutine(_tufDl.Run(d =>
            {
                if (d != _tufDl || hash != MapHash) return;
                _tufDl = null;
                if (d.Done) { DlReceived = MapSize; FinishDownload(); }
                else if (!d.Cancelled)
                {
                    Main.Log("TUF download failed, falling back to host: " + d.Error);
                    Notice = "TUF에서 받지 못해서 방장에게서 받는 중...";
                    StartDownload();
                }
            }));
            Net.SendToHost(Packet.Build(Msg.MapProgress, w => w.Write(0f)));
        }

        static void StartDownload()
        {
            Directory.CreateDirectory(MapPack.Root);
            CloseDownload(true);
            _dlStream = new FileStream(MapPack.PartPath(MapHash), FileMode.Create, FileAccess.Write);
            DlReceived = 0; DlPaused = false; DlActive = true;
            Net.SendToHost(Packet.Build(Msg.MapNeed, w => { w.Write(MapHash); w.Write(0L); }));
        }

        static void CloseDownload(bool delete)
        {
            DlActive = false;
            if (delete && _tufDl != null) { _tufDl.Cancelled = true; _tufDl = null; }
            if (_dlStream != null) { _dlStream.Dispose(); _dlStream = null; }
            if (delete && MapHash != null) { try { File.Delete(MapPack.PartPath(MapHash)); } catch { } }
        }

        static async void FinishDownload()
        {
            CloseDownload(false);
            Notice = "압축 해제 중...";
            var hash = MapHash;
            bool ok;
            try { ok = await MapPack.ExtractAsync(hash); } catch (Exception ex) { Main.Log(ex.ToString()); ok = false; }
            MainThread.Post(() =>
            {
                if (hash != MapHash) return;
                if (!ok) { Notice = "맵 파일이 손상됐습니다. 다시 받아주세요"; DlCancelled = true; Net.SendToHost(Packet.Build(Msg.MapCancel)); return; }
                if (!MapPack.Has(hash, MapRel)) { var rel = MapPack.FindLevelFile(MapPack.MapDir(hash)); if (rel != null) MapRel = rel; }
                Notice = "맵 준비 완료";
                Net.SendToHost(Packet.Build(Msg.MapHave, w => w.Write(hash)));
            });
        }

        // ===================== 매 프레임 =====================
        public static void Update()
        {
            if (_pendingJoin != null && !Game.IsScnGame) { var code = _pendingJoin; _pendingJoin = null; RequestJoin(code); }
            if (!InRoom) return;
            if (Net.IsHost)
            {
                string ph = Phase == Phase.Packing || Phase == Phase.Distributing ? "distributing"
                          : Phase == Phase.Results ? "results"
                          : Phase >= Phase.Loading ? "playing" : "lobby";
                Net.PublishMeta(ph, MapHash != null ? MapSong : "", MapHash != null ? MapArtist : "");
            }
            // 방에 있는 동안 메뉴로 나오면 항상 멀티 로비 화면 (멀티는 로비 안에서만)
            if (!Game.IsScnGame && !Island.InIsland && !Island.Busy && Phase != Phase.Loading) Island.Enter();
            if (Game.InGame && Input.GetKeyDown(KeyCode.Q) && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift)) Forfeit();
            if (Net.IsHost) HostTick();

            // TUF 직접 다운로드 진행률을 방장에게 보고
            if (_tufDl != null && _tufDl.Running)
            {
                DlReceived = _tufDl.Received;
                if (_tufDl.Total > 0) MapSize = _tufDl.Total;
                if (!DlPaused && Time.realtimeSinceStartup >= _nextTufReport)
                {
                    _nextTufReport = Time.realtimeSinceStartup + 0.3f;
                    float pr = _tufDl.Progress;
                    Net.SendToHost(Packet.Build(Msg.MapProgress, w => w.Write(pr)));
                }
            }

            switch (Phase)
            {
                case Phase.Loading:
                    if (Game.WaitingSeen)
                    {
                        Phase = Phase.WaitingStart;
                        Net.SendToHost(Packet.Build(Msg.Loaded));
                    }
                    break;
                case Phase.WaitingStart:
                    Game.KeepNoFail();
                    if (Input.GetKeyDown(KeyCode.F5)) ToggleReady();
                    break;
                case Phase.Countdown:
                    Game.KeepNoFail();
                    if (Time.realtimeSinceStartup >= _goAt)
                    {
                        Game.GateClosed = false;
                        Game.AutoStartOnce = true;
                        Phase = Phase.Playing;
                        _notInGameSince = -1;
                    }
                    break;
                case Phase.Playing:
                    Game.KeepNoFail();
                    bool won = Game.IsWon();
                    // 레벨을 나가버리면 기권 처리
                    if (!Game.InGame)
                    {
                        if (_notInGameSince < 0) _notInGameSince = Time.realtimeSinceStartup;
                        else if (Time.realtimeSinceStartup - _notInGameSince > 1.5f) won = true;
                    }
                    else _notInGameSince = -1;

                    if (won || Time.realtimeSinceStartup >= _nextProgress)
                    {
                        _nextProgress = Time.realtimeSinceStartup + 0.2f;
                        var st = Game.InGame ? Game.ReadStats() : LastStats ?? new Stats();
                        st.Finished = won;
                        LastStats = st;
                        Net.SendToHost(Packet.Build(Msg.Progress, w => st.Write(w)));
                        if (won) { Phase = Phase.Results; Notice = "다른 플레이어를 기다리는 중..."; Game.RestoreSettings(); }
                    }
                    break;
            }
        }

        public static Stats LastStats;

        // ===================== 메시지 처리 =====================
        public static void Handle(ulong from, byte[] data)
        {
            if (data.Length == 0) return;
            using (var ms = new MemoryStream(data, 1, data.Length - 1))
            using (var r = new BinaryReader(ms))
            {
                var type = (Msg)data[0];
                bool fromHost = from == Net.HostId;
                if (Net.IsHost) HandleAsHost(type, from, r);
                if (fromHost) HandleFromHost(type, r);
            }
        }

        static void HandleAsHost(Msg type, ulong from, BinaryReader r)
        {
            Peers.TryGetValue(from, out var e);
            switch (type)
            {
                case Msg.Hello:
                {
                    var name = r.ReadString(); var ver = r.ReadString();
                    if (ver != Main.Version)
                    {
                        string tip = Updater.IsNewer(Main.Version, ver) ? "게임을 재시작하면 자동으로 업데이트돼요." : "방장이 게임을 재시작해서 업데이트해야 해요.";
                        Net.Send(from, Packet.Build(Msg.Reject, w => w.Write($"버전이 달라요 (방장 v{Main.Version} / 나 v{ver}). {tip}")));
                        return;
                    }
                    if (Phase >= Phase.Loading) { Net.Send(from, Packet.Build(Msg.Reject, w => w.Write("게임이 진행 중입니다. 끝나고 다시 들어와 주세요"))); return; }
                    Peers[from] = new Entry { Id = from, Name = name, State = PState.Lobby };
                    Net.Send(from, Packet.Build(Msg.Welcome));
                    if (MapHash != null) Net.Send(from, OfferPacket());
                    break;
                }
                case Msg.MapNeed:
                    if (e == null || r.ReadString() != MapHash) return;
                    e.State = PState.Downloading;
                    HostSendChunk(from, r.ReadInt64());
                    break;
                case Msg.MapAck:
                {
                    if (e == null || r.ReadString() != MapHash) return;
                    long next = r.ReadInt64();
                    e.Download = MapSize > 0 ? (float)next / MapSize : 1;
                    if (e.State == PState.Downloading) HostSendChunk(from, next);
                    break;
                }
                case Msg.MapPause: if (e != null) e.State = PState.Paused; break;
                case Msg.MapProgress:
                    if (e != null && e.State != PState.HasMap && e.State != PState.Cancelled) { e.State = PState.Downloading; e.Download = r.ReadSingle(); }
                    break;
                case Msg.MapCancel: if (e != null) { e.State = PState.Cancelled; e.Download = 0; } break;
                case Msg.MapHave:
                    if (e != null && r.ReadString() == MapHash) { e.State = PState.HasMap; e.Download = 1; }
                    break;
                case Msg.Loaded: if (e != null) e.State = PState.Loaded; break;
                case Msg.Ready: if (e != null) e.State = r.ReadBoolean() ? PState.Ready : PState.Loaded; break;
                case Msg.Progress:
                    if (e != null)
                    {
                        e.Stats = Stats.Read(r);
                        e.State = e.Stats.Finished ? PState.Finished : PState.Playing;
                    }
                    break;
            }
        }

        static void HandleFromHost(Msg type, BinaryReader r)
        {
            switch (type)
            {
                case Msg.Welcome: Notice = "방에 들어왔습니다"; break;
                case Msg.Reject: Notice = r.ReadString(); LeaveRoom(); break;
                case Msg.MapOffer:
                    MapHash = r.ReadString(); MapSize = r.ReadInt64(); MapRel = r.ReadString(); MapTitle = r.ReadString(); MapSong = r.ReadString(); MapArtist = r.ReadString(); MapAuthor = r.ReadString();
                    MapSource = r.ReadByte(); MapTufId = r.ReadInt32(); MapDlUrl = r.ReadString(); MapDiff = r.ReadString();
                    { ColorUtility.TryParseHtmlString("#" + r.ReadString(), out var dc); MapDiffColor = dc; }
                    ResetMatch();
                    if (Net.IsHost) break;
                    Phase = Phase.Distributing;
                    DlCancelled = false;
                    if (MapPack.Has(MapHash, MapRel)) { Notice = "이미 받은 맵입니다"; Net.SendToHost(Packet.Build(Msg.MapHave, w => w.Write(MapHash))); }
                    else if (MapSource == 1 && !string.IsNullOrEmpty(MapDlUrl)) { Notice = $"TUF에서 받는 중: {MapTitle}"; StartTufDownload(); }
                    else { Notice = $"맵 받는 중: {MapTitle}"; StartDownload(); }
                    break;
                case Msg.MapChunk:
                {
                    var hash = r.ReadString(); long off = r.ReadInt64(); int len = r.ReadInt32(); var bytes = r.ReadBytes(len);
                    if (hash != MapHash || !DlActive || _dlStream == null || off != DlReceived) return;
                    _dlStream.Write(bytes, 0, bytes.Length);
                    DlReceived += bytes.Length;
                    if (DlReceived >= MapSize) FinishDownload();
                    else Net.SendToHost(Packet.Build(Msg.MapAck, w => { w.Write(MapHash); w.Write(DlReceived); })); // 일시정지 중이면 방장이 다음 조각을 안 보냄
                    break;
                }
                case Msg.LoadLevel:
                {
                    var hash = r.ReadString();
                    if (hash != MapHash) return;
                    var path = Net.IsHost ? _hostLevelPath : MapPack.LevelPath(MapHash, MapRel);
                    MyReady = false;
                    Phase = Phase.Loading; Notice = "레벨 불러오는 중...";
                    Island.PlayLevel(path, (ok, err) => { if (!ok) { Notice = err; Phase = Phase.Lobby; } });
                    break;
                }
                case Msg.Go:
                    if (Phase != Phase.WaitingStart) return;
                    _goAt = Time.realtimeSinceStartup + r.ReadInt32() / 1000f;
                    Phase = Phase.Countdown;
                    break;
                case Msg.Board:
                    Board = Packet.ReadEntries(r);
                    break;
                case Msg.Results:
                    Results = Packet.ReadEntries(r);
                    Phase = Phase.Results;
                    Notice = "결과 발표!";
                    Game.RestoreSettings();
                    UI.ShowResults = true;
                    break;
                case Msg.BackToLobby:
                    ResetMatch();
                    Phase = Phase.Lobby;
                    UI.ShowResults = false;
                    if (Game.IsScnGame) Island.ReturnFromLevel();
                    else if (!Island.InIsland) Island.Enter();
                    if (Net.IsHost) foreach (var e in Peers.Values) { e.State = PState.Lobby; e.Stats = new Stats(); }
                    Notice = "로비로 돌아왔습니다";
                    break;
            }
        }
    }
}
