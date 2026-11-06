using System.IO;
using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>Quartz 스타일 (다크 그린 · 사이드바 · 카드) IMGUI</summary>
    public static class UI
    {
        public enum Tab { Home, Room, Map, Results, Settings }
        public static Tab Page = Tab.Home;
        public static bool ShowResults;
        static bool _confirmLeave;

        static string _joinId = "", _mapPath = "", _search = "";
        static Vector2 _sPeers, _sMaps, _sRes;

        // ---- palette ----
        static readonly Color C_bg = Hex("0b1410"), C_side = Hex("101b16"), C_card = Hex("1d2a25"), C_cardHi = Hex("27372f"),
            C_line = Hex("2b3b34"), C_acc = Hex("74e0b0"), C_txt = Hex("e6efe9"), C_sub = Hex("8fa39a"),
            C_fire = Hex("ff6a4d"), C_ice = Hex("5ab8ff"), C_gold = Hex("ffd36b");

        static Texture2D T_bg, T_side, T_card, T_cardHi, T_acc, T_line, T_fire, T_ice, T_white, T_circle, T_dim, T_chip;
        static GUIStyle S_para, S_paraTxt, S_h1, S_h2, S_txt, S_sub, S_small, S_num, S_card, S_cardBtn, S_btn, S_btnAcc, S_side, S_sideOn, S_field, S_chip, S_big, S_row, S_rowOn, S_logo;
        static Font _font;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // 둥근 모서리 텍스처 (9-slice)
        static Texture2D Round(Color c, int r = 8)
        {
            int n = r * 2 + 2;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply(); Object.DontDestroyOnLoad(t); t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t; }

        static GUIStyle Box(Texture2D bg, int border, RectOffset pad)
        {
            var s = new GUIStyle { border = new RectOffset(border, border, border, border), padding = pad, margin = new RectOffset(6, 6, 6, 6) };
            s.normal.background = bg;
            return s;
        }

        static readonly System.Collections.Generic.Dictionary<string, GUIStyle> _labelCache = new System.Collections.Generic.Dictionary<string, GUIStyle>();

        /// <summary>라벨 스타일 (같은 조합은 캐시해서 매 프레임 새로 만들지 않음)</summary>
        static GUIStyle Label(int size, Color col, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false)
        {
            string key = size + "|" + HexOf(col) + "|" + (int)fs + "|" + (int)a + "|" + wrap;
            if (_labelCache.TryGetValue(key, out var cached)) return cached;
            var s = new GUIStyle(GUI.skin.label) { font = _font, fontSize = size, fontStyle = fs, alignment = a, richText = true, wordWrap = wrap, clipping = TextClipping.Clip };
            s.normal.textColor = col;
            _labelCache[key] = s;
            return s;
        }

        static GUIStyle Button(Texture2D n, Texture2D h, Color text, int size, int height)
        {
            var s = new GUIStyle { font = _font, fontSize = size, alignment = TextAnchor.MiddleCenter, richText = true, border = new RectOffset(8, 8, 8, 8), padding = new RectOffset(14, 14, 6, 6), margin = new RectOffset(4, 4, 4, 4), fixedHeight = height };
            s.normal.background = n; s.hover.background = h; s.active.background = h; s.focused.background = n;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = text;
            return s;
        }

        static void Styles()
        {
            if (S_h1 != null) return;
            try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Pretendard", "Pretendard Variable", "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 16); } catch { }

            T_bg = Solid(C_bg); T_side = Solid(C_side); T_line = Solid(C_line); T_white = Solid(Color.white);
            T_dim = Solid(new Color(0, 0, 0, 0.6f));
            T_card = Round(C_card); T_cardHi = Round(C_cardHi); T_acc = Round(C_acc);
            T_fire = Round(C_fire, 4); T_ice = Round(C_ice, 4); T_chip = Round(Hex("22302a"), 5);
            T_circle = Round(Color.white, 32);

            S_h1 = Label(30, C_txt);
            S_h2 = Label(19, C_txt, FontStyle.Bold);
            S_txt = Label(16, C_txt);
            S_sub = Label(14, C_sub);
            S_para = Label(14, C_sub, FontStyle.Normal, TextAnchor.UpperLeft, true);
            S_paraTxt = Label(16, C_txt, FontStyle.Normal, TextAnchor.UpperLeft, true);
            S_small = Label(13, C_sub);
            S_num = Label(30, C_acc);
            S_big = Label(72, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            S_logo = Label(20, C_txt, FontStyle.Normal);
            S_card = Box(T_card, 8, new RectOffset(16, 16, 14, 14));
            S_cardBtn = Button(T_card, T_cardHi, C_txt, 16, 0); S_cardBtn.alignment = TextAnchor.UpperLeft; S_cardBtn.padding = new RectOffset(16, 16, 14, 14); S_cardBtn.margin = new RectOffset(6, 6, 6, 6);
            S_btn = Button(Round(Hex("2c3d36")), Round(Hex("374b43")), C_txt, 16, 40);
            S_btnAcc = Button(T_acc, Round(Hex("8cf0c4")), Hex("0b1410"), 17, 46); S_btnAcc.fontStyle = FontStyle.Bold;
            S_side = Button(null, Round(Hex("1c2a24")), C_sub, 13, 42); S_side.font = _font; S_side.padding = new RectOffset(0, 0, 0, 0); S_side.fixedWidth = 42;
            S_sideOn = Button(Round(Hex("22342c")), Round(Hex("22342c")), C_acc, 13, 42); S_sideOn.font = _font; S_sideOn.fontStyle = FontStyle.Bold; S_sideOn.padding = new RectOffset(0, 0, 0, 0); S_sideOn.fixedWidth = 42;
            S_field = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = 16, fixedHeight = 38, padding = new RectOffset(12, 12, 9, 9), border = new RectOffset(8, 8, 8, 8) };
            S_field.normal.background = Round(Hex("16211c")); S_field.focused.background = Round(Hex("1a2a23")); S_field.hover.background = S_field.normal.background;
            S_field.normal.textColor = S_field.focused.textColor = S_field.hover.textColor = C_txt;
            S_chip = Box(T_chip, 5, new RectOffset(8, 8, 3, 3)); S_chip.font = _font; S_chip.fontSize = 13; S_chip.normal.textColor = C_txt; S_chip.alignment = TextAnchor.MiddleCenter; S_chip.margin = new RectOffset(4, 4, 0, 0);
            S_row = Button(null, T_cardHi, C_txt, 15, 34); S_row.alignment = TextAnchor.MiddleLeft;
            S_rowOn = Button(Round(Hex("22342c")), Round(Hex("22342c")), C_acc, 15, 34); S_rowOn.alignment = TextAnchor.MiddleLeft;
        }

        // =====================================================================
        public static void Draw()
        {
            Styles();
            if (Island.InIsland) DrawIsland();
            else if (Game.IsScnGame) { if (Session.InRoom) DrawInGame(); }
            else if (!Island.Busy) DrawPortal();

            if (Island.Cover > 0.001f) DrawCover();
        }

        // ---------------- 메인 메뉴의 포탈 ----------------
        static void DrawPortal()
        {
            if (Updater.Current == Updater.State.ReadyToRestart)
                GUI.Label(new Rect(Screen.width - 420, Screen.height - 128, 400, 22), $"<color=#{HexOf(C_acc)}>멀티 v{Updater.Latest} 받음 · 재시작하면 적용</color>", Label(13, C_acc, FontStyle.Normal, TextAnchor.MiddleRight));
            var r = new Rect(Screen.width - 260, Screen.height - 100, 240, 76);
            if (GUI.Button(r, "", S_cardBtn)) Island.Enter();
            Orbit(new Vector2(r.x + 38, r.y + r.height / 2), 15, 7, 1.2f);
            GUI.Label(new Rect(r.x + 66, r.y + 12, 170, 28), "멀티 섬으로", Label(19, C_txt, FontStyle.Bold));
            GUI.Label(new Rect(r.x + 66, r.y + 40, 170, 22), "Ctrl + M", S_small);
        }

        // ---------------- 전환 화면 ----------------
        static void DrawCover()
        {
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, Island.Cover);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_bg);
            Orbit(new Vector2(Screen.width / 2f, Screen.height / 2f - 20), 34, 13, 2.2f);
            GUI.Label(new Rect(0, Screen.height / 2f + 34, Screen.width, 30), Island.CoverText, Label(17, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            GUI.color = old;
        }

        // 불/얼음 행성 공전 애니메이션
        static void Orbit(Vector2 c, float radius, float size, float speed)
        {
            float a = Time.unscaledTime * speed * Mathf.PI;
            bool swap = Mathf.FloorToInt(Time.unscaledTime * speed / 2f) % 2 == 1;
            var p1 = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Dot(swap ? p1 : c, size, C_fire);
            Dot(swap ? c : p1, size, C_ice);
        }
        static void Dot(Vector2 p, float r, Color col)
        {
            var old = GUI.color; GUI.color = new Color(col.r, col.g, col.b, old.a * col.a);
            GUI.DrawTexture(new Rect(p.x - r, p.y - r, r * 2, r * 2), T_circle);
            GUI.color = old;
        }

        // =====================================================================
        //  멀티 섬
        // =====================================================================
        static void DrawIsland()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !Island.Busy) { if (_confirmLeave) _confirmLeave = false; else AskLeave(); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F5 && Session.Phase == Phase.WaitingStart) { Session.ToggleReady(); e.Use(); }

            float W = Screen.width, H = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, W, H), T_bg);
            DrawIslandArt(new Rect(0, 0, W, H));

            // top bar
            GUI.DrawTexture(new Rect(0, 0, W, 48), T_side);
            Orbit(new Vector2(24, 24), 9, 6, 0.9f);
            GUI.Label(new Rect(48, 8, 400, 32), "A D O F A I   M U L T I", S_logo);
            if (GUI.Button(new Rect(W - 50, 6, 40, 36), "×", Label(24, C_txt, FontStyle.Normal, TextAnchor.MiddleCenter))) AskLeave();

            // sidebar
            GUI.DrawTexture(new Rect(0, 48, 56, H - 48), T_side);
            GUILayout.BeginArea(new Rect(7, 56, 42, H - 64));
            SideBtn(Tab.Home, "홈");
            SideBtn(Tab.Room, "방");
            SideBtn(Tab.Map, "맵");
            SideBtn(Tab.Results, "결과");
            SideBtn(Tab.Settings, "설정");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("나가기", S_side)) AskLeave();
            GUILayout.Space(30);
            GUILayout.EndArea();

            // footer
            GUI.DrawTexture(new Rect(56, H - 34, W - 56, 34), T_side);
            GUILayout.BeginArea(new Rect(70, H - 30, W - 80, 26));
            GUILayout.BeginHorizontal();
            GUILayout.Label("ADOFAI Multi " + Main.Version, S_small, GUILayout.ExpandWidth(false));
            GUILayout.Space(16);
            string st = Esc(string.Join("  ·  ", new[] { Net.Status, Session.Notice }.Where(x => !string.IsNullOrEmpty(x)).ToArray()));
            GUILayout.Label(st, S_small);
            GUILayout.FlexibleSpace();
            if (Session.Phase == Phase.WaitingStart) { GUILayout.Label("F5", S_chip, GUILayout.ExpandWidth(false)); GUILayout.Label("준비", S_small, GUILayout.ExpandWidth(false)); GUILayout.Space(10); }
            GUILayout.Label("Esc", S_chip, GUILayout.ExpandWidth(false)); GUILayout.Label("섬 떠나기", S_small, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // content
            GUILayout.BeginArea(new Rect(72, 60, W - 88, H - 104));
            switch (Page)
            {
                case Tab.Home: PageHome(); break;
                case Tab.Room: PageRoom(); break;
                case Tab.Map: PageMap(); break;
                case Tab.Results: PageResults(); break;
                case Tab.Settings: PageSettings(); break;
            }
            GUILayout.EndArea();

            if (_confirmLeave) DrawConfirm();
        }

        static void AskLeave()
        {
            if (Session.InRoom) _confirmLeave = true;
            else Island.Leave();
        }

        static void SideBtn(Tab t, string icon)
        {
            if (GUILayout.Button(icon, Page == t ? S_sideOn : S_side)) Page = t;
            GUILayout.Space(4);
        }

        // 배경: 섬 위 타일 길 + 공전하는 행성 (얼불춤 느낌)
        static void DrawIslandArt(Rect area)
        {
            float y = area.height - 120, tile = 34, gap = 6;
            int n = Mathf.CeilToInt(area.width / (tile + gap)) + 2;
            float speed = 1.6f; // beats/sec
            float beat = Time.unscaledTime * speed;
            int cur = Mathf.FloorToInt(beat);
            float scroll = (beat - cur) * 0f;
            var old = GUI.color;
            for (int i = 0; i < n; i++)
            {
                float x = 80 + i * (tile + gap) - scroll;
                float wave = Mathf.Sin(i * 0.55f) * 18;
                GUI.color = new Color(1, 1, 1, 0.07f);
                GUI.DrawTexture(new Rect(x, y + wave, tile, tile), T_card);
            }
            int idx = cur % n;
            float t = beat - cur;
            Vector2 C(int i) => new Vector2(80 + i * (tile + gap) + tile / 2f, y + Mathf.Sin(i * 0.55f) * 18 + tile / 2f);
            var a = C(idx); var b = C((idx + 1) % n);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) + Mathf.PI * (1 - t);
            var p = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Vector2.Distance(a, b);
            GUI.color = new Color(1, 1, 1, 0.35f);
            bool fireCenter = idx % 2 == 0;
            Dot(a, 9, fireCenter ? C_fire : C_ice);
            Dot(p, 9, fireCenter ? C_ice : C_fire);
            GUI.color = old;
        }

        // ---------------- 공통 위젯 ----------------
        static void Title(string t, string sub)
        {
            GUILayout.Label(t, S_h1);
            if (!string.IsNullOrEmpty(sub)) GUILayout.Label(sub, S_para);
            GUILayout.Space(8);
        }

        /// <summary>콘텐츠 영역 폭을 n등분한 카드 폭 (카드 margin 12 고려)</summary>
        static float Col(int n) => Mathf.Floor((Screen.width - 88f) / n) - 12f;

        static void StatCard(string num, string label, float width)
        {
            GUILayout.BeginVertical(S_card, GUILayout.Width(width), GUILayout.Height(84));
            GUILayout.Label(num, S_num);
            GUILayout.Label(label, S_small);
            GUILayout.EndVertical();
        }

        static void Bar(float p, float h)
        {
            var r = GUILayoutUtility.GetRect(100, h, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(r, T_line);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(p), r.height), T_acc);
        }

        // ---------------- 홈 ----------------
        static void PageHome()
        {
            Title("홈", "ADOFAI Multi — " + Main.Version + "   ·   " + StripTags(Updater.StatusText));
            if (!Session.InRoom)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(S_card, GUILayout.Width(Col(2)), GUILayout.Height(170));
                GUILayout.Label("방 만들기", S_h2);
                GUILayout.Label("내가 방장이 돼요. 맵을 고르면 모두에게 자동으로 전송돼요. 최대 8명.", S_para);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새 방 만들기", S_btnAcc)) { Net.CreateRoom(8); Page = Tab.Room; }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(S_card, GUILayout.Width(Col(2)), GUILayout.Height(170));
                GUILayout.Label("참가하기", S_h2);
                GUILayout.Label("Steam 친구 초대를 수락하면 자동으로 들어와요. 또는 로비 ID 입력:", S_para);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                _joinId = GUILayout.TextField(_joinId, S_field);
                if (GUILayout.Button("참가", S_btn, GUILayout.Width(90)) && ulong.TryParse(_joinId.Trim(), out var lid)) { Net.Join(lid); Page = Tab.Room; }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                StatCard(CacheCount().ToString(), "받아둔 맵", Col(2));
                StatCard(MapBrowser.Items.Count.ToString(), "내 컴퓨터의 맵", Col(2));
                GUILayout.EndHorizontal();
                MapBrowser.ScanOnce();
                return;
            }

            GUILayout.BeginHorizontal();
            StatCard($"{Session.Board.Count}/8", "참가자", Col(2));
            StatCard(PhaseText(), "진행 상태", Col(2));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(S_card);
            GUILayout.Label("맵", S_h2);
            GUILayout.Label(Session.MapHash != null ? $"{Esc(Session.MapTitle)}   <color=#{HexOf(C_sub)}>{Session.MapSize / 1048576f:0.0}MB</color>" : (Net.IsHost ? "맵 탭에서 맵을 골라 배포하세요." : "방장이 맵을 고르는 중..."), S_paraTxt);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(S_card);
            GUILayout.Label(Net.IsHost ? "방장" : "멤버", S_h2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("친구 초대", S_btn)) Net.InviteOverlay();
            if (Net.CurrentLobby.HasValue && GUILayout.Button("로비 ID 복사", S_btn)) GUIUtility.systemCopyBuffer = Net.CurrentLobby.Value.Id.Value.ToString();
            if (GUILayout.Button("방 나가기", S_btn)) _confirmLeave = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ---------------- 방 (참가자) ----------------
        static void PageRoom()
        {
            if (!Session.InRoom) { Title("방", "아직 방에 없어요. 홈에서 방을 만들거나 참가하세요."); return; }
            Title("방", $"참가자 {Session.Board.Count}/8   ·   {PhaseText()}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("친구 초대", S_btn, GUILayout.Width(140))) Net.InviteOverlay();
            if (Net.IsHost)
            {
                GUI.enabled = Session.Phase == Phase.Distributing;
                if (GUILayout.Button("받은 사람끼리 바로 시작", S_btn, GUILayout.Width(220))) Session.HostStartLoad();
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            _sPeers = GUILayout.BeginScrollView(_sPeers);
            var list = Session.Board;
            for (int i = 0; i < list.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                PeerCard(list[i]);
                if (i + 1 < list.Count) PeerCard(list[i + 1]);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        static void PeerCard(Entry p)
        {
            GUILayout.BeginVertical(S_card, GUILayout.Width(Col(2)), GUILayout.Height(92));
            string crown = p.Id == Net.HostId ? $"<color=#{HexOf(C_gold)}>방장</color>  " : "";
            string me = p.Id == Net.MyId ? $"  <color=#{HexOf(C_acc)}>(나)</color>" : "";
            GUILayout.Label($"{crown}{Trim(p.Name, 18)}{me}", S_h2);
            GUILayout.Label(StateText(p), S_sub);
            if (p.State == PState.Downloading || p.State == PState.Paused) Bar(p.Download, 6);
            GUILayout.EndVertical();
        }

        // ---------------- 맵 ----------------
        static void PageMap()
        {
            if (!Session.InRoom) { Title("맵", "방에 들어가면 사용할 수 있어요."); return; }
            if (!Net.IsHost) { MemberDownload(); return; }

            bool busy = Session.Phase == Phase.Packing || Session.InMatch;
            Title("맵", Session.MapHash != null ? $"현재: {Esc(Session.MapTitle)} ({Session.MapSize / 1048576f:0.0}MB)" : "배포할 맵을 고르세요");

            GUILayout.BeginHorizontal();
            _search = GUILayout.TextField(_search, S_field);
            if (GUILayout.Button(MapBrowser.Scanning ? "검색 중..." : "새로고침", S_btn, GUILayout.Width(120))) MapBrowser.Rescan();
            GUILayout.EndHorizontal();
            MapBrowser.ScanOnce();

            GUILayout.BeginVertical(S_card, GUILayout.ExpandHeight(true));
            _sMaps = GUILayout.BeginScrollView(_sMaps);
            var q = _search.Trim();
            int shown = 0;
            foreach (var it in MapBrowser.Items)
            {
                if (q.Length > 0 && it.Label.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0 && (it.Author ?? "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > 300) break;
                bool sel = it.Path == _mapPath;
                if (GUILayout.Button($"{Trim(it.Label, 64)}   <color=#{HexOf(C_sub)}><size=12>{Trim(it.Author, 24)}</size></color>", sel ? S_rowOn : S_row)) _mapPath = it.Path;
            }
            if (MapBrowser.Items.Count == 0 && !MapBrowser.Scanning) GUILayout.Label("맵을 못 찾았어요. 아래에 .adofai 경로를 직접 넣어주세요.", S_para);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            _mapPath = GUILayout.TextField(_mapPath, S_field);
            if (GUILayout.Button("최근 맵", S_btn, GUILayout.Width(100))) _mapPath = Game.DefaultLevelPath();
            GUI.enabled = !busy && !string.IsNullOrEmpty(_mapPath);
            if (GUILayout.Button("이 맵으로 배포", S_btnAcc, GUILayout.Width(180))) { Session.HostDistribute(_mapPath.Trim().Trim('"')); Page = Tab.Room; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        static void MemberDownload()
        {
            Title("맵", Session.MapHash != null ? $"{Esc(Session.MapTitle)} ({Session.MapSize / 1048576f:0.0}MB)" : "방장이 맵을 고르는 중...");
            if (Session.Phase != Phase.Distributing || Session.MapSize <= 0) return;
            GUILayout.BeginVertical(S_card);
            if (Session.DlCancelled)
            {
                GUILayout.Label("다운로드를 취소했어요 (이번 판은 관전)", S_txt);
                if (GUILayout.Button("다시 받기", S_btn, GUILayout.Width(140))) Session.DlRetry();
            }
            else if (!Session.DlActive) GUILayout.Label("맵 준비 완료! 다른 사람을 기다리는 중...", S_txt);
            else
            {
                float p = (float)Session.DlReceived / Session.MapSize;
                GUILayout.Label($"<color=#{HexOf(C_acc)}>{p * 100:0}%</color>   {Session.DlReceived / 1048576f:0.0} / {Session.MapSize / 1048576f:0.0}MB{(Session.DlPaused ? "   · 일시정지" : "")}", S_h2);
                Bar(p, 8);
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                if (Session.DlPaused) { if (GUILayout.Button("재개", S_btn, GUILayout.Width(120))) Session.DlResume(); }
                else if (GUILayout.Button("일시정지", S_btn, GUILayout.Width(120))) Session.DlPause();
                if (GUILayout.Button("취소", S_btn, GUILayout.Width(120))) Session.DlCancel();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        // ---------------- 결과 ----------------
        static void PageResults()
        {
            Title("결과", Session.Results.Count > 0 ? "정확도 → X정확도 → 미스 순" : "아직 결과가 없어요");
            if (Session.Results.Count == 0) return;
            GUILayout.BeginVertical(S_card, GUILayout.ExpandHeight(true));
            _sRes = GUILayout.BeginScrollView(_sRes);
            ResultsTable();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (Net.IsHost && GUILayout.Button("다음 판 — 맵 고르기", S_btnAcc, GUILayout.Width(260))) { Session.HostBackToLobby(); Page = Tab.Map; }
        }

        static void ResultsTable()
        {
            int rank = 1;
            foreach (var e in Session.Results)
            {
                var s = e.Stats;
                Color c = rank == 1 ? C_gold : rank == 2 ? Hex("d8dee0") : rank == 3 ? Hex("e2a06a") : C_txt;
                GUILayout.BeginHorizontal(GUILayout.Height(40));
                GUILayout.Label($"<color=#{HexOf(c)}><b>{rank}</b></color>", Label(24, c), GUILayout.Width(40));
                GUILayout.Label($"{Trim(e.Name, 14)}{(s.Progress < 0.999f ? $"  <color=#{HexOf(C_sub)}><size=12>기권 {s.Progress * 100:0}%</size></color>" : "")}", S_txt, GUILayout.Width(220));
                GUILayout.Label($"<color=#{HexOf(C_acc)}><b>{s.Acc * 100:0.00}%</b></color>", Label(18, C_acc), GUILayout.Width(110));
                GUILayout.Label($"X {s.XAcc * 100:0.00}%", S_sub, GUILayout.Width(100));
                GUILayout.Label($"<color=#{HexOf(C_fire)}>{s.TooEarly} {s.VeryEarly}</color> {s.EarlyPerfect} <color=#{HexOf(C_acc)}>{s.Perfect}</color> {s.LatePerfect} <color=#{HexOf(C_ice)}>{s.VeryLate} {s.TooLate}</color>", S_txt, GUILayout.Width(230));
                GUILayout.Label($"미스 {s.Miss + s.Overload}", S_sub);
                GUILayout.EndHorizontal();
                GUI.DrawTexture(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), T_line);
                rank++;
            }
        }

        // ---------------- 설정 ----------------
        static void PageSettings()
        {
            Title("설정", null);
            GUILayout.BeginVertical(S_card);
            GUILayout.Label("업데이트", S_h2);
            GUILayout.Label($"현재 v{Main.Version}   ·   {Updater.StatusText}", S_para);
            if (Updater.Current == Updater.State.Failed) GUILayout.Label(Updater.Error, S_small);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(S_card);
            GUILayout.Label("받은 맵 캐시", S_h2);
            GUILayout.Label($"{CacheCount()}개 · {CacheSizeMB():0.0}MB   ({MapPack.Root})", S_para);
            GUI.enabled = !Session.InMatch;
            if (GUILayout.Button("캐시 비우기", S_btn, GUILayout.Width(160))) { try { Directory.Delete(MapPack.Root, true); } catch { } }
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.BeginVertical(S_card);
            GUILayout.Label("단축키", S_h2);
            GUILayout.Label("Ctrl + M  멀티 섬 열기/떠나기   ·   F5  준비   ·   Ctrl + Shift + Q  기권", S_para);
            GUILayout.EndVertical();
        }

        static void DrawConfirm()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_dim);
            var r = new Rect((Screen.width - 440) / 2f, (Screen.height - 190) / 2f, 440, 190);
            GUILayout.BeginArea(r, S_card);
            GUILayout.Label("섬을 떠날까요?", S_h2);
            GUILayout.Label(Net.IsHost ? "방장이 떠나면 방이 사라져요." : "방에서 나가게 돼요. 다시 들어오려면 초대를 받아야 해요.", S_para);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("방만 나가기", S_btn)) { _confirmLeave = false; Session.LeaveRoom(); Page = Tab.Home; }
            if (GUILayout.Button("섬 떠나기", S_btnAcc)) { _confirmLeave = false; Session.LeaveRoom(); Island.Leave(); }
            if (GUILayout.Button("취소", S_btn)) _confirmLeave = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // =====================================================================
        //  레벨 안 (랭킹 · 카운트다운 · 결과)
        // =====================================================================
        static void DrawInGame()
        {
            var ph = Session.Phase;
            var board = Session.Board.Where(e => e.State >= PState.Loading && e.State != PState.Cancelled).ToList();
            float h = 40 + board.Count * 26, y = Screen.height * 0.22f;
            var box = new Rect(10, y, 300, h);
            GUI.color = new Color(1, 1, 1, 0.92f);
            GUI.Box(box, "", S_card);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 14, box.y + 8, 260, 22), "실시간 랭킹", Label(14, C_sub, FontStyle.Bold));
            int rank = 1;
            foreach (var e in board)
            {
                float ry = box.y + 32 + (rank - 1) * 26;
                bool me = e.Id == Net.MyId;
                GUI.Label(new Rect(box.x + 14, ry, 24, 24), rank.ToString(), Label(15, rank == 1 ? C_gold : C_sub, FontStyle.Bold));
                GUI.Label(new Rect(box.x + 36, ry, 120, 24), Trim(e.Name, 9) + (e.State == PState.Finished ? " (완주)" : ""), Label(15, me ? C_acc : C_txt));
                GUI.Label(new Rect(box.x + 150, ry, 80, 24), $"{e.Stats.Acc * 100:0.00}%", Label(15, me ? C_acc : C_txt, FontStyle.Bold, TextAnchor.MiddleRight));
                GUI.Label(new Rect(box.x + 236, ry, 54, 24), $"{e.Stats.Progress * 100:0}%", Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));
                rank++;
            }

            if (ph == Phase.WaitingStart)
                Banner(Session.MyReady ? "준비 완료 · 다른 사람을 기다리는 중" : "<color=#" + HexOf(C_acc) + ">F5</color> 를 눌러 준비");
            else if (ph == Phase.Countdown)
            {
                int n = Mathf.CeilToInt(Session.CountdownLeft);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 140), n > 0 ? n.ToString() : "GO!", S_big);
            }
            else if (ph == Phase.Results)
            {
                if (Session.Results.Count > 0)
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_dim);
                    var r = new Rect((Screen.width - 860) / 2f, (Screen.height - 460) / 2f, 860, 460);
                    GUILayout.BeginArea(r, S_card);
                    GUILayout.Label("결과", S_h1);
                    ResultsTable();
                    GUILayout.FlexibleSpace();
                    if (Net.IsHost) { if (GUILayout.Button("모두 멀티 섬으로", S_btnAcc)) Session.HostBackToLobby(); }
                    else GUILayout.Label("방장이 섬으로 돌아가면 같이 이동해요", S_sub);
                    GUILayout.EndArea();
                }
                else Banner("완주! 다른 사람을 기다리는 중...");
            }

            if (ph == Phase.Countdown || ph == Phase.Playing)
                GUI.Label(new Rect(Screen.width - 240, Screen.height - 30, 230, 24), "기권  Ctrl + Shift + Q", Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));
        }

        static void Banner(string text)
        {
            var r = new Rect((Screen.width - 520) / 2f, Screen.height * 0.1f, 520, 64);
            GUI.Box(r, "", S_card);
            GUI.Label(r, text, Label(22, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
        }

        // ---------------- 텍스트 ----------------
        static string PhaseText()
        {
            switch (Session.Phase)
            {
                case Phase.Lobby: return "대기";
                case Phase.Packing: return "압축 중";
                case Phase.Distributing: return "맵 배포";
                case Phase.Loading: return "로딩";
                case Phase.WaitingStart: return "준비";
                case Phase.Countdown: return "시작!";
                case Phase.Playing: return "플레이";
                case Phase.Results: return "결과";
            }
            return "-";
        }

        static string StateText(Entry e)
        {
            switch (e.State)
            {
                case PState.Lobby: return "대기";
                case PState.Downloading: return $"다운로드 {e.Download * 100:0}%";
                case PState.Paused: return $"일시정지 {e.Download * 100:0}%";
                case PState.Cancelled: return "다운 취소 (관전)";
                case PState.HasMap: return $"<color=#{HexOf(C_acc)}>맵 준비됨</color>";
                case PState.Loading: return "로딩 중";
                case PState.Loaded: return "레벨 입장 · 준비 안 됨";
                case PState.Ready: return $"<color=#{HexOf(C_acc)}>준비</color>";
                case PState.Playing: return $"플레이 중 {e.Stats.Progress * 100:0}%";
                case PState.Finished: return "완주";
            }
            return "";
        }

        static int CacheCount() { try { return Directory.Exists(MapPack.Root) ? Directory.GetDirectories(MapPack.Root).Length : 0; } catch { return 0; } }
        static float CacheSizeMB()
        {
            try { return Directory.Exists(MapPack.Root) ? Directory.GetFiles(MapPack.Root, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1048576f : 0; }
            catch { return 0; }
        }

        static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");
        static string Esc(string s) => (s ?? "").Replace("<", "\uFF1C").Replace(">", "\uFF1E");
        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : Esc(s.Length > n ? s.Substring(0, n) + "…" : s);
    }
}
