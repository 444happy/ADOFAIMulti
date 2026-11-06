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

        // ---- palette (현재 테마에서 복사) ----
        static Color C_bg, C_side, C_card, C_cardHi, C_line, C_acc, C_acc2, C_txt, C_sub, C_fire, C_ice, C_gold;
        static Theme _built;

        static Texture2D T_bg, T_side, T_card, T_cardHi, T_acc, T_line, T_fire, T_ice, T_white, T_circle, T_dim, T_chip, T_glow, T_grad, T_accBar, T_sideHi;
        static GUIStyle S_para, S_paraTxt, S_h1, S_h2, S_txt, S_sub, S_small, S_num, S_card, S_cardBtn, S_btn, S_btnAcc, S_side, S_sideOn, S_field, S_chip, S_big, S_row, S_rowOn, S_logo, S_section;
        static Font _font;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>둥근 모서리 + (선택) 1px 테두리 + (선택) 가로 그라데이션 텍스처 (9-slice)</summary>
        static Texture2D Round(Color c, int r = 10, Color? border = null, Color? c2 = null)
        {
            int n = r * 2 + 2;
            if (c2.HasValue) n = Mathf.Max(n, 64);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    var fill = c2.HasValue ? Color.Lerp(c, c2.Value, x / (float)(n - 1)) : c;
                    if (border.HasValue)
                    {
                        // 가장자리 1px 테두리
                        float edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                        bool inCorner = dx > 0 && dy > 0;
                        bool isBorder = inCorner ? d > r - 1.5f : edge < 1f;
                        if (isBorder) fill = Color.Lerp(fill, border.Value, border.Value.a);
                    }
                    t.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * a));
                }
            t.Apply(); t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t; }

        // 세로 그라데이션 배경 / 원형 은은한 빛
        static Texture2D VGrad(Color top, Color bottom)
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t;
        }
        static Texture2D Glow(Color c)
        {
            int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1 - d); a = a * a * 0.55f;
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t;
        }
        static Texture2D HGrad(Color a, Color b)
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++) t.SetPixel(x, 0, Color.Lerp(a, b, x / 63f));
            t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t;
        }

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
            string key = size + "|" + ColorUtility.ToHtmlStringRGBA(col) + "|" + (int)fs + "|" + (int)a + "|" + wrap;
            if (_labelCache.TryGetValue(key, out var cached)) return cached;
            var s = new GUIStyle(GUI.skin.label) { font = _font, fontSize = size, fontStyle = fs, alignment = a, richText = true, wordWrap = wrap, clipping = TextClipping.Clip };
            s.normal.textColor = col;
            _labelCache[key] = s;
            return s;
        }

        static GUIStyle Button(Texture2D n, Texture2D h, Color text, int size, int height, Color? hoverText = null)
        {
            var s = new GUIStyle { font = _font, fontSize = size, alignment = TextAnchor.MiddleCenter, richText = true, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(16, 16, 6, 6), margin = new RectOffset(4, 4, 4, 4), fixedHeight = height };
            s.normal.background = n; s.hover.background = h; s.active.background = h; s.focused.background = n;
            s.normal.textColor = s.focused.textColor = text;
            s.hover.textColor = s.active.textColor = hoverText ?? text;
            return s;
        }

        static void Styles()
        {
            if (S_h1 != null && _built == Theme.Current) return;
            var th = Theme.Current; _built = th;
            _labelCache.Clear();
            C_bg = th.Bg; C_side = th.Side; C_card = th.Card; C_cardHi = th.CardHi; C_line = th.Line; C_acc = th.Acc; C_acc2 = th.Acc2;
            C_txt = th.Txt; C_sub = th.Sub; C_fire = th.Fire; C_ice = th.Ice; C_gold = th.Gold;

            // 주의: Font.CreateDynamicFontFromOSFont 로 OS 글꼴을 만들면 Unity 6 에서
            // 같은 OS 글꼴을 쓰는 다른 모드(StutterFix 등)와 텍스트 캐시가 섞여 글자가 바뀌어 나옴.
            // → 게임 기본 IMGUI 글꼴(GUI.skin.font)을 그대로 쓴다.
            _font = null;

            var border = A(Color.white, 0.06f);
            T_bg = VGrad(th.BgTop, th.Bg); T_glow = Glow(th.Glow);
            T_side = Solid(A(th.Side, 0.96f)); T_line = Solid(C_line); T_white = Solid(Color.white);
            T_dim = Solid(new Color(0, 0, 0, 0.62f));
            T_card = Round(A(C_card, 0.94f), 12, border); T_cardHi = Round(C_cardHi, 12, A(C_acc, 0.25f));
            T_acc = Round(C_acc, 12, null, C_acc2); T_grad = HGrad(C_fire, C_ice); T_accBar = HGrad(C_acc, C_acc2);
            T_fire = Round(C_fire, 4); T_ice = Round(C_ice, 4); T_chip = Round(th.CardHi, 6, border);
            T_circle = Round(Color.white, 32);
            T_sideHi = Round(A(C_acc, 0.14f), 10);

            S_h1 = Label(30, C_txt, FontStyle.Bold);
            S_h2 = Label(18, C_txt, FontStyle.Bold);
            S_txt = Label(16, C_txt);
            S_sub = Label(14, C_sub);
            S_para = Label(14, C_sub, FontStyle.Normal, TextAnchor.UpperLeft, true);
            S_paraTxt = Label(16, C_txt, FontStyle.Normal, TextAnchor.UpperLeft, true);
            S_small = Label(13, C_sub);
            S_section = Label(12, A(C_sub, 0.9f), FontStyle.Bold);
            S_num = Label(32, C_acc, FontStyle.Bold);
            S_big = Label(80, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            S_logo = Label(17, C_txt, FontStyle.Bold);
            S_card = Box(T_card, 12, new RectOffset(18, 18, 16, 16));
            S_cardBtn = Button(T_card, T_cardHi, C_txt, 16, 0); S_cardBtn.alignment = TextAnchor.UpperLeft; S_cardBtn.padding = new RectOffset(18, 18, 16, 16); S_cardBtn.margin = new RectOffset(6, 6, 6, 6);
            S_btn = Button(Round(C_cardHi, 10, border), Round(Color.Lerp(C_cardHi, C_acc, 0.18f), 10, A(C_acc, 0.4f)), C_txt, 15, 40);
            S_btnAcc = Button(T_acc, Round(Color.Lerp(C_acc, Color.white, 0.18f), 12, null, Color.Lerp(C_acc2, Color.white, 0.18f)), th.AccText, 16, 46); S_btnAcc.fontStyle = FontStyle.Bold;
            S_side = Button(null, Round(A(C_acc, 0.07f), 10), C_sub, 12, 46, C_txt); S_side.padding = new RectOffset(0, 0, 0, 0); S_side.fixedWidth = 46;
            S_sideOn = Button(T_sideHi, T_sideHi, C_acc, 12, 46); S_sideOn.fontStyle = FontStyle.Bold; S_sideOn.padding = new RectOffset(0, 0, 0, 0); S_sideOn.fixedWidth = 46;
            S_field = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = 15, fixedHeight = 40, padding = new RectOffset(14, 14, 10, 10), border = new RectOffset(10, 10, 10, 10), margin = new RectOffset(4, 4, 4, 4) };
            S_field.normal.background = Round(th.Field, 10, border); S_field.focused.background = Round(th.Field, 10, A(C_acc, 0.6f)); S_field.hover.background = Round(th.Field, 10, A(Color.white, 0.12f));
            S_field.normal.textColor = S_field.focused.textColor = S_field.hover.textColor = C_txt;
            S_chip = Box(T_chip, 6, new RectOffset(8, 8, 3, 3)); S_chip.font = _font; S_chip.fontSize = 12; S_chip.fontStyle = FontStyle.Bold; S_chip.normal.textColor = C_txt; S_chip.alignment = TextAnchor.MiddleCenter; S_chip.margin = new RectOffset(4, 4, 2, 0);
            S_row = Button(null, Round(A(C_acc, 0.06f), 8), C_txt, 15, 36); S_row.alignment = TextAnchor.MiddleLeft; S_row.border = new RectOffset(8, 8, 8, 8);
            S_rowOn = Button(Round(A(C_acc, 0.16f), 8, A(C_acc, 0.5f)), Round(A(C_acc, 0.16f), 8, A(C_acc, 0.5f)), C_acc, 15, 36); S_rowOn.alignment = TextAnchor.MiddleLeft; S_rowOn.border = new RectOffset(8, 8, 8, 8);
        }

        static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _swatches = new System.Collections.Generic.Dictionary<Color, Texture2D>();
        static Texture2D Swatch(Color c)
        {
            if (!_swatches.TryGetValue(c, out var t)) _swatches[c] = t = Round(c, 6, A(Color.white, 0.15f));
            return t;
        }

        /// <summary>작은 대문자 섹션 머리말</summary>
        static void Section(string t) { GUILayout.Space(4); GUILayout.Label(t, S_section); }

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
            // 배경: 세로 그라데이션 + 은은한 빛 두 개 + 섬 그림
            GUI.DrawTexture(new Rect(0, 0, W, H), T_bg);
            float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 0.6f) * 0.15f;
            var oc = GUI.color;
            GUI.color = A(Color.white, pulse);
            GUI.DrawTexture(new Rect(W * 0.55f, -H * 0.35f, W * 0.8f, W * 0.8f), T_glow);
            GUI.color = A(Color.white, 0.5f * pulse);
            GUI.DrawTexture(new Rect(-W * 0.2f, H * 0.45f, W * 0.6f, W * 0.6f), T_glow);
            GUI.color = oc;
            DrawIslandArt(new Rect(0, 0, W, H));

            // 상단 바 + 불→얼음 그라데이션 선
            GUI.DrawTexture(new Rect(0, 0, W, 52), T_side);
            GUI.DrawTexture(new Rect(0, 51, W, 2), T_grad);
            Orbit(new Vector2(32, 26), 10, 6, 0.9f);
            GUI.Label(new Rect(58, 10, 300, 32), "ADOFAI <color=#" + HexOf(C_acc) + ">MULTI</color>", S_logo);
            if (Session.InRoom)
            {
                string badge = (Net.IsHost ? "방장" : "멤버") + "  ·  " + Session.Board.Count + "/8  ·  " + PhaseText();
                GUI.Label(new Rect(200, 14, 400, 24), badge, S_small);
            }
            if (GUI.Button(new Rect(W - 54, 8, 44, 36), "×", Label(26, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter))) AskLeave();

            // 사이드바 (활성 탭 왼쪽에 강조 막대)
            GUI.DrawTexture(new Rect(0, 53, 64, H - 53), T_side);
            GUI.DrawTexture(new Rect(63, 53, 1, H - 53), T_line);
            GUILayout.BeginArea(new Rect(9, 64, 46, H - 72));
            SideBtn(Tab.Home, "홈");
            SideBtn(Tab.Room, "방");
            SideBtn(Tab.Map, "맵");
            SideBtn(Tab.Results, "결과");
            SideBtn(Tab.Settings, "설정");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("나가기", S_side)) AskLeave();
            GUILayout.Space(36);
            GUILayout.EndArea();

            // 하단 바
            GUI.DrawTexture(new Rect(64, H - 36, W - 64, 36), T_side);
            GUI.DrawTexture(new Rect(64, H - 36, W - 64, 1), T_line);
            GUILayout.BeginArea(new Rect(80, H - 31, W - 92, 28));
            GUILayout.BeginHorizontal();
            GUILayout.Label("ADOFAI Multi " + Main.Version, S_small, GUILayout.ExpandWidth(false));
            GUILayout.Space(16);
            string st = Esc(string.Join("  ·  ", new[] { Net.Status, Session.Notice }.Where(x => !string.IsNullOrEmpty(x)).ToArray()));
            GUILayout.Label(st, S_small);
            GUILayout.FlexibleSpace();
            if (Session.Phase == Phase.WaitingStart) { GUILayout.Label("F5", S_chip, GUILayout.ExpandWidth(false)); GUILayout.Label("준비", S_small, GUILayout.ExpandWidth(false)); GUILayout.Space(12); }
            GUILayout.Label("Esc", S_chip, GUILayout.ExpandWidth(false)); GUILayout.Label("섬 떠나기", S_small, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // content
            GUILayout.BeginArea(new Rect(80, 68, W - 96, H - 112));
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
            bool on = Page == t;
            if (GUILayout.Button(icon, on ? S_sideOn : S_side)) Page = t;
            if (on && Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                GUI.DrawTexture(new Rect(r.x - 9, r.y + 10, 3, r.height - 20), T_accBar);
            }
            GUILayout.Space(6);
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
            GUILayout.Label(t, S_h1, GUILayout.Height(40));
            if (Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                GUI.DrawTexture(new Rect(r.x + 2, r.yMax - 2, 36, 3), T_accBar);
            }
            if (!string.IsNullOrEmpty(sub)) { GUILayout.Space(4); GUILayout.Label(sub, S_para); }
            GUILayout.Space(10);
        }

        /// <summary>콘텐츠 영역 폭을 n등분한 카드 폭 (카드 margin 12 고려)</summary>
        static float Col(int n) => Mathf.Floor((Screen.width - 96f) / n) - 12f;

        static void StatCard(string num, string label, float width)
        {
            GUILayout.BeginVertical(S_card, GUILayout.Width(width), GUILayout.Height(84));
            GUILayout.Label(label, S_section);
            GUILayout.Label(num, S_num);
            GUILayout.EndVertical();
        }

        static void Bar(float p, float h)
        {
            var r = GUILayoutUtility.GetRect(100, h, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(r, T_line);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(p), r.height), T_accBar);
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

            // ---- 테마 ----
            GUILayout.BeginVertical(S_card);
            GUILayout.Label("테마", S_h2);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            foreach (var th in Theme.All)
            {
                bool on = th == Theme.Current;
                var r = GUILayoutUtility.GetRect(150, 74, GUILayout.Width(150), GUILayout.Height(74));
                if (GUI.Button(r, "", on ? S_rowOn : S_row)) Theme.Set(th);
                if (Event.current.type == EventType.Repaint)
                {
                    // 미리보기: 배경/카드/강조색 스와치
                    GUI.DrawTexture(new Rect(r.x + 12, r.y + 12, 26, 26), Swatch(th.Bg));
                    GUI.DrawTexture(new Rect(r.x + 42, r.y + 12, 26, 26), Swatch(th.Card));
                    GUI.DrawTexture(new Rect(r.x + 72, r.y + 12, 26, 26), Swatch(th.Acc));
                    GUI.DrawTexture(new Rect(r.x + 102, r.y + 12, 26, 26), Swatch(th.Acc2));
                }
                GUI.Label(new Rect(r.x + 12, r.y + 44, r.width - 20, 22), th.Name, Label(13, on ? C_acc : C_txt, on ? FontStyle.Bold : FontStyle.Normal));
                GUILayout.Space(8);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

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
