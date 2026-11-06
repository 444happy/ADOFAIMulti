using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    public static class UI
    {
        /// <summary>전용 멀티 로비 화면 (메인 메뉴 위를 덮음)</summary>
        public static bool LobbyOpen;
        public static bool ShowResults;
        static bool _confirmLeave;

        static string _joinId = "", _mapPath = "", _search = "";
        static Vector2 _scrollPeers, _scrollMaps, _scrollRes;
        static GUIStyle _h1, _h2, _txt, _small, _big, _card, _btn, _bigBtn, _row;
        static Texture2D _bg, _cardTex, _fire, _ice, _barBg;

        public static void ToggleLobby()
        {
            if (LobbyOpen)
            {
                if (Session.InRoom) _confirmLeave = true;
                else LobbyOpen = false;
            }
            else if (!Game.IsScnGame) LobbyOpen = true;
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); Object.DontDestroyOnLoad(t); return t; }

        static void Styles()
        {
            if (_h1 != null) return;
            _bg = Tex(new Color(0.055f, 0.06f, 0.12f, 0.97f));
            _cardTex = Tex(new Color(0.10f, 0.11f, 0.20f, 1f));
            _fire = Tex(new Color(1f, 0.36f, 0.24f));
            _ice = Tex(new Color(0.31f, 0.70f, 1f));
            _barBg = Tex(new Color(0.18f, 0.19f, 0.33f));
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, richText = true };
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            _txt = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _row = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = true };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            _card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14), margin = new RectOffset(8, 8, 8, 8) };
            _card.normal.background = _cardTex;
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 34 };
            _bigBtn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, fixedHeight = 52 };
        }

        public static void Draw()
        {
            Styles();
            if (LobbyOpen && !Game.IsScnGame) { DrawLobby(); return; }
            if (!Game.IsScnGame && !Session.InRoom) DrawMenuButton();
            if (Session.InRoom && Game.IsScnGame) DrawInGame();
        }

        // ================= 메인 메뉴 [멀티] 버튼 =================
        static void DrawMenuButton()
        {
            var r = new Rect(Screen.width - 230, Screen.height - 90, 210, 64);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width / 2 + 3, r.height + 6), _fire);
            GUI.DrawTexture(new Rect(r.x + r.width / 2, r.y - 3, r.width / 2 + 3, r.height + 6), _ice);
            if (Updater.Current == Updater.State.ReadyToRestart)
                GUI.Label(new Rect(r.x - 120, r.y - 30, r.width + 120, 26), "<color=#7CFC00><size=14>멀티 v" + Updater.Latest + " 업데이트됨 · 재시작하면 적용</size></color>", _small);
            if (GUI.Button(r, "<b>멀티플레이</b>\n<size=12>Ctrl + M</size>", new GUIStyle(_bigBtn) { richText = true, fixedHeight = 0 })) LobbyOpen = true;
        }

        // ================= 전용 멀티 로비 =================
        static void DrawLobby()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { ToggleLobby(); e.Use(); }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bg);
            GUI.DrawTexture(new Rect(0, 0, Screen.width / 2f, 4), _fire);
            GUI.DrawTexture(new Rect(Screen.width / 2f, 0, Screen.width / 2f, 4), _ice);

            float m = Mathf.Max(24, Screen.width * 0.04f);
            GUILayout.BeginArea(new Rect(m, 20, Screen.width - m * 2, Screen.height - 40));

            GUILayout.BeginHorizontal();
            GUILayout.Label("얼불춤 <color=#ff5c3d>멀</color><color=#4fb3ff>티</color>  <size=16><color=#8a8fb8>v" + Main.Version + "</color></size>", _h1);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Session.InRoom ? "방 나가기 (ESC)" : "메뉴로 (ESC)", _btn, GUILayout.Width(170))) ToggleLobby();
            GUILayout.EndHorizontal();

            string status = string.Join("   ·   ", new[] { Net.Status, Session.Notice, Updater.StatusText }.Where(s => !string.IsNullOrEmpty(s)).ToArray());
            GUILayout.Label("<color=#a3a8cc>" + status + "</color>", _small);
            GUILayout.Space(6);

            if (!Session.InRoom) DrawNoRoom();
            else DrawRoom();

            GUILayout.EndArea();

            if (_confirmLeave) DrawConfirm();
        }

        static void DrawNoRoom()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(_card, GUILayout.Width(520));
            GUILayout.Label("방 만들기", _h2);
            GUILayout.Label("방장이 맵을 고르면 모두에게 자동으로 전송돼요. 최대 8명.", _txt);
            if (GUILayout.Button("새 방 만들기", _bigBtn)) Net.CreateRoom(8);
            GUILayout.Space(18);
            GUILayout.Label("참가하기", _h2);
            GUILayout.Label("Steam 친구 초대를 수락하면 자동으로 들어와요. 또는 로비 ID 입력:", _txt);
            GUILayout.BeginHorizontal();
            _joinId = GUILayout.TextField(_joinId, new GUIStyle(GUI.skin.textField) { fontSize = 18, fixedHeight = 34 });
            if (GUILayout.Button("참가", _btn, GUILayout.Width(90)) && ulong.TryParse(_joinId.Trim(), out var lid)) Net.Join(lid);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        static void DrawRoom()
        {
            bool host = Net.IsHost;
            GUILayout.BeginHorizontal();

            // ---- 왼쪽: 참가자 ----
            GUILayout.BeginVertical(_card, GUILayout.Width(Mathf.Max(340, Screen.width * 0.3f)), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"참가자 {Session.Board.Count}/8", _h2);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("친구 초대", _btn, GUILayout.Width(100))) Net.InviteOverlay();
            GUILayout.EndHorizontal();
            if (Net.CurrentLobby.HasValue && GUILayout.Button("로비 ID 복사", _btn)) GUIUtility.systemCopyBuffer = Net.CurrentLobby.Value.Id.Value.ToString();
            GUILayout.Space(6);
            _scrollPeers = GUILayout.BeginScrollView(_scrollPeers);
            foreach (var p in Session.Board)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                string me = p.Id == Net.MyId ? " <color=#ffd84a>(나)</color>" : "";
                string crown = p.Id == Net.HostId ? "<color=#ffd84a>[방장]</color> " : "";
                GUILayout.Label($"{crown}<b>{Trim(p.Name, 16)}</b>{me}", _small);
                GUILayout.Label("<color=#a3a8cc>" + StateText(p) + "</color>", _small);
                if (p.State == PState.Downloading || p.State == PState.Paused) Bar(p.Download, 10);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // ---- 오른쪽: 맵 / 진행 ----
            GUILayout.BeginVertical(_card, GUILayout.ExpandHeight(true));
            if (Session.Results.Count > 0 && (Session.Phase == Phase.Results || Session.Phase == Phase.Lobby)) DrawResultsPanel();
            else if (Session.Phase == Phase.Results) GUILayout.Label("결과를 기다리는 중...", _h2);
            else
            {
                GUILayout.Label("맵", _h2);
                if (Session.MapHash != null)
                    GUILayout.Label($"<b>{Session.MapTitle}</b>  <color=#a3a8cc>({Session.MapSize / 1048576f:0.0}MB)</color>", _txt);
                else GUILayout.Label(host ? "아래에서 맵을 골라 배포하세요." : "방장이 맵을 고르는 중...", _txt);

                if (host) DrawHostControls();
                else DrawMemberDownload();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        static void DrawHostControls()
        {
            bool busy = Session.Phase == Phase.Packing || Session.InMatch;
            GUILayout.BeginHorizontal();
            GUI.enabled = Session.Phase == Phase.Distributing;
            if (GUILayout.Button("받은 사람끼리 바로 시작", _btn)) Session.HostStartLoad();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("맵 찾기", _h2);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(MapBrowser.Scanning ? "검색 중..." : "새로고침", _btn, GUILayout.Width(110))) MapBrowser.Rescan();
            GUILayout.EndHorizontal();
            MapBrowser.ScanOnce();

            _search = GUILayout.TextField(_search, new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 30 });
            _scrollMaps = GUILayout.BeginScrollView(_scrollMaps, GUILayout.ExpandHeight(true));
            var q = _search.Trim();
            int shown = 0;
            foreach (var it in MapBrowser.Items)
            {
                if (q.Length > 0 && it.Label.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0 && (it.Author ?? "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > 300) break;
                bool sel = it.Path == _mapPath;
                string label = $"{(sel ? "<color=#ffd84a>> </color>" : "")}{Trim(it.Label, 60)}  <color=#8a8fb8><size=12>{Trim(it.Author, 20)}</size></color>";
                if (GUILayout.Button(label, _row, GUILayout.Height(30))) _mapPath = it.Path;
            }
            if (MapBrowser.Items.Count == 0 && !MapBrowser.Scanning) GUILayout.Label("맵을 못 찾았어요. 아래에 .adofai 경로를 직접 넣어주세요.", _txt);
            GUILayout.EndScrollView();

            GUILayout.Label("<color=#a3a8cc>경로 (직접 입력 가능)</color>", _small);
            GUILayout.BeginHorizontal();
            _mapPath = GUILayout.TextField(_mapPath, new GUIStyle(GUI.skin.textField) { fontSize = 14, fixedHeight = 30 });
            if (GUILayout.Button("최근 맵", _btn, GUILayout.Width(90))) _mapPath = Game.DefaultLevelPath();
            GUILayout.EndHorizontal();
            GUI.enabled = !busy && !string.IsNullOrEmpty(_mapPath);
            if (GUILayout.Button("이 맵으로 배포", _bigBtn)) Session.HostDistribute(_mapPath.Trim().Trim('"'));
            GUI.enabled = true;
        }

        static void DrawMemberDownload()
        {
            if (Session.Phase != Phase.Distributing || Session.MapSize <= 0) return;
            GUILayout.Space(10);
            if (Session.DlCancelled)
            {
                GUILayout.Label("다운로드를 취소했습니다 (이번 판은 관전)", _txt);
                if (GUILayout.Button("다시 받기", _btn)) Session.DlRetry();
                return;
            }
            if (!Session.DlActive) { GUILayout.Label("맵 준비 완료! 다른 사람을 기다리는 중...", _txt); return; }
            float p = (float)Session.DlReceived / Session.MapSize;
            GUILayout.Label($"다운로드 {p * 100:0}%  <color=#a3a8cc>({Session.DlReceived / 1048576f:0.0} / {Session.MapSize / 1048576f:0.0}MB){(Session.DlPaused ? "  · 일시정지" : "")}</color>", _txt);
            Bar(p, 18);
            GUILayout.BeginHorizontal();
            if (Session.DlPaused) { if (GUILayout.Button("재개", _btn)) Session.DlResume(); }
            else if (GUILayout.Button("일시정지", _btn)) Session.DlPause();
            if (GUILayout.Button("취소", _btn)) Session.DlCancel();
            GUILayout.EndHorizontal();
        }

        static void DrawResultsPanel()
        {
            GUILayout.Label("결과", _h2);
            _scrollRes = GUILayout.BeginScrollView(_scrollRes, GUILayout.ExpandHeight(true));
            ResultsTable();
            GUILayout.EndScrollView();
            if (Net.IsHost)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("다음 판 (맵 고르기)", _bigBtn)) { Session.HostBackToLobby(); Session.Results.Clear(); }
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("<color=#a3a8cc>방장이 다음 맵을 고르는 중...</color>", _small);
        }

        static void ResultsTable()
        {
            GUILayout.Label("<color=#a3a8cc><size=13>순위   이름                정확도      X정확도     빨!!/빨!/조빠/완벽/조느/느!/느!!    미스</size></color>", _small);
            int rank = 1;
            foreach (var e in Session.Results)
            {
                var s = e.Stats;
                string c = rank == 1 ? "#ffd84a" : rank == 2 ? "#d8d8d8" : rank == 3 ? "#e09a5a" : "white";
                GUILayout.Label($"<color={c}><size=18><b>{rank}</b></size>   {Trim(e.Name, 10),-12}   <b>{s.Acc * 100:0.00}%</b>    {s.XAcc * 100:0.00}%    " +
                                $"{s.TooEarly}/{s.VeryEarly}/{s.EarlyPerfect}/<b>{s.Perfect}</b>/{s.LatePerfect}/{s.VeryLate}/{s.TooLate}    {s.Miss + s.Overload}" +
                                $"{(s.Progress < 0.999f ? $"  <size=12>(기권 · {s.Progress * 100:0}%)</size>" : "")}</color>", _small);
                rank++;
            }
        }

        static void DrawConfirm()
        {
            var r = new Rect((Screen.width - 420) / 2f, (Screen.height - 170) / 2f, 420, 170);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bg);
            GUILayout.BeginArea(r, _card);
            GUILayout.Label("방에서 나갈까요?", _h2);
            GUILayout.Label(Net.IsHost ? "방장이 나가면 방이 사라져요." : "다시 들어오려면 초대를 받아야 해요.", _txt);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("나가기", _btn)) { _confirmLeave = false; Session.LeaveRoom(); LobbyOpen = false; }
            if (GUILayout.Button("취소", _btn)) _confirmLeave = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ================= 레벨 안 (랭킹/카운트다운) =================
        static void DrawInGame()
        {
            var ph = Session.Phase;
            var board = Session.Board.Where(e => e.State >= PState.Loading && e.State != PState.Cancelled).ToList();
            float h = 34 + board.Count * 22;
            float y = Screen.height * 0.22f;
            GUI.DrawTexture(new Rect(8, y, 310, h), _cardTex);
            GUILayout.BeginArea(new Rect(16, y + 4, 300, h));
            GUILayout.Label("<b>실시간 랭킹</b>", _small);
            int rank = 1;
            foreach (var e in board)
            {
                string col = e.Id == Net.MyId ? "#ffd84a" : "white";
                string st = e.State == PState.Finished ? " (완주)" : e.State == PState.Ready ? " (준비)" : e.State == PState.Loaded ? " (대기)" : "";
                GUILayout.Label($"<color={col}>{rank}. {Trim(e.Name, 8)}  {e.Stats.Acc * 100:0.00}%  <size=12>X {e.Stats.XAcc * 100:0.00}% · {e.Stats.Progress * 100:0}%</size>{st}</color>", _small);
                rank++;
            }
            GUILayout.EndArea();

            if (ph == Phase.WaitingStart)
            {
                string msg = Session.MyReady ? "준비 완료! 다른 사람을 기다리는 중..." : "F5 를 눌러 준비";
                GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 80), $"<size=32>{msg}</size>", _big);
            }
            else if (ph == Phase.Countdown)
            {
                int n = Mathf.CeilToInt(Session.CountdownLeft);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 120), n > 0 ? n.ToString() : "GO!", _big);
            }
            else if (ph == Phase.Results)
            {
                if (Session.Results.Count > 0)
                {
                    var r = new Rect((Screen.width - 680) / 2f, (Screen.height - 420) / 2f, 680, 420);
                    GUILayout.BeginArea(r, _card);
                    GUILayout.Label("결과", _h2);
                    ResultsTable();
                    GUILayout.FlexibleSpace();
                    if (Net.IsHost && GUILayout.Button("모두 로비로", _bigBtn)) Session.HostBackToLobby();
                    else if (!Net.IsHost) GUILayout.Label("<color=#a3a8cc>방장이 로비로 돌아가면 같이 이동해요</color>", _small);
                    GUILayout.EndArea();
                }
                else GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 80), "<size=28>완주! 다른 사람을 기다리는 중...</size>", _big);
            }

            if (ph == Phase.Countdown || ph == Phase.Playing)
                GUI.Label(new Rect(Screen.width - 260, Screen.height - 30, 250, 24), "<color=#8a8fb8><size=12>기권: Ctrl + Shift + Q</size></color>", _small);
        }

        static void Bar(float p, float h)
        {
            var r = GUILayoutUtility.GetRect(100, h, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(r, _barBg);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(p) * 0.5f, r.height), _fire);
            GUI.DrawTexture(new Rect(r.x + r.width * Mathf.Clamp01(p) * 0.5f, r.y, r.width * Mathf.Clamp01(p) * 0.5f, r.height), _ice);
        }

        static string StateText(Entry e)
        {
            switch (e.State)
            {
                case PState.Lobby: return "대기";
                case PState.Downloading: return $"다운로드 {e.Download * 100:0}%";
                case PState.Paused: return $"일시정지 {e.Download * 100:0}%";
                case PState.Cancelled: return "다운 취소 (관전)";
                case PState.HasMap: return "<color=#7CFC00>맵 준비됨</color>";
                case PState.Loading: return "로딩 중";
                case PState.Loaded: return "레벨 입장 (준비 안 됨)";
                case PState.Ready: return "<color=#7CFC00>준비</color>";
                case PState.Playing: return $"플레이 중 {e.Stats.Progress * 100:0}%";
                case PState.Finished: return "완주";
            }
            return "";
        }

        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length > n ? s.Substring(0, n) + "…" : s);
    }
}
