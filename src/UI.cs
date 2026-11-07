using System.IO;
using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// 멀티 섬 UI — 멀티의 흐름(맵 선택 → 맵 받기 → 준비 → 플레이 → 결과)을
    /// 한 화면에서 보여주는 "로비 스테이지" 구성. 왼쪽 라인업(8슬롯), 오른쪽 스테이지,
    /// 방장의 맵 서랍, 결과 시상대, 설정 팝오버.
    /// </summary>
    public static partial class UI
    {
        public static bool ShowResults;

        // ---- palette (현재 테마에서 복사) ----
        static Color C_bg, C_side, C_card, C_cardHi, C_line, C_acc, C_acc2, C_txt, C_sub, C_fire, C_ice, C_gold;
        static int _builtVer = -1; static float _builtAt;
        static readonly System.Collections.Generic.List<Texture2D> _owned = new System.Collections.Generic.List<Texture2D>();
        static Texture2D Own(Texture2D t) { t.hideFlags = HideFlags.HideAndDontSave; _owned.Add(t); return t; }

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
            t.Apply();
            return Own(t);
        }
        static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return Own(t); }

        // 세로 그라데이션 배경 / 원형 은은한 빛
        static Texture2D VGrad(Color top, Color bottom)
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            t.Apply(); return Own(t);
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
            t.Apply(); return Own(t);
        }
        static Texture2D HGrad(Color a, Color b)
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++) t.SetPixel(x, 0, Color.Lerp(a, b, x / 63f));
            t.Apply(); return Own(t);
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
            if (S_h1 != null && _builtVer == Theme.Version) return;
            // 같은 프레임의 Layout/Repaint 사이에 텍스처가 바뀌지 않도록 Layout 이벤트에서만 다시 만든다
            if (S_h1 != null && Event.current != null && Event.current.type != EventType.Layout) return;
            // 커스텀 슬라이더를 끄는 동안 매 프레임 다시 만들지 않게 (최대 초당 ~12번)
            if (S_h1 != null && Time.unscaledTime - _builtAt < 0.08f) return;
            var th = Theme.Current; _builtVer = Theme.Version; _builtAt = Time.unscaledTime;
            foreach (var old in _owned) if (old != null) Object.Destroy(old);
            _owned.Clear(); _swatches.Clear(); _pillTex.Clear();
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
        //  진입점
        // =====================================================================
        public static void Draw()
        {
            Styles();
            if (S_cta == null || _extraVer != _builtVer) ExtraStyles();
            if (Island.InIsland) DrawIsland();
            else if (Game.IsScnGame) { if (Session.InRoom && !DrawInGameLayout()) DrawInGame(); }
            else if (!Island.Busy) DrawPortal();

            if (Island.Cover > 0.001f) DrawCover();
        }

        static GUIStyle S_cta40, S_field40, S_cardMe, S_cta, S_ctaGhost, S_pill, S_title, S_stepOn, S_stepOff, S_empty, S_iconBtn;
        static int _extraVer = -1;

        static void ExtraStyles()
        {
            _extraVer = _builtVer;
            S_cardMe = new GUIStyle(S_card); S_cardMe.normal.background = T_cardHi;
            S_cta = new GUIStyle(S_btnAcc) { fontSize = 19, fixedHeight = 58 };
            S_cta40 = new GUIStyle(S_cta) { fontSize = 17, fixedHeight = 40, padding = new RectOffset(12, 12, 4, 4) };
            S_field40 = new GUIStyle(S_field) { fixedHeight = 40, fontSize = 20, fontStyle = FontStyle.Bold };
            S_ctaGhost = new GUIStyle(S_btn) { fontSize = 17, fixedHeight = 58 };
            S_pill = new GUIStyle(Label(12, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter)) { padding = new RectOffset(9, 9, 2, 2), border = new RectOffset(9, 9, 9, 9) };
            S_title = Label(38, C_txt, FontStyle.Bold);
            S_stepOn = Label(13, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter);
            S_stepOff = Label(13, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter);
            S_empty = Button(Round(A(C_card, 0.35f), 12, A(Color.white, 0.05f)), Round(A(C_card, 0.6f), 12, A(C_acc, 0.35f)), A(C_sub, 0.7f), 14, 0);
            S_empty.alignment = TextAnchor.MiddleLeft; S_empty.padding = new RectOffset(20, 16, 0, 0); S_empty.margin = new RectOffset(0, 0, 0, 0);
            S_iconBtn = new GUIStyle(S_btn) { fixedHeight = 36, fontSize = 14, padding = new RectOffset(14, 14, 4, 4) };
        }

        static bool _drawer, _settings, _confirmLeave, _profile;
        static string _nickEdit = "";
        static string _joinId = "", _mapPath = "", _search = "";
        static Vector2 _sMaps, _sRes;

        // 플레이어 행성 색 (슬롯 순서대로)
        static Color SlotColor(int i)
        {
            switch (i % 8)
            {
                case 0: return C_fire; case 1: return C_ice; case 2: return C_gold; case 3: return Hex("7ee08a");
                case 4: return Hex("ff8fd0"); case 5: return Hex("b69cff"); case 6: return Hex("ffa94d"); default: return Hex("5fe0d6");
            }
        }

        // =====================================================================
        //  메인 메뉴 포탈 · 전환 화면
        // =====================================================================
        static void DrawPortal()
        {
            // 메인 섬의 멀티 포탈 위에 표지판
            if (PortalTile.ScreenPos(out var p))
            {
                float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 4f;
                var r = new Rect(p.x - 70, p.y - 92 + bob, 140, 46);
                GUI.Box(r, "", S_card);
                Orbit(new Vector2(r.x + 22, r.y + r.height / 2f), 7, 4, 1.4f);
                GUI.Label(new Rect(r.x + 36, r.y + 4, r.width - 40, 22), "멀티", Label(16, C_txt, FontStyle.Bold));
                GUI.Label(new Rect(r.x + 36, r.y + 23, r.width - 40, 18), Updater.UpdateRequired ? "업데이트 필요 · 재시작" : "포탈로 이동", Label(11, Updater.UpdateRequired ? C_gold : C_sub));
                Fill(new Rect(p.x - 1, r.yMax, 2, 18 - bob), A(C_acc, 0.6f));
                return;
            }

            // 포탈을 놓을 자리를 못 찾았을 때만 대체 버튼
            if (PortalTile.Failed && Game.IsMainMenu)
            {
                var r = new Rect(Screen.width - 270, Screen.height - 104, 250, 80);
                if (GUI.Button(r, "", S_cardBtn)) Island.Enter();
                Orbit(new Vector2(r.x + 42, r.y + r.height / 2), 16, 8, 1.2f);
                GUI.Label(new Rect(r.x + 74, r.y + 14, 170, 28), "멀티 섬으로", Label(19, C_txt, FontStyle.Bold));
                GUI.Label(new Rect(r.x + 74, r.y + 42, 170, 22), "친구와 같은 맵으로 대결", S_small);
            }
        }

        static void DrawCover()
        {
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, Island.Cover);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_bg);
            Orbit(new Vector2(Screen.width / 2f, Screen.height / 2f - 20), 36, 14, 2.2f);
            GUI.Label(new Rect(0, Screen.height / 2f + 38, Screen.width, 30), Island.CoverText, Label(17, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            GUI.color = old;
        }

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
        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, T_white); GUI.color = o; }

        // =====================================================================
        //  멀티 섬 (로비 스테이지)
        // =====================================================================
        static void DrawIsland()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !Island.Busy)
            {
                if (_confirmLeave) _confirmLeave = false;
                else if (_settings) _settings = false;
                else if (_profile) _profile = false;
                else if (_drawer) _drawer = false;
                else if (ShowResults && Session.Results.Count > 0 && Session.Phase != Phase.Results) ShowResults = false;
                else AskLeave();
                e.Use();
            }

            float W = Screen.width, H = Screen.height;
            if (!(!Session.InRoom && Updater.UpdateRequired) && DrawIslandLayout())
            {
                if (_settings) DrawSettings(W, H);
                if (_profile) DrawProfile(W, H);
                if (_confirmLeave) DrawConfirm();
                return;
            }
            DrawBackdrop(W, H);
            DrawTopBar(W);

            var body = new Rect(28, 84, W - 56, H - 84 - 24);
            if (!Session.InRoom && Updater.UpdateRequired) DrawUpdateRequired(body);
            else if (!Session.InRoom) DrawEntrance(body);
            else
            {
                float lw = Mathf.Clamp(W * 0.27f, 300, 400);
                DrawLineup(new Rect(body.x, body.y, lw, body.height));
                var stage = new Rect(body.x + lw + 24, body.y, body.width - lw - 24, body.height);
                if (ShowResults && Session.Results.Count > 0) DrawPodium(stage);
                else DrawStage(stage);
            }

            if (_drawer) DrawMapDrawer(W, H);
            if (_settings) DrawSettings(W, H);
            if (_profile) DrawProfile(W, H);
            if (_confirmLeave) DrawConfirm();
        }

        static void DrawBackdrop(float W, float H)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), T_bg);
            float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 0.6f) * 0.15f;
            var oc = GUI.color;
            GUI.color = A(Color.white, pulse);
            GUI.DrawTexture(new Rect(W * 0.5f, -H * 0.4f, W * 0.8f, W * 0.8f), T_glow);
            GUI.color = A(Color.white, 0.45f * pulse);
            GUI.DrawTexture(new Rect(-W * 0.25f, H * 0.5f, W * 0.6f, W * 0.6f), T_glow);
            GUI.color = oc;
            DrawTilePath(W, H);
        }

        // 화면 아래를 지나는 타일 길 + 박자에 맞춰 굴러가는 두 행성
        static void DrawTilePath(float W, float H)
        {
            float y = H - 70, tile = 30, gap = 6;
            int n = Mathf.CeilToInt(W / (tile + gap)) + 1;
            float beat = Time.unscaledTime * 1.6f;
            int cur = Mathf.FloorToInt(beat);
            var old = GUI.color;
            GUI.color = A(Color.white, 0.06f);
            for (int i = 0; i < n; i++) GUI.DrawTexture(new Rect(i * (tile + gap), y + Mathf.Sin(i * 0.5f) * 14, tile, tile), T_card);
            Vector2 C(int i) => new Vector2(i * (tile + gap) + tile / 2f, y + Mathf.Sin(i * 0.5f) * 14 + tile / 2f);
            int idx = cur % n; float t = beat - cur;
            var a = C(idx); var b = C((idx + 1) % n);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) + Mathf.PI * (1 - t);
            var p = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Vector2.Distance(a, b);
            GUI.color = A(Color.white, 0.3f);
            bool fire = idx % 2 == 0;
            Dot(a, 8, fire ? C_fire : C_ice); Dot(p, 8, fire ? C_ice : C_fire);
            GUI.color = old;
        }

        static void DrawTopBar(float W)
        {
            Orbit(new Vector2(46, 42), 11, 7, 0.9f);
            GUI.Label(new Rect(70, 26, 260, 32), "ADOFAI <color=#" + HexOf(C_acc) + ">MULTI</color>", Label(20, C_txt, FontStyle.Bold));

            // 오른쪽 버튼들
            float x = W - 28;
            x -= 96; if (GUI.Button(new Rect(x, 24, 96, 36), "나가기", S_iconBtn)) AskLeave();
            x -= 104; if (GUI.Button(new Rect(x, 24, 96, 36), "설정", S_iconBtn)) { _settings = !_settings; _profile = false; }
            x -= 104; if (GUI.Button(new Rect(x, 24, 96, 36), "프로필", _profile ? S_rowOn : S_iconBtn)) { _profile = !_profile; _settings = false; _nickEdit = Profile.Nick; }
            if (Session.InRoom)
            {
                if (Net.CanInvite) { x -= 116; if (GUI.Button(new Rect(x, 24, 108, 36), "Steam 초대", S_iconBtn)) Net.InviteOverlay(); }
                if (Net.IsHost)
                {
                    x -= 104;
                    if (GUI.Button(new Rect(x, 24, 96, 36), Net.RoomPublic ? "공개" : "비공개", Net.RoomPublic ? S_rowOn : S_iconBtn))
                    { Net.SetPublic(!Net.RoomPublic); Session.Notice = Net.RoomPublic ? "공개 방이 됐어요 — 목록에 표시돼요" : "비공개 방이 됐어요 — 코드로만 들어와요"; }
                }
                if (!string.IsNullOrEmpty(Net.RoomCode))
                {
                    x -= 196;
                    if (GUI.Button(new Rect(x, 24, 188, 36), "방 코드  <b>" + Net.RoomCode + "</b>  복사", S_iconBtn))
                    { GUIUtility.systemCopyBuffer = Net.RoomCode; Session.Notice = "방 코드를 복사했어요"; }
                }
            }
            GUI.DrawTexture(new Rect(28, 74, W - 56, 1), T_line);

            // 아래 상태 줄
            string upd = Updater.Current == Updater.State.ReadyToRestart ? $"멀티 v{Updater.Latest} 받음 · 재시작하면 적용" : Updater.Current == Updater.State.Downloading ? $"v{Updater.Latest} 받는 중" : "";
            string st = Esc(string.Join("   ·   ", new[] { Net.Status, Session.Notice, upd }.Where(s => !string.IsNullOrEmpty(s)).ToArray()));
            GUI.Label(new Rect(28, Screen.height - 26, W - 300, 22), st, S_small);
            GUI.Label(new Rect(W - 300, Screen.height - 26, 272, 22), "v" + Main.Version + "   ·   Esc 뒤로", Label(12, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleRight));
        }

        // 맵 → 배포 → 준비 → 플레이 → 결과
        static readonly string[] Steps = { "맵 선택", "맵 받기", "준비", "플레이", "결과" };
        static int CurrentStep()
        {
            switch (Session.Phase)
            {
                case Phase.Packing: case Phase.Distributing: return 1;
                case Phase.Loading: case Phase.WaitingStart: case Phase.Countdown: return 2;
                case Phase.Playing: return 3;
                case Phase.Results: return 4;
            }
            return 0;
        }

        static void DrawStepper(Rect r)
        {
            int cur = CurrentStep();
            float seg = r.width / Steps.Length;
            for (int i = 0; i < Steps.Length; i++)
            {
                float cx = r.x + seg * i + seg / 2f;
                if (i < Steps.Length - 1)
                    Fill(new Rect(cx + 12, r.y + 11, seg - 24, 2), i < cur ? C_acc : C_line);
                Color dc = i < cur ? C_acc : i == cur ? C_acc : C_line;
                Dot(new Vector2(cx, r.y + 12), i == cur ? 8 : 6, dc);
                if (i == cur) { var o = GUI.color; GUI.color = A(Color.white, 0.25f + Mathf.PingPong(Time.unscaledTime, 0.5f) * 0.3f); Dot(new Vector2(cx, r.y + 12), 13, C_acc); GUI.color = o; }
                GUI.Label(new Rect(cx - seg / 2f, r.y + 22, seg, 18), Steps[i], i == cur ? S_stepOn : S_stepOff);
            }
        }

        // ---------------- 구버전: 이용 막기 ----------------
        static void DrawUpdateRequired(Rect b)
        {
            float cx = b.x + b.width / 2f;
            Orbit(new Vector2(cx, b.y + 110), 46, 18, 1.1f);
            GUI.Label(new Rect(b.x, b.y + 170, b.width, 50), "새 버전이 나왔어요", Label(36, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
            string sub = Updater.Current == Updater.State.ReadyToRestart
                ? $"v{Updater.Latest} 를 받아뒀어요. 게임을 재시작하면 적용되고, 그때부터 멀티를 쓸 수 있어요."
                : Updater.Current == Updater.State.Downloading ? $"v{Updater.Latest} 받는 중..."
                : $"현재 v{Main.Version} → 최신 v{Updater.Latest}. 업데이트를 받은 뒤 재시작해 주세요.";
            GUI.Label(new Rect(b.x, b.y + 220, b.width, 28), sub, Label(16, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(b.x, b.y + 250, b.width, 24), "버전이 다르면 친구 방에 들어갈 수 없어서, 업데이트 전에는 방 만들기·참가를 막아뒀어요.", Label(13, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter));
            if (Updater.Current == Updater.State.Failed)
            {
                var br = new Rect(cx - 140, b.y + 300, 280, 58);
                if (GUI.Button(br, "다시 받기", S_cta) && Runner.Instance != null) Updater.CheckNow(Runner.Instance);
                GUI.Label(new Rect(b.x, br.yMax + 10, b.width, 22), Esc(Updater.Error), Label(12, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter));
            }
        }

        // ---------------- 방 밖: 입구 (공개 방 목록 + 방 만들기/코드로 참가) ----------------
        static string _roomName = "";
        static bool _newPublic = true;
        static Vector2 _sRooms;

        static void DrawEntrance(Rect b)
        {
            RoomList.Tick(Runner.Instance);
            float rw = Mathf.Clamp(b.width * 0.36f, 340, 460);
            var left = new Rect(b.x, b.y, b.width - rw - 24, b.height);
            var right = new Rect(left.xMax + 24, b.y, rw, b.height);

            // ---- 왼쪽: 공개 방 목록 ----
            GUI.Label(new Rect(left.x + 4, left.y, 300, 22), "공개 방", S_section);
            string info = !string.IsNullOrEmpty(RoomList.Error) ? RoomList.Error : !RoomList.Loaded ? "불러오는 중..." : $"{RoomList.Rooms.Count}개";
            GUI.Label(new Rect(left.x, left.y, left.width - 110, 22), info, Label(13, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));
            if (GUI.Button(new Rect(left.xMax - 100, left.y - 6, 100, 32), RoomList.Loading ? "..." : "새로고침", S_iconBtn)) RoomList.RefreshNow();

            var listArea = new Rect(left.x, left.y + 32, left.width, left.height - 32);
            GUILayout.BeginArea(listArea);
            _sRooms = GUILayout.BeginScrollView(_sRooms);
            if (RoomList.Loaded && RoomList.Rooms.Count == 0)
            {
                GUILayout.Space(60);
                GUILayout.Label("지금 열린 공개 방이 없어요", Label(20, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUILayout.Label("오른쪽에서 방을 만들어 보세요", Label(14, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            foreach (var rm in RoomList.Rooms) DrawRoomCard(rm);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- 오른쪽: 방 만들기 / 코드로 참가 (한 줄씩 압축) ----
            GUILayout.BeginArea(right);
            GUILayout.BeginVertical(S_card);
            GUILayout.Label("방 만들기", S_h2);
            GUILayout.Space(4);
            SearchField(ref _roomName, Net.MyName + "의 방");
            if (_roomName.Length > 40) _roomName = _roomName.Substring(0, 40);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("공개", _newPublic ? S_rowOn : S_row, GUILayout.Height(40), GUILayout.Width(84))) _newPublic = true;
            if (GUILayout.Button("비공개", !_newPublic ? S_rowOn : S_row, GUILayout.Height(40), GUILayout.Width(96))) _newPublic = false;
            GUILayout.Space(6);
            if (GUILayout.Button("방 만들기", S_cta40, GUILayout.ExpandWidth(true))) Net.CreateRoom(_newPublic, _roomName);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.BeginVertical(S_card);
            GUILayout.Label("코드로 참가", S_h2);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            _joinId = Net.NormalizeCode(GUILayout.TextField(_joinId, 8, S_field40));
            if (_joinId.Length == 0 && Event.current.type == EventType.Repaint)
            {
                var lr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(lr.x + 16, lr.y, lr.width, lr.height), "방 코드 6자리", Label(15, A(C_sub, 0.6f)));
            }
            GUI.enabled = Net.IsValidCode(_joinId);
            if (GUILayout.Button("참가", S_cta40, GUILayout.Width(110))) Net.Join(_joinId);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            int cached = CacheCount();
            if (cached > 0) { GUILayout.Space(8); GUILayout.Label($"받아둔 맵 {cached}개 · {CacheSizeMB():0}MB", Label(12, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter)); }
            GUILayout.EndArea();
        }

        static void DrawRoomCard(RoomList.Room rm)
        {
            var r = GUILayoutUtility.GetRect(100, 86, GUILayout.ExpandWidth(true));
            r = new Rect(r.x, r.y, r.width - 4, r.height - 8);
            bool canJoin = rm.SameVersion && !rm.Full && !rm.InGame;
            if (GUI.Button(r, "", canJoin ? S_cardBtn : S_card) && canJoin) Net.Join(rm.Code);

            // 왼쪽: 인원 원형
            var cr = new Rect(r.x + 18, r.y + (r.height - 50) / 2f, 50, 50);
            GUI.DrawTexture(cr, Swatch(A(canJoin ? C_acc : C_sub, 0.14f)));
            GUI.Label(cr, $"{rm.Count}<size=12>/{rm.Max}</size>", Label(17, canJoin ? C_acc : C_sub, FontStyle.Bold, TextAnchor.MiddleCenter));

            float tx = cr.xMax + 16, tw = r.xMax - tx - 150;
            GUI.Label(new Rect(tx, r.y + 12, tw, 26), Trim(rm.Name, 34), Label(17, C_txt, FontStyle.Bold));
            string song = string.IsNullOrEmpty(rm.Song) ? "맵 고르는 중" : Trim(rm.Song, 30) + (string.IsNullOrEmpty(rm.Artist) ? "" : "  ·  " + Trim(rm.Artist, 18));
            GUI.Label(new Rect(tx, r.y + 40, tw, 20), $"방장 {Trim(rm.Host, 14)}   ·   {song}", S_small);

            // 오른쪽: 상태
            string st; Color sc;
            if (!rm.SameVersion) { st = "버전 다름 v" + Trim(rm.Ver, 10); sc = C_sub; }
            else if (rm.Full) { st = "가득 참"; sc = C_sub; }
            else if (rm.InGame) { st = rm.Phase == "results" ? "결과 보는 중" : "게임 중"; sc = C_gold; }
            else if (rm.Phase == "distributing") { st = "맵 받는 중 · 입장"; sc = C_acc; }
            else { st = "입장"; sc = C_acc; }
            float w = 22 + Label(12, sc, FontStyle.Bold).CalcSize(new GUIContent(st)).x;
            Pill(new Rect(r.xMax - w - 18, r.y + (r.height - 26) / 2f, w, 26), Esc(st), sc);
        }

        // ---------------- 왼쪽: 라인업 (8슬롯) ----------------
        static void DrawLineup(Rect r, bool header = true)
        {
            if (header)
            {
                GUI.Label(new Rect(r.x + 4, r.y, r.width, 22), "라인업", S_section);
                GUI.Label(new Rect(r.x, r.y, r.width - 4, 22), $"{Session.Board.Count} / 8", Label(13, C_sub, FontStyle.Bold, TextAnchor.MiddleRight));
            }
            float top = header ? 30 : 0;
            float sh = Mathf.Min(70, (r.height - top - 4) / 8f - 8), y = r.y + top;
            var list = Session.Board;
            for (int i = 0; i < 8; i++)
            {
                var sr = new Rect(r.x, y, r.width, sh);
                if (i < list.Count) DrawSlot(sr, list[i], i);
                else if (GUI.Button(sr, Net.CanInvite ? "+  빈 자리 · Steam 초대" : "+  빈 자리 · 방 코드 " + Net.RoomCode, S_empty))
                {
                    if (Net.CanInvite) Net.InviteOverlay();
                    else { GUIUtility.systemCopyBuffer = Net.RoomCode ?? ""; Session.Notice = "방 코드를 복사했어요"; }
                }
                y += sh + 8;
            }
        }

        static void DrawSlot(Rect r, Entry p, int i)
        {
            bool me = p.Id == Net.MyId;
            GUI.Box(r, "", me ? S_cardMe : S_card);
            Dot(new Vector2(r.x + 26, r.y + r.height / 2f), 9, SlotColor(i));
            float tx = r.x + 46;
            string name = Trim(p.Name, 14);
            GUI.Label(new Rect(tx, r.y + 10, r.width - 170, 24), name + (me ? $"  <color=#{HexOf(C_sub)}><size=12>나</size></color>" : ""), Label(16, C_txt, FontStyle.Bold));
            if (p.Id == Net.HostId) GUI.Label(new Rect(tx, r.y + r.height - 28, 60, 20), "방장", Label(12, C_gold, FontStyle.Bold));

            // 오른쪽 상태 알약
            Color pc; string pt = PillText(p, out pc);
            var size = Label(12, pc, FontStyle.Bold, TextAnchor.MiddleCenter).CalcSize(new GUIContent(pt)); size.x += 22;
            var pr = new Rect(r.xMax - size.x - 14, r.y + (r.height - 24) / 2f, size.x, 24);
            Pill(pr, pt, pc);

            if (p.State == PState.Downloading || p.State == PState.Paused)
            {
                var br = new Rect(tx + (p.Id == Net.HostId ? 44 : 0), r.y + r.height - 18, r.width - (tx - r.x) - size.x - 40 - (p.Id == Net.HostId ? 44 : 0), 4);
                Fill(br, C_line);
                var o = GUI.color; GUI.color = Color.white;
                GUI.DrawTexture(new Rect(br.x, br.y, br.width * Mathf.Clamp01(p.Download), br.height), T_accBar);
                GUI.color = o;
            }
        }

        static string PillText(Entry e, out Color c)
        {
            c = C_sub;
            switch (e.State)
            {
                case PState.Lobby: return "대기";
                case PState.Downloading: c = C_acc; return $"받는 중 {e.Download * 100:0}%";
                case PState.Paused: c = C_gold; return $"일시정지 {e.Download * 100:0}%";
                case PState.Cancelled: return "관전";
                case PState.HasMap: c = C_acc; return "맵 준비됨";
                case PState.Loading: c = C_ice; return "입장 중";
                case PState.Loaded: c = C_gold; return "준비 전";
                case PState.Ready: c = C_acc; return "준비 완료";
                case PState.Playing: c = C_ice; return $"플레이 {e.Stats.Progress * 100:0}%";
                case PState.Finished: c = C_acc; return $"완주 {e.Stats.Acc * 100:0.0}%";
            }
            return "";
        }

        static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _pillTex = new System.Collections.Generic.Dictionary<Color, Texture2D>();
        static void Pill(Rect r, string text, Color c)
        {
            if (!_pillTex.TryGetValue(c, out var t)) _pillTex[c] = t = Round(A(c, 0.16f), 11, A(c, 0.45f));
            GUI.DrawTexture(r, t);
            GUI.Label(r, text, Label(12, c, FontStyle.Bold, TextAnchor.MiddleCenter));
        }

        // ---------------- 오른쪽: 스테이지 ----------------
        static void DrawStage(Rect r)
        {
            bool host = Net.IsHost;
            // 맵 히어로 카드
            var hero = new Rect(r.x, r.y, r.width, Mathf.Min(260, r.height * 0.42f));
            GUI.Box(hero, "", S_card);
            GUI.Label(new Rect(hero.x + 28, hero.y + 22, 300, 20), Session.MapHash != null ? "이번 판의 맵" : "맵", S_section);

            if (Session.MapHash == null)
            {
                GUI.Label(new Rect(hero.x + 28, hero.y + 60, hero.width - 56, 46), host ? "어떤 맵으로 할까요?" : "방장이 맵을 고르는 중...", S_title);
                GUI.Label(new Rect(hero.x + 28, hero.y + 112, hero.width - 56, 24), host ? "맵을 고르면 모두에게 자동으로 전송돼요. 이미 받은 사람은 건너뛰어요." : "맵이 정해지면 자동으로 받아요.", S_sub);
            }
            else
            {
                string song = string.IsNullOrEmpty(Session.MapSong) ? Session.MapTitle : Session.MapSong;
                GUI.Label(new Rect(hero.x + 28, hero.y + 50, hero.width - 56, 50), Trim(song, 40), S_title);
                string meta = string.Join("   ·   ", new[] { Trim(Session.MapArtist, 30), string.IsNullOrEmpty(Session.MapAuthor) ? "" : "제작 " + Trim(Session.MapAuthor, 24), $"{Session.MapSize / 1048576f:0.0}MB" }.Where(x => !string.IsNullOrEmpty(x)).ToArray());
                GUI.Label(new Rect(hero.x + 28, hero.y + 104, hero.width - 56, 24), meta, Label(16, C_sub));
                if (Session.MapSource == 1)
                {
                    float px = hero.xMax - 28;
                    if (!string.IsNullOrEmpty(Session.MapDiff)) { float w = 22 + Label(12, C_txt, FontStyle.Bold).CalcSize(new GUIContent(Session.MapDiff)).x; px -= w; Pill(new Rect(px, hero.y + 20, w, 24), Esc(Session.MapDiff), Session.MapDiffColor); px -= 8; }
                    string tag = "TUF #" + Session.MapTufId; float tw = 22 + Label(12, C_txt, FontStyle.Bold).CalcSize(new GUIContent(tag)).x; px -= tw;
                    Pill(new Rect(px, hero.y + 20, tw, 24), tag, C_acc);
                }
                // 모두 맵 받은 비율
                var parts = Session.Board.Where(x => x.State != PState.Cancelled).ToList();
                int have = parts.Count(x => x.State >= PState.HasMap);
                if (Session.Phase == Phase.Distributing || Session.Phase == Phase.Packing)
                {
                    float avg = Session.Phase == Phase.Packing ? Mathf.Max(0, Session.HostFetchProgress) : parts.Count == 0 ? 0 : parts.Average(x => x.State >= PState.HasMap ? 1f : x.Download);
                    var br = new Rect(hero.x + 28, hero.yMax - 50, hero.width - 56, 8);
                    Fill(br, C_line);
                    GUI.DrawTexture(new Rect(br.x, br.y, br.width * avg, br.height), T_accBar);
                    GUI.Label(new Rect(br.x, br.y - 28, br.width, 22), Session.Phase == Phase.Packing ? (Session.HostFetchProgress >= 0 ? $"방장이 TUF에서 받는 중 {Session.HostFetchProgress * 100:0}%" : "맵 준비 중...") : $"{have}/{parts.Count}명 받음", Label(13, C_sub, FontStyle.Bold));
                }
            }

            // 상황별 패널 + 메인 버튼
            var panel = new Rect(r.x, hero.yMax + 16, r.width, r.yMax - hero.yMax - 16);
            GUILayout.BeginArea(panel);
            GUILayout.BeginVertical(S_card, GUILayout.ExpandHeight(true));
            if (host) HostPanel(); else MemberPanel();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        static void HostPanel()
        {
            var ph = Session.Phase;
            if (ph == Phase.Results)
            {
                GUILayout.Label("결과 대기", S_section);
                GUILayout.Label("아직 플레이 중인 사람이 있어요. 모두 끝나면 결과가 나와요.", S_paraTxt);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("지금 결과 발표", S_ctaGhost)) Session.HostShowResults();
            }
            else if (ph == Phase.Lobby)
            {
                GUILayout.Label("다음 할 일", S_section);
                GUILayout.Label(Session.MapHash == null ? "맵을 골라서 배포하세요." : "같은 맵으로 한 판 더 하거나, 다른 맵을 골라요.", S_paraTxt);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (Session.MapHash != null && !string.IsNullOrEmpty(Session.HostLevelPath))
                {
                    if (GUILayout.Button("이 맵으로 한 판 더", S_cta)) Session.HostRedo();
                    if (GUILayout.Button("다른 맵 고르기", S_ctaGhost, GUILayout.Width(220))) { _drawer = true; _drawerTab = 1; }
                }
                else if (GUILayout.Button("맵 고르기", S_cta)) { _drawer = true; _drawerTab = 1; }
                GUILayout.EndHorizontal();
            }
            else if (ph == Phase.Packing || ph == Phase.Distributing)
            {
                GUILayout.Label("맵 보내는 중", S_section);
                GUILayout.Label("모두 받으면 자동으로 레벨에 들어가요. 오래 걸리는 사람이 있으면 받은 사람끼리 먼저 시작할 수 있어요.", S_paraTxt);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUI.enabled = ph == Phase.Distributing;
                if (GUILayout.Button("받은 사람끼리 시작", S_ctaGhost)) Session.HostStartLoad();
                GUI.enabled = true;
                if (GUILayout.Button("다른 맵 고르기", S_ctaGhost, GUILayout.Width(220))) { _drawer = true; _drawerTab = 1; }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("진행 중", S_section);
                GUILayout.Label("레벨에 들어가는 중이에요...", S_paraTxt);
            }
        }

        static void MemberPanel()
        {
            var ph = Session.Phase;
            if (ph == Phase.Distributing && Session.MapSize > 0)
            {
                if (Session.DlCancelled)
                {
                    GUILayout.Label("관전", S_section);
                    GUILayout.Label("이번 판은 다운로드를 취소해서 관전해요.", S_paraTxt);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("다시 받고 참가하기", S_cta)) Session.DlRetry();
                    return;
                }
                if (Session.DlActive)
                {
                    float p = (float)Session.DlReceived / Session.MapSize;
                    GUILayout.Label("맵 받는 중", S_section);
                    GUILayout.Label($"<color=#{HexOf(C_acc)}>{p * 100:0}%</color>   <size=16><color=#{HexOf(C_sub)}>{Session.DlReceived / 1048576f:0.0} / {Session.MapSize / 1048576f:0.0}MB{(Session.DlPaused ? "  ·  일시정지됨" : "")}</color></size>", Label(34, C_txt, FontStyle.Bold));
                    Bar(p, 10);
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    if (Session.DlPaused) { if (GUILayout.Button("이어 받기", S_cta)) Session.DlResume(); }
                    else if (GUILayout.Button("일시정지", S_ctaGhost)) Session.DlPause();
                    if (GUILayout.Button("취소하고 관전", S_ctaGhost, GUILayout.Width(200))) Session.DlCancel();
                    GUILayout.EndHorizontal();
                    return;
                }
                GUILayout.Label("준비 완료", S_section);
                GUILayout.Label("맵을 다 받았어요. 다른 사람을 기다리면 자동으로 시작해요.", S_paraTxt);
                return;
            }
            if (ph == Phase.Results)
            {
                GUILayout.Label("결과 대기", S_section);
                GUILayout.Label("다른 사람이 끝나길 기다리는 중이에요.", S_paraTxt);
                return;
            }
            GUILayout.Label("대기 중", S_section);
            GUILayout.Label(Session.MapHash == null ? "방장이 맵을 고르면 자동으로 받아요." : "방장이 다음 판을 준비하는 중이에요.", S_paraTxt);
        }

        // ---------------- 결과 시상대 ----------------
        static void DrawPodium(Rect r, bool inGame = false)
        {
            GUI.Box(r, "", S_card);
            GUI.Label(new Rect(r.x + 28, r.y + 20, 300, 20), "결과", S_section);
            string song = string.IsNullOrEmpty(Session.MapSong) ? Session.MapTitle : Session.MapSong;
            GUI.Label(new Rect(r.x + 28, r.y + 40, r.width - 56, 30), Trim(song, 50), Label(20, C_txt, FontStyle.Bold));

            var res = Session.Results;
            float baseY = r.y + 300, colW = Mathf.Min(220, (r.width - 80) / 3f), cx = r.x + r.width / 2f;
            int[] order = { 1, 0, 2 };              // 2등-1등-3등 배치
            float[] heights = { 150, 110, 80 };
            for (int k = 0; k < 3; k++)
            {
                int rank = order[k];
                if (rank >= res.Count) continue;
                var e = res[rank];
                float x = cx + (k - 1) * (colW + 16) - colW / 2f, h = heights[rank];
                Color c = rank == 0 ? C_gold : rank == 1 ? Hex("d8dee0") : Hex("e2a06a");
                var block = new Rect(x, baseY - h, colW, h);
                GUI.DrawTexture(block, Swatch(A(c, 0.18f)));
                GUI.Label(new Rect(x, baseY - h + 8, colW, 40), (rank + 1).ToString(), Label(34, c, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x, baseY - h - 64, colW, 26), Trim(e.Name, 12), Label(17, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x, baseY - h - 38, colW, 28), $"{e.Stats.Acc * 100:0.00}%", Label(22, c, FontStyle.Bold, TextAnchor.MiddleCenter));
            }

            // 세부 표
            var tr = new Rect(r.x + 20, baseY + 20, r.width - 40, r.yMax - baseY - 100);
            GUILayout.BeginArea(tr);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", S_section, GUILayout.Width(36));
            GUILayout.Label("이름", S_section, GUILayout.Width(200));
            GUILayout.Label("정확도", S_section, GUILayout.Width(100));
            GUILayout.Label("X정확도", S_section, GUILayout.Width(90));
            GUILayout.Label("빠름 ← 판정 → 느림", S_section, GUILayout.Width(240));
            GUILayout.Label("미스", S_section);
            GUILayout.EndHorizontal();
            _sRes = GUILayout.BeginScrollView(_sRes);
            ResultsTable();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (inGame) return;
            var br = new Rect(r.x + 24, r.yMax - 76, r.width - 48, 58);
            GUILayout.BeginArea(br);
            GUILayout.BeginHorizontal();
            if (Net.IsHost)
            {
                if (!string.IsNullOrEmpty(Session.HostLevelPath) && GUILayout.Button("같은 맵으로 한 판 더", S_cta)) { Session.HostBackToLobby(); Session.HostRedo(); }
                if (GUILayout.Button("다른 맵 고르기", S_ctaGhost, GUILayout.Width(220))) { Session.HostBackToLobby(); _drawer = true; _drawerTab = 1; }
            }
            else
            {
                GUILayout.Label("방장이 다음 판을 고르는 중...", S_sub);
                GUILayout.FlexibleSpace();
                if (Session.Phase != Phase.Results && GUILayout.Button("닫기", S_ctaGhost, GUILayout.Width(140))) ShowResults = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void ResultsTable()
        {
            int rank = 1;
            foreach (var e in Session.Results)
            {
                var s = e.Stats;
                Color c = rank == 1 ? C_gold : rank == 2 ? Hex("d8dee0") : rank == 3 ? Hex("e2a06a") : C_sub;
                GUILayout.BeginHorizontal(GUILayout.Height(34));
                GUILayout.Label(rank.ToString(), Label(18, c, FontStyle.Bold), GUILayout.Width(36));
                GUILayout.Label($"{Trim(e.Name, 14)}{(s.Progress < 0.999f ? $"  <color=#{HexOf(C_sub)}><size=12>기권 {s.Progress * 100:0}%</size></color>" : "")}", S_txt, GUILayout.Width(200));
                GUILayout.Label($"{s.Acc * 100:0.00}%", Label(16, C_acc, FontStyle.Bold), GUILayout.Width(100));
                GUILayout.Label($"{s.XAcc * 100:0.00}%", S_sub, GUILayout.Width(90));
                GUILayout.Label($"<color=#{HexOf(C_fire)}>{s.TooEarly}  {s.VeryEarly}</color>  {s.EarlyPerfect}  <color=#{HexOf(C_acc)}><b>{s.Perfect}</b></color>  {s.LatePerfect}  <color=#{HexOf(C_ice)}>{s.VeryLate}  {s.TooLate}</color>", S_txt, GUILayout.Width(240));
                GUILayout.Label((s.Miss + s.Overload).ToString(), S_sub);
                GUILayout.EndHorizontal();
                Fill(GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true)), A(C_line, 0.7f));
                rank++;
            }
        }

        // ---------------- 맵 서랍 (방장) ----------------
        static int _drawerTab;                       // 0 = 내 컴퓨터, 1 = TUF
        static string _tufQuery = "", _tufSent = null;
        static float _tufTypedAt;
        static Tuf.Level _tufSel;
        static Vector2 _sTuf;

        static void DrawMapDrawer(float W, float H)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), T_dim);
            float dw = Mathf.Min(680, W * 0.55f);
            if (Event.current.type == EventType.MouseDown && Event.current.mousePosition.x < W - dw) { _drawer = false; Event.current.Use(); return; }
            var r = new Rect(W - dw, 0, dw, H);
            Fill(r, A(C_side, 0.98f));
            Fill(new Rect(r.x, 0, 1, H), C_line);
            GUILayout.BeginArea(new Rect(r.x + 28, 28, r.width - 56, H - 56));
            GUILayout.BeginHorizontal();
            GUILayout.Label("맵 고르기", Label(26, C_txt, FontStyle.Bold));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", S_iconBtn, GUILayout.Width(80))) _drawer = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // 탭
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("TUF에서 찾기", _drawerTab == 1 ? S_rowOn : S_row, GUILayout.Width(170))) _drawerTab = 1;
            if (GUILayout.Button("내 컴퓨터", _drawerTab == 0 ? S_rowOn : S_row, GUILayout.Width(140))) { _drawerTab = 0; MapBrowser.ScanOnce(); }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            bool busy = Session.Phase == Phase.Packing || (Session.InMatch && Session.Phase != Phase.Results);
            if (_drawerTab == 1) DrawTufTab(busy); else DrawLocalTab(busy);
            GUILayout.EndArea();
        }

        static void SearchField(ref string value, string placeholder)
        {
            value = GUILayout.TextField(value, S_field);
            if (value.Length == 0 && Event.current.type == EventType.Repaint)
            {
                var lr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(lr.x + 16, lr.y, lr.width, lr.height), placeholder, Label(15, A(C_sub, 0.6f)));
            }
        }

        static bool _tufSortOpen, _tufDirty;
        static int _tufDrag = -1;                      // 난이도 막대에서 잡은 손잡이 (0 최소, 1 최대)

        static void TufChip(string text, ref bool on)
        {
            if (GUILayout.Button(text, on ? S_rowOn : S_row, GUILayout.Width(Label(14, C_txt).CalcSize(new GUIContent(text)).x + 40))) { on = !on; _tufDirty = true; }
        }

        /// <summary>P1 ~ U20 난이도 범위 막대 (손잡이 두 개를 끌어서 조절)</summary>
        static void TufDiffRange() => TufDiffRange(GUILayoutUtility.GetRect(100, 58, GUILayout.ExpandWidth(true)));

        static void TufDiffRange(Rect area)
        {
            int n = Mathf.Max(1, Tuf.Pgu.Count);
            float x0 = area.x + 18, x1 = area.xMax - 18, ty = area.y + 34;
            float Pos(int i) => n <= 1 ? x0 : Mathf.Lerp(x0, x1, i / (float)(n - 1));
            int IdxAt(float x) => Mathf.Clamp(Mathf.RoundToInt((x - x0) / Mathf.Max(1, x1 - x0) * (n - 1)), 0, n - 1);

            // 색 막대 (선택 밖은 흐리게)
            for (int i = 0; i < n; i++)
            {
                float a = Pos(i) - (x1 - x0) / (n - 1f) / 2f, b = Pos(i) + (x1 - x0) / (n - 1f) / 2f;
                a = Mathf.Max(a, x0); b = Mathf.Min(b, x1);
                Color c = Tuf.Pgu.Count > 0 ? Tuf.Pgu[i].Color : C_acc;
                bool inside = i >= Tuf.MinIdx && i <= Tuf.MaxIdx;
                Fill(new Rect(a, ty - 4, b - a + 0.5f, 8), inside ? c : A(c, 0.18f));
            }

            // 입력: 가까운 손잡이를 잡고 끌기, 놓으면 검색
            var ev = Event.current;
            var hit = new Rect(area.x, area.y + 8, area.width, area.height - 8);
            if (ev.type == EventType.MouseDown && ev.button == 0 && hit.Contains(ev.mousePosition) && Tuf.Pgu.Count > 0)
            {
                int i = IdxAt(ev.mousePosition.x);
                _tufDrag = i <= Tuf.MinIdx ? 0 : i >= Tuf.MaxIdx ? 1 : (i - Tuf.MinIdx <= Tuf.MaxIdx - i ? 0 : 1);
                ev.Use();
            }
            if (_tufDrag >= 0 && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown))
            {
                int i = IdxAt(ev.mousePosition.x);
                if (_tufDrag == 0) Tuf.MinIdx = Mathf.Min(i, Tuf.MaxIdx); else Tuf.MaxIdx = Mathf.Max(i, Tuf.MinIdx);
                if (ev.type == EventType.MouseDrag) ev.Use();
            }
            if (_tufDrag >= 0 && (ev.type == EventType.MouseUp || ev.rawType == EventType.MouseUp)) { _tufDrag = -1; _tufDirty = true; }

            // 손잡이 (난이도 아이콘, 없으면 이름)
            for (int h = 0; h < 2; h++)
            {
                int idx = h == 0 ? Tuf.MinIdx : Tuf.MaxIdx;
                string name = h == 0 ? Tuf.MinName : Tuf.MaxName;
                Color c = Tuf.Pgu.Count > 0 ? Tuf.Pgu[Mathf.Clamp(idx, 0, n - 1)].Color : C_acc;
                float cx = Pos(Mathf.Clamp(idx, 0, n - 1));
                var hr = new Rect(cx - 15, ty - 15, 30, 30);
                var ic = Tuf.Icon(name);
                if (ic != null) GUI.DrawTexture(hr, ic, ScaleMode.ScaleToFit);
                else Pill(hr, Esc(name), c);
                float lx = Mathf.Clamp(cx - 30, area.x, area.xMax - 60);
                GUI.Label(new Rect(lx, area.y, 60, 16), Esc(name), Label(13, c, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
        }

        /// <summary>입력이 멈추고 0.4초 뒤 / 필터를 바꾸면 바로 검색 (첫 진입 시 기본 정렬)</summary>
        static void TufSearchTick()
        {
            if (_tufSent == null || _tufDirty || (_tufQuery != _tufSent && Time.unscaledTime - _tufTypedAt > 0.4f))
            {
                _tufDirty = false;
                _tufSent = _tufQuery; _tufSel = null; _sTuf = Vector2.zero;
                if (Runner.Instance != null) Tuf.Search(Runner.Instance, _tufQuery);
            }
        }

        static void DrawTufTab(bool busy)
        {
            var before = _tufQuery;
            SearchField(ref _tufQuery, "곡명 · 아티스트 · 제작자로 TUF 검색");
            if (_tufQuery != before) _tufTypedAt = Time.unscaledTime;

            // ---- 필터 ----
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("난이도", S_sub, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            GUILayout.Label("특수 난이도", S_small);
            TufChip("미평가", ref Tuf.IncUnranked);
            TufChip("Q", ref Tuf.IncQ);
            TufChip("기타", ref Tuf.IncEtc);
            GUILayout.EndHorizontal();
            TufDiffRange();

            // ---- 정렬 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("정렬", S_sub, GUILayout.Width(60));
            if (GUILayout.Button(Tuf.SortName, _tufSortOpen ? S_rowOn : S_row, GUILayout.Width(150))) _tufSortOpen = !_tufSortOpen;
            if (GUILayout.Button("높은 순", !Tuf.Asc ? S_rowOn : S_row, GUILayout.Width(90))) { if (Tuf.Asc) { Tuf.Asc = false; _tufDirty = true; } }
            if (GUILayout.Button("낮은 순", Tuf.Asc ? S_rowOn : S_row, GUILayout.Width(90))) { if (!Tuf.Asc) { Tuf.Asc = true; _tufDirty = true; } }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("초기화", S_iconBtn, GUILayout.Width(80)))
            {
                Tuf.MinIdx = 0; Tuf.MaxIdx = Mathf.Max(0, Tuf.Pgu.Count - 1); Tuf.IncUnranked = Tuf.IncQ = Tuf.IncEtc = true;
                Tuf.Sort = "CLEARS"; Tuf.Asc = false; _tufSortOpen = false; _tufDirty = true;
            }
            GUILayout.EndHorizontal();
            if (_tufSortOpen)
            {
                for (int row = 0; row < 2; row++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(64);
                    for (int i = row * 5; i < row * 5 + 5 && i < Tuf.SortKeys.Length; i++)
                        if (GUILayout.Button(Tuf.SortNames[i], Tuf.Sort == Tuf.SortKeys[i] ? S_rowOn : S_row, GUILayout.Width(104)))
                        { Tuf.Sort = Tuf.SortKeys[i]; _tufSortOpen = false; _tufDirty = true; }
                    GUILayout.EndHorizontal();
                }
            }

            TufSearchTick();
            GUILayout.Space(2);
            GUILayout.Label(Tuf.Loading && Tuf.Results.Count == 0 ? "검색 중..." : !string.IsNullOrEmpty(Tuf.Error) ? Tuf.Error : $"총 {Tuf.Total:N0}개   ·   {Tuf.MinName} ~ {Tuf.MaxName}   ·   {Tuf.SortName} {(Tuf.Asc ? "낮은 순" : "높은 순")}", S_small);
            GUILayout.Space(4);

            // ---- 목록 ----
            _sTuf = GUILayout.BeginScrollView(_sTuf, GUILayout.ExpandHeight(true));
            DrawTufRows();
            GUILayout.EndScrollView();
            GUILayout.Space(8);

            if (_tufSel != null)
                GUILayout.Label($"<b>{Esc(Trim(_tufSel.Song, 40))}</b>  <color=#{HexOf(C_sub)}>TUF #{_tufSel.Id} · {Esc(_tufSel.Diff)}</color>", S_txt);
            GUILayout.Label("모두가 TUF에서 직접 받아요 (실패하면 방장에게서 받아요). 받아둔 맵은 다시 받지 않아요.", S_small);
            GUI.enabled = !busy && _tufSel != null && _tufSel.CanDownload;
            if (GUILayout.Button("이 맵으로 시작", S_cta)) { _drawer = false; ShowResults = false; Session.HostDistributeTuf(_tufSel); }
            GUI.enabled = true;
        }

        /// <summary>TUF 결과 행들 (스크롤 뷰 안에서 호출)</summary>
        static void DrawTufRows()
        {
            foreach (var lv in Tuf.Results)
            {
                bool sel = _tufSel == lv;
                var rr = GUILayoutUtility.GetRect(100, 76, GUILayout.ExpandWidth(true));
                if (GUI.Button(rr, "", sel ? S_rowOn : S_row)) _tufSel = lv;

                // 난이도 아이콘
                var ir = new Rect(rr.x + 12, rr.y + 15, 46, 46);
                var ic = Tuf.Icon(lv.Diff);
                if (ic != null) GUI.DrawTexture(ir, ic, ScaleMode.ScaleToFit);
                else if (!string.IsNullOrEmpty(lv.Diff)) Pill(new Rect(ir.x - 4, ir.y + 12, 54, 22), Esc(Trim(lv.Diff, 6)), lv.DiffColor);

                // 오른쪽: 클리어 · 좋아요
                float rw = 96;
                var rc = new Rect(rr.xMax - rw - 12, rr.y + 12, rw, 26);
                GUI.Label(rc, $"<b>{lv.Clears:N0}</b> <size=12>클리어</size>", Label(16, C_acc, FontStyle.Normal, TextAnchor.MiddleRight));
                GUI.Label(new Rect(rc.x, rc.y + 26, rw, 24), $"<b>{lv.Likes:N0}</b> <size=12>좋아요</size>", Label(14, C_fire, FontStyle.Normal, TextAnchor.MiddleRight));

                float tx = rr.x + 70, tw = rc.x - tx - 10;
                GUI.Label(new Rect(tx, rr.y + 6, tw, 18), $"#{lv.Id}  ·  {Esc(Trim(lv.Artist, 34))}", Label(12, C_sub));
                GUI.Label(new Rect(tx, rr.y + 23, tw, 26), Esc(Trim(lv.Song, 40)), Label(17, sel ? C_acc : C_txt, FontStyle.Bold));
                string info = string.Join("   ", new[] {
                    lv.LengthText.Length > 0 ? "길이 " + lv.LengthText : "",
                    lv.Tiles > 0 ? $"타일 {lv.Tiles:N0}" : "",
                    lv.Bpm > 0 ? $"BPM {lv.Bpm:0.##}" : "",
                    string.IsNullOrEmpty(lv.Creator) ? "" : "제작 " + Esc(Trim(lv.Creator, 22)) }.Where(x => x.Length > 0).ToArray());
                GUI.Label(new Rect(tx, rr.y + 50, tw, 20), info, S_small);
            }
            if (Tuf.Results.Count == 0 && !Tuf.Loading && string.IsNullOrEmpty(Tuf.Error)) GUILayout.Label("조건에 맞는 레벨이 없어요. 난이도 범위나 검색어를 바꿔보세요.", S_para);
            if (Tuf.HasMore)
            {
                GUILayout.Space(6);
                GUI.enabled = !Tuf.Loading;
                if (GUILayout.Button(Tuf.Loading ? "불러오는 중..." : "더 보기", S_btn) && Runner.Instance != null) Tuf.Search(Runner.Instance, _tufSent ?? "", true);
                GUI.enabled = true;
            }
        }

        static void DrawLocalTab(bool busy)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("워크샵 · 게임 폴더 · 문서 · 다운로드에서 찾은 맵", S_sub);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(MapBrowser.Scanning ? "찾는 중..." : "새로고침", S_iconBtn, GUILayout.Width(110))) MapBrowser.Rescan();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            SearchField(ref _search, "곡명 · 아티스트 · 제작자 검색");
            GUILayout.Space(6);
            _sMaps = GUILayout.BeginScrollView(_sMaps, GUILayout.ExpandHeight(true));
            DrawLocalRows();
            GUILayout.EndScrollView();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            _mapPath = GUILayout.TextField(_mapPath, S_field);
            if (GUILayout.Button("최근 맵", S_iconBtn, GUILayout.Width(90))) _mapPath = Game.DefaultLevelPath();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUI.enabled = !busy && !string.IsNullOrEmpty(_mapPath);
            if (GUILayout.Button("이 맵으로 시작", S_cta)) { _drawer = false; ShowResults = false; Session.HostDistribute(_mapPath.Trim().Trim('"')); }
            GUI.enabled = true;
        }

        static void DrawLocalRows()
        {
            var q = _search.Trim();
            int shown = 0;
            foreach (var it in MapBrowser.Items)
            {
                if (q.Length > 0 && it.Label.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0 && (it.Author ?? "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > 300) break;
                bool sel = it.Path == _mapPath;
                string song = string.IsNullOrEmpty(it.Song) ? it.Folder : it.Song;
                string sub = string.Join("  ·  ", new[] { it.Artist, string.IsNullOrEmpty(it.Author) ? "" : "제작 " + it.Author }.Where(x => !string.IsNullOrEmpty(x)).ToArray());
                var rr = GUILayoutUtility.GetRect(100, 56, GUILayout.ExpandWidth(true));
                if (GUI.Button(rr, "", sel ? S_rowOn : S_row)) _mapPath = it.Path;
                GUI.Label(new Rect(rr.x + 16, rr.y + 7, rr.width - 32, 24), Trim(song, 48), Label(16, sel ? C_acc : C_txt, FontStyle.Bold));
                GUI.Label(new Rect(rr.x + 16, rr.y + 29, rr.width - 32, 20), Trim(sub, 70), S_small);
            }
            if (MapBrowser.Items.Count == 0 && !MapBrowser.Scanning) GUILayout.Label("맵을 못 찾았어요. 아래에 .adofai 경로를 직접 넣어주세요.", S_para);
        }

        // ---- 커스텀 테마 편집 상태 ----
        static int _editIdx;                    // 0 배경 1 카드 2 글자 3 강조 4 강조2
        static string _hexEdit = "";
        static int _hexFor = -1;
        static readonly string[] EditNames = { "배경", "카드", "글자", "강조", "강조 2" };

        static Color GetBase(Theme t, int i) => i == 0 ? t.Bg : i == 1 ? t.Card : i == 2 ? t.Txt : i == 3 ? t.Acc : t.Acc2;

        static void SetBase(int i, Color c)
        {
            var t = Theme.Custom;
            Color bg = t.Bg, card = t.Card, txt = t.Txt, acc = t.Acc, acc2 = t.Acc2;
            switch (i) { case 0: bg = c; break; case 1: card = c; break; case 2: txt = c; break; case 3: acc = c; break; default: acc2 = c; break; }
            Theme.EditCustom(bg, card, txt, acc, acc2);
        }

        static void CopyToCustom(Theme from)
        {
            Theme.EditCustom(from.Bg, from.Card, from.Txt, from.Acc, from.Acc2);
            _hexFor = -1;
        }

        // ---------------- 설정 팝오버 ----------------
        static void DrawProfile(float W, float H)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), T_dim);
            var r = new Rect(W - 28 - 520, 72, 520, 300);
            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition)) { _profile = false; Event.current.Use(); return; }
            GUILayout.BeginArea(r, S_card);
            GUILayout.BeginHorizontal();
            GUILayout.Label("프로필", Label(22, C_txt, FontStyle.Bold));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", S_iconBtn, GUILayout.Width(80))) _profile = false;
            GUILayout.EndHorizontal();
            Section("멀티에서 쓸 이름");
            SearchField(ref _nickEdit, Net.DefaultName + " (Steam 이름)");
            if (_nickEdit.Length > 20) _nickEdit = _nickEdit.Substring(0, 20);
            GUILayout.Label("비워두면 Steam 이름을 써요. 바꾼 이름은 다음에 방에 들어갈 때부터 보여요.", S_para);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Steam 이름으로", S_ctaGhost)) { _nickEdit = ""; Profile.Save(""); Session.Notice = "이름을 Steam 이름으로 되돌렸어요"; }
            if (GUILayout.Button("저장", S_cta)) { Profile.Save(_nickEdit); _profile = false; Session.Notice = "이름을 " + Net.MyName + "(으)로 바꿨어요"; }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void DrawSettings(float W, float H)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), T_dim);
            bool custom = Theme.Current == Theme.Custom;
            float ph = custom ? 640 : 420;
            var r = new Rect(W - 28 - 640, 72, 640, Mathf.Min(ph, H - 100));
            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition)) { _settings = false; Event.current.Use(); return; }
            GUILayout.BeginArea(r, S_card);
            GUILayout.BeginHorizontal();
            GUILayout.Label("설정", Label(22, C_txt, FontStyle.Bold));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", S_iconBtn, GUILayout.Width(80))) _settings = false;
            GUILayout.EndHorizontal();

            Section("테마");
            GUILayout.BeginHorizontal();
            foreach (var th in Theme.All)
            {
                bool on = th == Theme.Current;
                var tr = GUILayoutUtility.GetRect(180, 74, GUILayout.Width(180), GUILayout.Height(74));
                if (GUI.Button(tr, "", on ? S_rowOn : S_row)) { Theme.Set(th); _hexFor = -1; }
                if (Event.current.type == EventType.Repaint)
                    for (int k = 0; k < 5; k++)
                        GUI.DrawTexture(new Rect(tr.x + 12 + k * 30, tr.y + 12, 24, 24), Swatch(GetBase(th, k)));
                GUI.Label(new Rect(tr.x + 12, tr.y + 44, tr.width - 20, 22), th.Name, Label(13, on ? C_acc : C_txt, on ? FontStyle.Bold : FontStyle.Normal));
                GUILayout.Space(6);
            }
            GUILayout.EndHorizontal();

            if (custom) CustomEditor();

            Section("업데이트");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"현재 v{Main.Version}   ·   {Updater.StatusText}", S_sub);
            GUILayout.FlexibleSpace();
            GUI.enabled = Updater.Current != Updater.State.Checking && Updater.Current != Updater.State.Downloading && Updater.Current != Updater.State.ReadyToRestart;
            if (GUILayout.Button("지금 확인", S_iconBtn, GUILayout.Width(110)) && Runner.Instance != null) Updater.CheckNow(Runner.Instance);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (Updater.Current == Updater.State.Failed) GUILayout.Label(Esc(Updater.Error), S_para);

            Section("받은 맵 캐시");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{CacheCount()}개 · {CacheSizeMB():0.0}MB", S_sub);
            GUILayout.FlexibleSpace();
            GUI.enabled = !Session.InMatch && Session.Phase != Phase.Distributing;
            if (GUILayout.Button("비우기", S_iconBtn, GUILayout.Width(90))) { try { Directory.Delete(MapPack.Root, true); } catch { } _cacheAt = -10; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Section("단축키");
            GUILayout.Label("메인 섬의 멀티 포탈로 입장   ·   F5  준비   ·   Ctrl + Shift + Q  기권   ·   Esc  뒤로", S_para);
            GUILayout.EndArea();
        }

        static void CustomEditor()
        {
            var t = Theme.Custom;
            GUILayout.Space(6);
            // 편집할 색 고르기
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 5; i++)
            {
                bool on = i == _editIdx;
                var cr = GUILayoutUtility.GetRect(110, 64, GUILayout.Width(110), GUILayout.Height(64));
                if (GUI.Button(cr, "", on ? S_rowOn : S_row)) { _editIdx = i; _hexFor = -1; }
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(new Rect(cr.x + 10, cr.y + 10, cr.width - 20, 22), Swatch(GetBase(t, i)));
                GUI.Label(new Rect(cr.x + 10, cr.y + 36, cr.width - 16, 20), EditNames[i], Label(12, on ? C_acc : C_sub, on ? FontStyle.Bold : FontStyle.Normal));
                GUILayout.Space(2);
            }
            GUILayout.EndHorizontal();

            var c = GetBase(t, _editIdx);
            if (_hexFor != _editIdx) { _hexEdit = ColorUtility.ToHtmlStringRGB(c); _hexFor = _editIdx; }

            // 색상/채도/밝기 슬라이더
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            float nh = SliderRow("색상", h), ns = SliderRow("채도", sat), nv = SliderRow("밝기", v);
            if (!Mathf.Approximately(nh, h) || !Mathf.Approximately(ns, sat) || !Mathf.Approximately(nv, v))
            {
                var nc = Color.HSVToRGB(nh, ns, nv);
                SetBase(_editIdx, nc);
                _hexEdit = ColorUtility.ToHtmlStringRGB(nc);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("HEX  #", S_sub, GUILayout.Width(60));
            var nhex = GUILayout.TextField(_hexEdit, 6, S_field, GUILayout.Width(140));
            if (nhex != _hexEdit)
            {
                _hexEdit = nhex;
                if (nhex.Length == 6 && ColorUtility.TryParseHtmlString("#" + nhex, out var parsed)) SetBase(_editIdx, parsed);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("모노에서 시작", S_iconBtn, GUILayout.Width(130))) CopyToCustom(Theme.Mono);
            if (GUILayout.Button("불과 얼음에서 시작", S_iconBtn, GUILayout.Width(160))) CopyToCustom(Theme.FireIce);
            GUILayout.EndHorizontal();
            GUILayout.Label("나머지 색(테두리, 보조 글자, 빛 등)은 이 5색에서 자동으로 맞춰져요.", S_para);
        }

        static float SliderRow(string label, float value)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(26));
            GUILayout.Label(label, S_sub, GUILayout.Width(60));
            float nv = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label(Mathf.RoundToInt(nv * 100) + "%", Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleRight), GUILayout.Width(48));
            GUILayout.EndHorizontal();
            return nv;
        }

        static void DrawConfirm()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_dim);
            var r = new Rect((Screen.width - 460) / 2f, (Screen.height - 200) / 2f, 460, 200);
            GUILayout.BeginArea(r, S_card);
            GUILayout.Label("어디로 갈까요?", Label(22, C_txt, FontStyle.Bold));
            GUILayout.Label(Net.IsHost ? "방장이 나가면 방이 사라져요." : "방에서 나가면 다시 들어오려면 초대가 필요해요.", S_para);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("방만 나가기", S_btn)) { _confirmLeave = false; Session.LeaveRoom(); ShowResults = false; }
            if (GUILayout.Button("메인 메뉴로", S_btnAcc)) { _confirmLeave = false; Session.LeaveRoom(); Island.Leave(); }
            if (GUILayout.Button("취소", S_btn)) _confirmLeave = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void AskLeave()
        {
            if (Session.InRoom) _confirmLeave = true;
            else Island.Leave();
        }

        static void Bar(float p, float h)
        {
            var r = GUILayoutUtility.GetRect(100, h, GUILayout.ExpandWidth(true));
            Fill(r, C_line);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(p), r.height), T_accBar);
        }

        // =====================================================================
        //  레벨 안 (실시간 순위 · 카운트다운 · 결과)
        // =====================================================================
        static void DrawInGame()
        {
            var ph = Session.Phase;
            var board = Session.Board.Where(e => e.State >= PState.Loading && e.State != PState.Cancelled).ToList();
            float rowH = 32, w = 264;
            var box = new Rect(24 * Screen.height / 1080f, 24 * Screen.height / 1080f, w, 36 + board.Count * rowH);
            GUI.color = A(Color.white, 0.94f);
            GUI.Box(box, "", S_card);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 16, box.y + 8, 200, 18), "실시간 순위", S_section);
            for (int i = 0; i < board.Count; i++)
            {
                var e = board[i];
                float ry = box.y + 30 + i * rowH;
                bool me = e.Id == Net.MyId;
                int slot = Session.Board.IndexOf(e);
                if (me) Fill(new Rect(box.x + 6, ry, w - 12, rowH - 4), A(C_acc, 0.10f));
                GUI.Label(new Rect(box.x + 14, ry, 22, rowH - 4), (i + 1).ToString(), Label(15, i == 0 ? C_gold : C_sub, FontStyle.Bold));
                Dot(new Vector2(box.x + 44, ry + (rowH - 4) / 2f), 5, SlotColor(slot < 0 ? i : slot));
                GUI.Label(new Rect(box.x + 56, ry, 112, rowH - 4), Trim(e.Name, 8) + (e.State == PState.Finished ? "  <size=11>완주</size>" : ""), Label(15, me ? C_acc : C_txt, me ? FontStyle.Bold : FontStyle.Normal));
                GUI.Label(new Rect(box.x + 160, ry, 92, rowH - 4), $"{e.Stats.Acc * 100:0.00}%", Label(15, me ? C_acc : C_txt, FontStyle.Bold, TextAnchor.MiddleRight));
                Fill(new Rect(box.x + 56, ry + rowH - 8, (w - 72) * Mathf.Clamp01(e.Stats.Progress), 2), A(SlotColor(slot < 0 ? i : slot), 0.7f));
            }

            if (ph == Phase.WaitingStart)
                Banner(Session.MyReady ? "준비 완료 · 다른 사람을 기다리는 중" : $"<color=#{HexOf(C_acc)}>F5</color>  를 눌러 준비");
            else if (ph == Phase.Countdown)
            {
                int n = Mathf.CeilToInt(Session.CountdownLeft);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 150), n > 0 ? n.ToString() : "GO!", S_big);
            }
            else if (ph == Phase.Results)
            {
                if (Session.Results.Count > 0)
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), T_dim);
                    var r = new Rect((Screen.width - 900) / 2f, (Screen.height - 560) / 2f, 900, 560);
                    DrawPodium(r, true);
                    if (Net.IsHost)
                    {
                        var br = new Rect(r.x + 24, r.yMax - 76, r.width - 48, 58);
                        GUILayout.BeginArea(br);
                        if (GUILayout.Button("모두 멀티 섬으로", S_cta)) Session.HostBackToLobby();
                        GUILayout.EndArea();
                    }
                }
                else Banner("완주! 다른 사람을 기다리는 중...");
            }

            if (ph == Phase.Countdown || ph == Phase.Playing)
                GUI.Label(new Rect(Screen.width - 260, Screen.height - 30, 248, 24), "기권  Ctrl + Shift + Q", Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));
        }

        static void Banner(string text)
        {
            var r = new Rect((Screen.width - 560) / 2f, Screen.height * 0.1f, 560, 66);
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

        // 디스크 조회는 3초에 한 번만 (OnGUI는 프레임마다 여러 번 불림)
        static int _cacheCount; static float _cacheMB, _cacheAt = -10;
        static void RefreshCache()
        {
            if (Time.unscaledTime - _cacheAt < 3f) return;
            _cacheAt = Time.unscaledTime;
            try
            {
                _cacheCount = Directory.Exists(MapPack.Root) ? Directory.GetDirectories(MapPack.Root).Length : 0;
                _cacheMB = Directory.Exists(MapPack.Root) ? Directory.GetFiles(MapPack.Root, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1048576f : 0;
            }
            catch { _cacheCount = 0; _cacheMB = 0; }
        }
        static int CacheCount() { RefreshCache(); return _cacheCount; }
        static float CacheSizeMB() { RefreshCache(); return _cacheMB; }

        static string Esc(string s) => (s ?? "").Replace("<", "＜").Replace(">", "＞");
        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : Esc(s.Length > n ? s.Substring(0, n) + "…" : s);
    }
}
