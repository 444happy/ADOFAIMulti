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
        static GUIStyle S_para, S_paraTxt, S_h1, S_h2, S_txt, S_sub, S_small, S_num, S_card, S_cardBtn, S_btn, S_btnAcc, S_btnDanger, S_side, S_sideOn, S_field, S_chip, S_big, S_row, S_rowOn, S_logo, S_section;
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
            _owned.Clear(); _swatches.Clear(); _pillTex.Clear(); _avTex.Clear(); _lBox.Clear(); _freeH.Clear();
            _labelCache.Clear();
            C_bg = th.Bg; C_side = th.Side; C_card = th.Card; C_cardHi = th.CardHi; C_line = th.Line; C_acc = th.Acc; C_acc2 = th.Acc2;
            C_txt = th.Txt; C_sub = th.Sub; C_fire = th.Fire; C_ice = th.Ice; C_gold = th.Gold; C_ok = th.Ok;

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
            S_btnDanger = Button(Round(Hex("ff8f8f"), 12), Round(Hex("ffa8a8"), 12), Hex("3a1010"), 15, 46); S_btnDanger.fontStyle = FontStyle.Bold;
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

        static GUIStyle S_cta40, S_field40, S_field48, S_cardMe, S_cta, S_ctaGhost, S_pill, S_title, S_stepOn, S_stepOff, S_empty, S_iconBtn;
        static int _extraVer = -1;

        static void ExtraStyles()
        {
            _extraVer = _builtVer;
            S_cardMe = new GUIStyle(S_card); S_cardMe.normal.background = T_cardHi;
            S_cta = new GUIStyle(S_btnAcc) { fontSize = 19, fixedHeight = 58 };
            S_cta40 = new GUIStyle(S_cta) { fontSize = 17, fixedHeight = 40, padding = new RectOffset(12, 12, 4, 4) };
            S_field40 = new GUIStyle(S_field) { fixedHeight = 40, fontSize = 20, fontStyle = FontStyle.Bold };
            S_field48 = new GUIStyle(S_field) { fixedHeight = 48, fontSize = 17 };
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
        // 비밀번호 창: 0 없음, 1 비공개 방 들어가기, 2 (방장) 비공개로 바꾸며 비밀번호 정하기
        static int _pwMode; static string _pwCode = "", _pwEdit = "", _pwMsg = "", _roomPw = "";
        public static void AskPassword(string code, string msg) { _pwMode = 1; _pwCode = code ?? ""; _pwEdit = ""; _pwMsg = msg ?? ""; }
        static void AskSetPassword() { _pwMode = 2; _pwEdit = Net.RoomPw ?? ""; _pwMsg = ""; }
        static void TryCreateRoom()
        {
            if (!_newPublic && string.IsNullOrWhiteSpace(_roomPw)) { Session.Notice = "비공개 방은 비밀번호를 정해야 해요"; Net.Status = Session.Notice; return; }
            Net.CreateRoom(_newPublic, _roomName, _roomPw);
        }
        static void JoinRoom(RoomList.Room rm)
        {
            if (rm.Locked) AskPassword(rm.Code, "");
            else Net.Join(rm.Code);
        }

        static bool _pwShowCreate, _pwShowPrompt;
        static Texture2D _eyeOpen, _eyeShut;
        static Texture2D EyeTex(bool open)
        {
            ref Texture2D t = ref (open ? ref _eyeOpen : ref _eyeShut);
            if (t != null) return t;
            const int n = 48;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                    float lid = 0.5f * (1 - u * u);                       // 눈꺼풀 곡선
                    float dLid = Mathf.Abs(Mathf.Abs(v) - lid);
                    float a = Mathf.Abs(u) < 0.98f ? Mathf.Clamp01(1 - (dLid - 0.035f) * 24) : 0;
                    float pr = Mathf.Sqrt(u * u + v * v);
                    a = Mathf.Max(a, Mathf.Clamp01(1 - (pr - 0.2f) * 24));  // 눈동자
                    if (!open) a = Mathf.Max(a, Mathf.Clamp01(1 - (Mathf.Abs(u + v) / 1.414f - 0.04f) * 24) * (Mathf.Abs(u) < 0.8f ? 1 : 0));
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>오른쪽 끝 눈 버튼 (누르면 비밀번호 보이기/숨기기). 버튼 영역을 돌려줌</summary>
        static Rect EyeButton(Rect field, ref bool show)
        {
            float s = Mathf.Min(field.height, 40);
            var r = new Rect(field.xMax - s - 4, field.y + (field.height - s) / 2f, s, s);
            var o = GUI.color; GUI.color = A(C_sub, r.Contains(Event.current.mousePosition) ? 1f : 0.75f);
            GUI.DrawTexture(new Rect(r.x + s * 0.2f, r.y + s * 0.2f, s * 0.6f, s * 0.6f), EyeTex(show));
            GUI.color = o;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) show = !show;
            return r;
        }

        static string InviteLabel() => !string.IsNullOrEmpty(Net.InviteCode) ? "코드  <b>" + Net.InviteCode + "</b>  복사" : Net.InviteBusy ? "코드 만드는 중..." : "참가 코드 만들기";
        static void InviteClick()
        {
            if (!string.IsNullOrEmpty(Net.InviteCode)) { GUIUtility.systemCopyBuffer = Net.InviteCode; Session.Notice = "참가 코드를 복사했어요"; }
            else Net.RequestInvite();
        }

        static bool _layoutMapBtn;     // 설계 배치에 맵 선택 버튼이 따로 있으면 알림 칸에서는 숨김

        /// <summary>판정 설정 (방장만 바꿈)</summary>
        static void JudgeRow()
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("판정", S_sub, GUILayout.Width(50));
            for (int i = 0; i < 3; i++)
            {
                var o = GUI.enabled; GUI.enabled = Net.IsHost && o;
                if (GUILayout.Button(Session.JudgeNames[i], Session.Judge == i ? S_rowOn : S_row, GUILayout.Width(96))) Session.HostSetJudge(i);
                GUI.enabled = o;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        static void DrawPasswordPrompt()
        {
            bool set = _pwMode == 2;
            var cr = Modal(500, 300, set ? "비공개 방 비밀번호" : "비밀번호가 걸린 방", "lock", () => _pwMode = 0);
            if (cr.width <= 0) return;
            bool err = _pwMsg.Length > 0 && !set;
            GUI.Label(new Rect(cr.x, cr.y, cr.width, 24), set ? "친구에게 알려줄 비밀번호를 정해주세요." : err ? Esc(_pwMsg) : "방장에게 받은 비밀번호를 입력하세요.", Label(15, err ? C_fire : C_sub));
            var fr = new Rect(cr.x, cr.y + 40, cr.width, 48);
            EyeButton(fr, ref _pwShowPrompt);
            GUI.SetNextControlName("pwField");
            var fr2 = new Rect(fr.x, fr.y, fr.width - 48, fr.height);
            _pwEdit = _pwShowPrompt ? GUI.TextField(fr2, _pwEdit ?? "", 32, S_field48) : GUI.PasswordField(fr2, _pwEdit ?? "", '*', 32, S_field48);
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "pwField") GUI.FocusControl("pwField");
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool ok = !string.IsNullOrWhiteSpace(_pwEdit);
            int hit = ModalButtons(cr, ("취소", false, true), (set ? "비공개로 바꾸기" : "들어가기", true, ok));
            if (hit == 0) _pwMode = 0;
            if (hit == 1 || (enter && ok))
            {
                if (set) { Net.SetPublic(false, _pwEdit); Session.Notice = "비공개 방이 됐어요 — 비밀번호를 알아야 들어와요"; }
                else Net.Join(_pwCode, _pwEdit);
                _pwMode = 0;
            }
        }
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
                if (_pwMode != 0) _pwMode = 0;
                else if (_confirmLeave) _confirmLeave = false;
                else if (_settings) _settings = false;
                else if (_profile) _profile = false;
                else if (_tufSortOpen && _drawer) _tufSortOpen = false;
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
                if (_pwMode != 0) DrawPasswordPrompt();
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
            DrawToast();
            DrawFooter(Session.InRoom);
            if (_settings) DrawSettings(W, H);
            if (_profile) DrawProfile(W, H);
            if (_confirmLeave) DrawConfirm();
            if (_pwMode != 0) DrawPasswordPrompt();
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
                    { if (Net.RoomPublic) AskSetPassword(); else { Net.SetPublic(true); Session.Notice = "공개 방이 됐어요"; } }
                }
                if (Net.IsHost || !string.IsNullOrEmpty(Net.InviteCode))
                {
                    x -= 196;
                    if (GUI.Button(new Rect(x, 24, 188, 36), InviteLabel(), S_iconBtn)) InviteClick();
                }
            }
            GUI.DrawTexture(new Rect(28, 74, W - 56, 1), T_line);

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
            if (GUILayout.Button("방 만들기", S_cta40, GUILayout.ExpandWidth(true))) TryCreateRoom();
            GUILayout.EndHorizontal();
            if (!_newPublic)
            {
                GUILayout.Space(6);
                var pr = GUILayoutUtility.GetRect(100, 40, GUILayout.ExpandWidth(true));
                EyeButton(pr, ref _pwShowCreate);
                var pr2 = new Rect(pr.x, pr.y, pr.width - 44, pr.height);
                _roomPw = _pwShowCreate ? GUI.TextField(pr2, _roomPw ?? "", 32, S_field) : GUI.PasswordField(pr2, _roomPw ?? "", '*', 32, S_field);
                if (string.IsNullOrEmpty(_roomPw) && Event.current.type == EventType.Repaint) GUI.Label(new Rect(pr2.x + 16, pr2.y, pr2.width, pr2.height), "비밀번호", Label(15, A(C_sub, 0.6f)));
            }
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
                GUI.Label(new Rect(lr.x + 16, lr.y, lr.width, lr.height), "참가 코드 6자리", Label(15, A(C_sub, 0.6f)));
            }
            GUI.enabled = Net.IsValidCode(_joinId);
            if (GUILayout.Button("참가", S_cta40, GUILayout.Width(110))) Net.JoinByCode(_joinId);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            int cached = CacheCount();
            if (cached > 0) { GUILayout.Space(8); GUILayout.Label($"받아둔 맵 {cached}개 · {CacheSizeMB():0}MB", Label(12, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter)); }
            GUILayout.EndArea();
        }

        static void DrawRoomCard(RoomList.Room rm)
        {
            var r = GUILayoutUtility.GetRect(100, 110, GUILayout.ExpandWidth(true));
            r = new Rect(r.x, r.y, r.width - 4, r.height - 10);
            bool canJoin = rm.SameVersion && !rm.Full && !rm.InGame;
            bool hover = canJoin && GUI.enabled && r.Contains(Event.current.mousePosition);

            // 상태: 글자 · 색
            string st; Color sc;
            if (!rm.SameVersion) { st = "버전 다름 v" + Trim(rm.Ver, 10); sc = C_sub; }
            else if (rm.Full) { st = "가득 참"; sc = C_sub; }
            else if (rm.InGame) { st = rm.Phase == "results" ? "결과 보는 중" : "게임 중"; sc = C_gold; }
            else if (rm.Phase == "distributing") { st = "맵 받는 중"; sc = C_acc; }
            else { st = "대기 중"; sc = C_ok; }

            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(r, hover ? Hex("232934") : Hex("1b1f26"), hover ? A(C_acc, 0.7f) : Hex("2c333d"), 14);
                // 왼쪽 상태 띠
                RoundBox(new Rect(r.x + 10, r.y + 16, 4, r.height - 32), canJoin ? sc : A(sc, 0.5f), Color.clear, 2);
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && canJoin) JoinRoom(rm);

            var o = GUI.color; if (!canJoin) GUI.color = A(o, o.a * 0.6f);
            float tx = r.x + 32, rightW = 300, tw = r.width - (tx - r.x) - rightW;

            // 1줄: (자물쇠) 방 이름
            float nx = tx;
            if (rm.Locked) { Icons.Draw(new Rect(nx, r.y + 19, 20, 20), "lock", C_gold); nx += 26; }
            GUI.Label(new Rect(nx, r.y + 14, tw - (nx - tx), 30), Trim(rm.Name, 34), Label(20, C_txt, FontStyle.Bold));

            // 2줄: ♪ 곡 — 아티스트
            Icons.Draw(new Rect(tx, r.y + 52, 16, 16), "music", C_sub);
            string song = string.IsNullOrEmpty(rm.Song) ? "맵 고르는 중" : Trim(rm.Song, 36) + (string.IsNullOrEmpty(rm.Artist) ? "" : $"  <color=#{HexOf(A(C_sub, 0.75f))}>—  {Trim(rm.Artist, 22)}</color>");
            GUI.Label(new Rect(tx + 22, r.y + 48, tw - 22, 24), song, Label(15, string.IsNullOrEmpty(rm.Song) ? C_sub : C_txt));

            // 3줄: 왕관 방장
            Icons.Draw(new Rect(tx, r.y + 76, 15, 15), "crown", A(C_gold, 0.85f));
            GUI.Label(new Rect(tx + 22, r.y + 72, tw - 22, 20), Trim(rm.Host, 18), Label(13, C_sub));

            // 오른쪽: 인원 점 + 숫자 / 상태 알약 / 화살표
            float rx = r.xMax - rightW;
            float pipY = r.y + 28;
            for (int i = 0; i < rm.Max && i < 8; i++)
                Dot(new Vector2(rx + 8 + i * 15, pipY), 4.5f, i < rm.Count ? (canJoin ? C_acc : C_sub) : A(C_sub, 0.22f));
            GUI.Label(new Rect(rx + 8 + Mathf.Min(8, rm.Max) * 15, pipY - 12, 70, 24), $"<b>{rm.Count}</b><color=#{HexOf(C_sub)}> / {rm.Max}</color>", Label(15, C_txt));
            float w = 26 + Label(13, sc, FontStyle.Bold).CalcSize(new GUIContent(st)).x;
            Pill(new Rect(rx + 6, r.y + r.height - 38, w, 26), Esc(st), sc);
            if (canJoin)
            {
                var jr = new Rect(r.xMax - 108, r.y + (r.height - 36) / 2f, 92, 36);
                if (Event.current.type == EventType.Repaint) RoundBox(jr, hover ? C_acc : A(C_acc, 0.14f), hover ? C_acc : A(C_acc, 0.5f), 10);
                IconLabel(jr, rm.Locked ? "key" : null, "입장", Label(15, hover ? Theme.Current.AccText : C_acc, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            GUI.color = o;
        }

        // ---------------- 왼쪽: 라인업 (8슬롯) ----------------
        static void DrawLineup(Rect r, bool header = true)
        {
            if (header)
            {
                GUI.Label(new Rect(r.x + 4, r.y, r.width, 22), "라인업", S_section);
                GUI.Label(new Rect(r.x, r.y, r.width - 4, 22), $"{Session.Board.Count} / 8", Label(13, C_sub, FontStyle.Bold, TextAnchor.MiddleRight));
            }
            float top = header ? 30 : 0, gap = 8;
            float sh = Mathf.Floor(Mathf.Min(96, (r.height - top) / 8f - gap)), y = r.y + top;
            var list = Session.Board;
            for (int i = 0; i < 8; i++)
            {
                var sr = new Rect(r.x, y, r.width, sh);
                if (i < list.Count) DrawSlot(sr, list[i], i);
                else DrawEmptySlot(sr, i == list.Count);
                y += sh + gap;
            }
        }

        static void DrawEmptySlot(Rect r, bool first)
        {
            string text = Net.CanInvite ? "Steam 친구 초대" : !string.IsNullOrEmpty(Net.InviteCode) ? "참가 코드 " + Net.InviteCode + " 복사" : Net.IsHost ? "참가 코드 만들기" : "빈 자리";
            bool act = Net.CanInvite || Net.IsHost || !string.IsNullOrEmpty(Net.InviteCode);
            bool hover = act && first && GUI.enabled && r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
                RoundBox(r, hover ? A(C_acc, 0.06f) : A(Color.white, 0.015f), hover ? A(C_acc, 0.5f) : A(Color.white, 0.06f), 14);
            float d = Mathf.Min(44, r.height - 24);
            var cr = new Rect(r.x + 16, r.y + (r.height - d) / 2f, d, d);
            if (Event.current.type == EventType.Repaint) RoundBox(cr, Color.clear, A(Color.white, first ? 0.16f : 0.07f), d / 2f);
            if (first && act) Icons.Draw(new Rect(cr.x + d * 0.28f, cr.y + d * 0.28f, d * 0.44f, d * 0.44f), "plus", hover ? C_acc : C_sub);
            // 첫 빈 자리에만 초대 안내, 나머지는 조용히
            if (first) GUI.Label(new Rect(cr.xMax + 14, r.y, r.width - d - 40, r.height), text, Label(15, hover ? C_acc : A(C_sub, act ? 0.9f : 0.5f), act ? FontStyle.Bold : FontStyle.Normal));
            if (first && act && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (Net.CanInvite) Net.InviteOverlay();
                else InviteClick();
            }
        }

        static void DrawSlot(Rect r, Entry p, int i)
        {
            bool me = p.Id == Net.MyId, host = p.Id == Net.HostId;
            var col = SlotColor(i);
            if (Event.current.type == EventType.Repaint)
                RoundBox(r, me ? Hex("212834") : Hex("1f242c"), me ? A(C_acc, 0.55f) : A(Color.white, 0.06f), 14);

            float d = Mathf.Min(48, r.height - 24);
            var av = new Rect(r.x + 16, r.y + (r.height - d) / 2f, d, d);
            Avatar(av, p.Name, col);
            float tx = av.xMax + 14;

            // 상태 (오른쪽 표시 + 아래 줄 글자)
            Color pc; string pt = PillText(p, out pc);
            bool ready = !host && ((p.State == PState.HasMap && p.LobbyReady) || p.State == PState.Ready);
            bool dl = p.State == PState.Downloading || p.State == PState.Paused;
            float rightW = ready ? 44 : dl ? 64 : 8;

            // 1줄: (왕관) 이름 (나)
            var nameSt = Label(18, C_txt, FontStyle.Bold);
            float lineH = 26, two = r.height >= 64 ? 1 : 0;
            float ny = two > 0 ? r.y + r.height / 2f - lineH + 2 : r.y + (r.height - lineH) / 2f;
            float nx = tx;
            if (host) { Icons.Draw(new Rect(nx, ny + 4, 18, 18), "crown", C_gold); nx += 24; }
            string name = Trim(p.Name, 14);
            float nw = Mathf.Min(nameSt.CalcSize(new GUIContent(name)).x, r.xMax - nx - rightW - 50);
            GUI.Label(new Rect(nx, ny, nw + 2, lineH), name, nameSt);
            if (me) Pill(new Rect(nx + nw + 8, ny + 3, 30, 20), "나", C_acc);

            // 2줄: 상태 글자
            if (two > 0)
                GUI.Label(new Rect(tx, r.y + r.height / 2f + 2, r.xMax - tx - rightW - 12, 22), (host && p.State <= PState.HasMap ? "방장  ·  " : "") + pt, Label(14, pc));

            // 오른쪽
            if (ready)
            {
                var cr = new Rect(r.xMax - 46, r.y + (r.height - 30) / 2f, 30, 30);
                Dot(cr.center, 15, A(C_ok, 0.18f));
                Icons.Draw(new Rect(cr.x + 6, cr.y + 6, 18, 18), "check", C_ok);
            }
            else if (dl)
                GUI.Label(new Rect(r.xMax - 80, r.y, 64, r.height), $"{p.Download * 100:0}%", Label(16, p.State == PState.Paused ? C_gold : C_acc, FontStyle.Bold, TextAnchor.MiddleRight));
            else if (p.State == PState.Finished)
                GUI.Label(new Rect(r.xMax - 110, r.y, 94, r.height), $"{p.Stats.Acc * 100:0.00}%", Label(16, C_ok, FontStyle.Bold, TextAnchor.MiddleRight));
            else if (p.State == PState.Playing)
                GUI.Label(new Rect(r.xMax - 110, r.y, 94, r.height), $"{p.Stats.Progress * 100:0}%", Label(16, C_ice, FontStyle.Bold, TextAnchor.MiddleRight));

            // 맨 아래 받기 막대
            if (dl)
            {
                var br = new Rect(r.x + 14, r.yMax - 7, r.width - 28, 3);
                Fill(br, A(C_line, 0.9f));
                Fill(new Rect(br.x, br.y, br.width * Mathf.Clamp01(p.Download), br.height), p.State == PState.Paused ? C_gold : C_acc);
            }
        }

        static string PillText(Entry e, out Color c)
        {
            c = C_sub;
            bool host = e.Id == Net.HostId;
            switch (e.State)
            {
                case PState.Lobby: return host ? "맵 고르는 중" : "대기 중";
                case PState.Downloading: c = C_acc; return $"맵 받는 중  {e.Download * 100:0}%";
                case PState.Paused: c = C_gold; return $"받기 일시정지  {e.Download * 100:0}%";
                case PState.Cancelled: return "관전";
                case PState.HasMap: if (host) { c = C_acc; return "맵 준비됨"; } if (e.LobbyReady) { c = C_ok; return "준비 완료"; } c = C_gold; return "준비 전";
                case PState.Loading: c = C_ice; return "입장 중";
                case PState.Loaded: c = C_gold; return "카운트다운 대기";
                case PState.Ready: c = C_ok; return "준비 완료";
                case PState.Playing: c = C_ice; return $"플레이 중  {e.Stats.Progress * 100:0}%";
                case PState.Finished: c = C_ok; return $"완주  {e.Stats.Acc * 100:0.00}%";
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
                GUI.Label(new Rect(hero.x + 28, hero.y + 60, hero.width - 56, 46), host ? "선택한 맵 없음" : "방장이 맵을 고르는 중", S_title);
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
                GUILayout.Label(Session.MapHash == null ? "맵을 선택하면 모두에게 보내져요." : "같은 맵으로 한 판 더 하거나, 다른 맵을 선택하세요.", S_paraTxt);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (Session.MapHash != null && !string.IsNullOrEmpty(Session.HostLevelPath))
                {
                    if (GUILayout.Button("이 맵으로 한 판 더", S_cta)) Session.HostRedo();
                    if (!_layoutMapBtn && GUILayout.Button("다른 맵 선택", S_ctaGhost, GUILayout.Width(220))) { _drawer = true; _drawerTab = 1; }
                }
                else if (!_layoutMapBtn && GUILayout.Button("맵 선택", S_cta)) { _drawer = true; _drawerTab = 1; }
                JudgeRow();
                GUILayout.EndHorizontal();
            }
            else if (ph == Phase.Packing || ph == Phase.Distributing)
            {
                GUILayout.Label(Session.CanStart ? "모두 준비 완료" : "준비 기다리는 중", S_section);
                GUILayout.Label(Session.CanStart ? "시작을 누르면 모두 레벨에 들어가고 바로 카운트다운이 시작돼요." : $"모두 맵을 받고 준비를 눌러야 시작할 수 있어요.   준비 {Session.ReadyCount}/{Session.ReadyNeeded}", S_paraTxt);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUI.enabled = Session.CanStart;
                if (GUILayout.Button($"시작  {Session.ReadyCount}/{Session.ReadyNeeded} 준비", S_cta)) Session.HostStart();
                GUI.enabled = true;
                if (!_layoutMapBtn && GUILayout.Button("다른 맵 선택", S_ctaGhost, GUILayout.Width(220))) { _drawer = true; _drawerTab = 1; }
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
                GUILayout.Label(Session.MyReady ? "준비 완료" : "맵 받기 완료", S_section);
                GUILayout.Label(Session.MyReady ? "방장이 시작을 누르면 바로 시작해요." : "준비를 누르세요 (F5). 모두 준비하면 방장이 시작할 수 있어요.", S_paraTxt);
                GUILayout.FlexibleSpace();
                GUI.enabled = Session.CanReady;
                if (GUILayout.Button(Session.MyReady ? "준비 취소" : "준비 (F5)", Session.MyReady ? S_ctaGhost : S_cta)) Session.ToggleReady();
                GUI.enabled = true;
                return;
            }
            if (ph == Phase.Results)
            {
                GUILayout.Label("결과 대기", S_section);
                GUILayout.Label("다른 사람이 끝나길 기다리는 중이에요.", S_paraTxt);
                return;
            }
            GUILayout.Label($"판정: {Session.JudgeNames[Session.Judge]}", S_sub);
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

            float baseY = r.y + 330, colW = Mathf.Min(220, (r.width - 80) / 3f), cx = r.x + r.width / 2f;
            int[] order = { 2, 1, 3 };              // 2등-1등-3등 배치
            float[] heights = { 250, 222, 206 };
            for (int k = 0; k < 3; k++)
            {
                int rank = order[k];
                float x = cx + (k - 1) * (colW + 16) - colW / 2f, h = heights[rank - 1];
                PodiumCard(new Rect(x, baseY - h, colW, h), rank, 14);
            }

            // 세부 표
            var tr = new Rect(r.x + 20, baseY + 16, r.width - 40, r.yMax - baseY - 100);
            GUILayout.BeginArea(tr);
            ResultsHeader();
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

        // 결과 표 열 (오른쪽부터 고정 폭, 이름이 남는 폭)
        struct ResCols { public float rank, av, name, nameW, acc, xacc, judge, judgeW, miss; }
        static ResCols Cols(Rect row)
        {
            var c = new ResCols();
            float x = row.x;
            c.rank = x + 8; c.av = x + 56; c.name = x + 100;
            c.miss = row.xMax - 64;
            c.judgeW = Mathf.Clamp(row.width * 0.3f, 180, 360); c.judge = c.miss - 24 - c.judgeW;
            c.xacc = c.judge - 24 - 96; c.acc = c.xacc - 12 - 120;
            c.nameW = c.acc - 16 - c.name;
            return c;
        }

        static void ResultsHeader()
        {
            var row = GUILayoutUtility.GetRect(100, 30, GUILayout.ExpandWidth(true));
            var c = Cols(row);
            var st = Label(12, A(C_sub, 0.85f), FontStyle.Bold);
            var str = Label(12, A(C_sub, 0.85f), FontStyle.Bold, TextAnchor.MiddleRight);
            GUI.Label(new Rect(c.rank, row.y, 40, row.height), "#", st);
            GUI.Label(new Rect(c.name, row.y, c.nameW, row.height), "플레이어", st);
            GUI.Label(new Rect(c.acc, row.y, 120, row.height), "정확도", str);
            GUI.Label(new Rect(c.xacc, row.y, 96, row.height), "X정확도", str);
            GUI.Label(new Rect(c.judge, row.y, c.judgeW, row.height), "빠름  ←  판정  →  느림", Label(12, A(C_sub, 0.85f), FontStyle.Bold, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(c.miss, row.y, 56, row.height), "미스", str);
            Fill(new Rect(row.x, row.yMax - 1, row.width, 1), A(C_line, 0.9f));
        }

        static void ResultsTable()
        {
            int rank = 1;
            foreach (var e in Session.Results)
            {
                var s = e.Stats;
                bool me = e.Id == Net.MyId;
                Color rc = RankColor(rank);
                var row = GUILayoutUtility.GetRect(100, 64, GUILayout.ExpandWidth(true));
                var c = Cols(row);
                if (me && Event.current.type == EventType.Repaint) RoundBox(new Rect(row.x, row.y + 4, row.width, row.height - 8), A(C_acc, 0.09f), A(C_acc, 0.35f), 10);

                // 순위 (1~3등은 메달)
                var mr = new Rect(c.rank, row.y + (row.height - 32) / 2f, 32, 32);
                if (rank <= 3) { Dot(mr.center, 16, rc); GUI.Label(mr, rank.ToString(), Label(16, Hex("14171c"), FontStyle.Bold, TextAnchor.MiddleCenter)); }
                else GUI.Label(mr, rank.ToString(), Label(17, C_sub, FontStyle.Bold, TextAnchor.MiddleCenter));

                int slot = Session.Board.FindIndex(x => x.Id == e.Id);
                Avatar(new Rect(c.av, row.y + (row.height - 34) / 2f, 34, 34), e.Name, SlotColor(slot < 0 ? rank - 1 : slot));

                bool forfeit = s.Progress < 0.999f;
                GUI.Label(new Rect(c.name, row.y + (forfeit ? 10 : 0), c.nameW, forfeit ? 24 : row.height), Trim(e.Name, 16) + (me ? $"  <color=#{HexOf(C_acc)}><size=12>나</size></color>" : ""), Label(17, C_txt, FontStyle.Bold));
                if (forfeit) GUI.Label(new Rect(c.name, row.y + 34, c.nameW, 20), $"미완주  ·  {s.Progress * 100:0}% 진행", Label(12, C_fire));

                GUI.Label(new Rect(c.acc, row.y, 120, row.height), $"{s.Acc * 100:0.00}%", Label(20, rank == 1 ? C_gold : C_txt, FontStyle.Bold, TextAnchor.MiddleRight));
                GUI.Label(new Rect(c.xacc, row.y, 96, row.height), $"{s.XAcc * 100:0.00}%", Label(15, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));

                JudgeBar(new Rect(c.judge, row.y + 18, c.judgeW, 8), s);
                GUI.Label(new Rect(c.judge, row.y + 30, c.judgeW, 20), JudgeNums(s), Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));

                int miss = s.Miss + s.Overload;
                GUI.Label(new Rect(c.miss, row.y, 56, row.height), miss.ToString(), Label(17, miss > 0 ? J_too : A(C_sub, 0.6f), FontStyle.Bold, TextAnchor.MiddleRight));
                if (rank < Session.Results.Count) Fill(new Rect(row.x + 8, row.yMax - 1, row.width - 16, 1), A(C_line, 0.6f));
                rank++;
            }
        }

        /// <summary>시상대 카드 한 장 (rank 1~3)</summary>
        static void PodiumCard(Rect r, int rank, float radius)
        {
            var res = Session.Results;
            Color rc = RankColor(rank);
            bool has = rank - 1 < res.Count;
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(r, has ? A(rc, 0.10f) : A(Color.white, 0.02f), has ? A(rc, 0.65f) : A(Color.white, 0.06f), radius);
                if (has) RoundBox(new Rect(r.x + 1, r.y + 1, r.width - 2, 5), A(rc, 0.9f), Color.clear, 3);
            }
            float cx = r.center.x, y = r.y + (rank == 1 && has ? 40 : 22);
            float md = rank == 1 ? 60 : 50;
            var mr = new Rect(cx - md / 2f, y, md, md);
            Dot(mr.center, md / 2f, has ? rc : A(C_sub, 0.25f));
            if (rank == 1 && has) Icons.Draw(new Rect(cx - 13, y - 31, 26, 26), "crown", C_gold);
            GUI.Label(mr, rank.ToString(), Label(rank == 1 ? 28 : 24, Hex("14171c"), FontStyle.Bold, TextAnchor.MiddleCenter));
            y = mr.yMax + 12;
            if (!has) { GUI.Label(new Rect(r.x, y, r.width, 30), "—", Label(20, A(C_sub, 0.5f), FontStyle.Bold, TextAnchor.MiddleCenter)); return; }
            var e = res[rank - 1];
            bool me = e.Id == Net.MyId;
            GUI.Label(new Rect(r.x + 8, y, r.width - 16, 30), Trim(e.Name, 12) + (me ? $" <color=#{HexOf(C_acc)}><size=13>(나)</size></color>" : ""), Label(20, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
            y += 32;
            GUI.Label(new Rect(r.x, y, r.width, 44), $"{e.Stats.Acc * 100:0.00}%", Label(rank == 1 ? 36 : 30, rc, FontStyle.Bold, TextAnchor.MiddleCenter));
            y += 46;
            int miss = e.Stats.Miss + e.Stats.Overload;
            string sub = e.Stats.Progress < 0.999f ? $"미완주 · {e.Stats.Progress * 100:0}% 진행" : $"X {e.Stats.XAcc * 100:0.00}%   ·   {(miss == 0 ? "노 미스" : "미스 " + miss)}";
            if (y + 20 <= r.yMax) GUI.Label(new Rect(r.x, y, r.width, 20), sub, Label(13, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            if (y + 50 <= r.yMax - 10) JudgeBar(new Rect(r.x + 24, r.yMax - 22, r.width - 48, 6), e.Stats);
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
            GUILayout.Label("맵 선택", Label(26, C_txt, FontStyle.Bold));
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
            // 위에 팝업이 있어 막혀 있으면(GUI.enabled false) 손잡이를 잡지 않음
            if (GUI.enabled && ev.type == EventType.MouseDown && ev.button == 0 && hit.Contains(ev.mousePosition) && Tuf.Pgu.Count > 0)
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

        /// <summary>목록 행 배경 (선택이면 강조 테두리 + 왼쪽 띠). 눌렸으면 true</summary>
        static bool RowButton(Rect r, bool sel)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(r, sel ? A(C_acc, 0.12f) : hover ? A(Color.white, 0.05f) : A(Color.white, 0.02f), sel ? A(C_acc, 0.7f) : A(Color.white, hover ? 0.10f : 0.04f), 12);
                if (sel) RoundBox(new Rect(r.x + 6, r.y + 14, 3, r.height - 28), C_acc, Color.clear, 1.5f);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>TUF 결과 행들 (스크롤 뷰 안에서 호출)</summary>
        static void DrawTufRows()
        {
            foreach (var lv in Tuf.Results)
            {
                bool sel = _tufSel == lv;
                var rr = GUILayoutUtility.GetRect(100, 90, GUILayout.ExpandWidth(true));
                rr = new Rect(rr.x, rr.y, rr.width - 6, rr.height - 8);
                if (RowButton(rr, sel)) _tufSel = lv;

                // 난이도 아이콘
                var ir = new Rect(rr.x + 16, rr.y + (rr.height - 50) / 2f, 50, 50);
                var ic = Tuf.Icon(lv.Diff);
                if (ic != null) GUI.DrawTexture(ir, ic, ScaleMode.ScaleToFit);
                else if (!string.IsNullOrEmpty(lv.Diff)) Pill(new Rect(ir.x - 2, ir.y + 13, 54, 24), Esc(Trim(lv.Diff, 6)), lv.DiffColor);

                // 오른쪽: 클리어 · 좋아요
                float rw = 92, rx = rr.xMax - rw - 14;
                IconLabel(new Rect(rx, rr.y + 14, rw, 24), "check", $"{lv.Clears:N0}", Label(15, C_txt, FontStyle.Bold, TextAnchor.MiddleRight), C_ok);
                IconLabel(new Rect(rx, rr.y + 42, rw, 24), "heart", $"{lv.Likes:N0}", Label(15, C_sub, FontStyle.Normal, TextAnchor.MiddleRight), Hex("ff6b81"));

                float tx = ir.xMax + 16, tw = rx - tx - 12;
                GUI.Label(new Rect(tx, rr.y + 9, tw, 26), Esc(Trim(lv.Song, 40)), Label(18, sel ? C_acc : C_txt, FontStyle.Bold));
                GUI.Label(new Rect(tx, rr.y + 34, tw, 20), $"{Esc(Trim(lv.Artist, 30))}{(string.IsNullOrEmpty(lv.Creator) ? "" : $"  <color=#{HexOf(A(C_sub, 0.7f))}>·  제작 {Esc(Trim(lv.Creator, 20))}</color>")}", Label(14, C_sub));
                // 정보 칩: 길이 · BPM · 타일 · #id
                float cx = tx, cy = rr.y + 57;
                void Meta(string icon, string t)
                {
                    if (string.IsNullOrEmpty(t) || cx > rx - 40) return;
                    var st = Label(12, A(C_sub, 0.9f));
                    float w = (icon != null ? 18 : 0) + st.CalcSize(new GUIContent(t)).x + 4;
                    IconLabel(new Rect(cx, cy, w, 18), icon, t, st);
                    cx += w + 14;
                }
                Meta("clock", lv.LengthText);
                Meta("music", lv.Bpm > 0 ? $"{lv.Bpm:0.##} BPM" : "");
                Meta(null, lv.Tiles > 0 ? $"타일 {lv.Tiles:N0}" : "");
                Meta(null, "#" + lv.Id);
            }
            if (Tuf.Results.Count == 0 && !Tuf.Loading && string.IsNullOrEmpty(Tuf.Error))
            {
                GUILayout.Space(40);
                GUILayout.Label("조건에 맞는 레벨이 없어요", Label(18, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUILayout.Label("난이도 범위나 검색어를 바꿔보세요", Label(14, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            if (Tuf.Loading && Tuf.Results.Count == 0)
            {
                GUILayout.Space(40);
                GUILayout.Label("검색 중...", Label(16, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            if (Tuf.HasMore)
            {
                GUILayout.Space(4);
                var o = GUI.enabled; GUI.enabled = o && !Tuf.Loading;
                if (GUILayout.Button(Tuf.Loading ? "불러오는 중..." : "더 보기", S_btn) && Runner.Instance != null) Tuf.Search(Runner.Instance, _tufSent ?? "", true);
                GUI.enabled = o;
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
                var rr = GUILayoutUtility.GetRect(100, 72, GUILayout.ExpandWidth(true));
                rr = new Rect(rr.x, rr.y, rr.width - 6, rr.height - 8);
                if (RowButton(rr, sel)) _mapPath = it.Path;
                var ir = new Rect(rr.x + 16, rr.y + (rr.height - 40) / 2f, 40, 40);
                if (Event.current.type == EventType.Repaint) RoundBox(ir, A(Color.white, 0.04f), A(Color.white, 0.08f), 10);
                Icons.Draw(new Rect(ir.x + 10, ir.y + 10, 20, 20), "music", sel ? C_acc : C_sub);
                float tx = ir.xMax + 14;
                GUI.Label(new Rect(tx, rr.y + 8, rr.xMax - tx - 16, 26), Trim(song, 48), Label(17, sel ? C_acc : C_txt, FontStyle.Bold));
                GUI.Label(new Rect(tx, rr.y + 34, rr.xMax - tx - 16, 20), Trim(sub, 70), Label(13, C_sub));
            }
            if (shown == 0 && !MapBrowser.Scanning)
            {
                GUILayout.Space(40);
                GUILayout.Label(MapBrowser.Items.Count == 0 ? "맵을 못 찾았어요" : "검색 결과가 없어요", Label(18, C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUILayout.Label(MapBrowser.Items.Count == 0 ? "워크샵 · 게임 폴더 · 문서 · 다운로드에서 .adofai 파일을 찾아요" : "곡명 · 아티스트 · 제작자로 찾아보세요", Label(14, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            if (MapBrowser.Scanning) GUILayout.Label("맵 찾는 중...", Label(14, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
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

        // ---------------- 프로필 / 설정 / 나가기 확인 (모달) ----------------
        static void DrawProfile(float W, float H)
        {
            var cr = Modal(520, 340, "프로필", "user", () => _profile = false, "멀티에서 다른 사람에게 보이는 이름");
            if (cr.width <= 0) return;
            string shown = string.IsNullOrWhiteSpace(_nickEdit) ? Net.DefaultName : _nickEdit;
            Avatar(new Rect(cr.x, cr.y, 56, 56), shown, SlotColor(0));
            GUI.Label(new Rect(cr.x + 72, cr.y + 4, cr.width - 72, 26), Trim(shown, 20), Label(20, C_txt, FontStyle.Bold));
            GUI.Label(new Rect(cr.x + 72, cr.y + 30, cr.width - 72, 22), string.IsNullOrWhiteSpace(_nickEdit) ? "Steam 이름 사용 중" : "직접 정한 이름", Label(13, C_sub));
            GUILayout.BeginArea(new Rect(cr.x, cr.y + 76, cr.width, 90));
            SearchField(ref _nickEdit, Net.DefaultName + " (Steam 이름)");
            if (_nickEdit.Length > 20) _nickEdit = _nickEdit.Substring(0, 20);
            GUILayout.Label("비워두면 Steam 이름을 써요. 다음에 방에 들어갈 때부터 보여요.", Label(13, C_sub));
            GUILayout.EndArea();
            int hit = ModalButtons(cr, ("Steam 이름으로", false, true), ("저장", true, true));
            if (hit == 0) { _nickEdit = ""; Profile.Save(""); Session.Notice = "이름을 Steam 이름으로 되돌렸어요"; }
            if (hit == 1) { Profile.Save(_nickEdit); _profile = false; Session.Notice = "이름을 " + Net.MyName + "(으)로 바꿨어요"; }
        }

        static Vector2 _sSettings;
        static void DrawSettings(float W, float H)
        {
            bool custom = Theme.Current == Theme.Custom;
            var cr = Modal(720, custom ? 860 : 640, "설정", "gear", () => _settings = false);
            if (cr.width <= 0) return;
            GUILayout.BeginArea(cr);
            _sSettings = GUILayout.BeginScrollView(_sSettings);

            SettingsHead("테마", "로비·결과 화면의 강조색과 카드 색");
            GUILayout.BeginHorizontal();
            float tw = (cr.width - 3 * 10 - 20) / 4f;
            foreach (var th in Theme.All)
            {
                bool on = th == Theme.Current;
                var tr = GUILayoutUtility.GetRect(tw, 86, GUILayout.Width(tw), GUILayout.Height(86));
                if (RowButton(tr, on)) { Theme.Set(th); _hexFor = -1; }
                if (Event.current.type == EventType.Repaint)
                {
                    RoundBox(new Rect(tr.x + 14, tr.y + 14, tr.width - 28, 30), GetBase(th, 0), A(Color.white, 0.12f), 8);
                    for (int k = 1; k < 5; k++) Dot(new Vector2(tr.x + 30 + (k - 1) * 22, tr.y + 29), 7, GetBase(th, k));
                }
                IconLabel(new Rect(tr.x + 14, tr.y + 52, tr.width - 20, 24), on ? "check" : null, th.Name, Label(14, on ? C_acc : C_txt, on ? FontStyle.Bold : FontStyle.Normal));
                GUILayout.Space(10);
            }
            GUILayout.EndHorizontal();
            if (custom) CustomEditor();

            SettingsHead("업데이트", null);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"현재 v{Main.Version}   ·   {Updater.StatusText}", S_sub);
            GUILayout.FlexibleSpace();
            var o = GUI.enabled;
            GUI.enabled = o && Updater.Current != Updater.State.Checking && Updater.Current != Updater.State.Downloading && Updater.Current != Updater.State.ReadyToRestart;
            if (GUILayout.Button("지금 확인", S_iconBtn, GUILayout.Width(110)) && Runner.Instance != null) Updater.CheckNow(Runner.Instance);
            GUI.enabled = o;
            GUILayout.EndHorizontal();
            if (Updater.Current == Updater.State.Failed) GUILayout.Label(Esc(Updater.Error), Label(13, C_fire, FontStyle.Normal, TextAnchor.UpperLeft, true));

            SettingsHead("받은 맵 캐시", "같은 맵은 다시 받지 않도록 보관해요");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{CacheCount()}개   ·   {CacheSizeMB():0.0}MB", S_sub);
            GUILayout.FlexibleSpace();
            GUI.enabled = o && !Session.InMatch && Session.Phase != Phase.Distributing;
            if (GUILayout.Button("비우기", S_iconBtn, GUILayout.Width(90))) { try { Directory.Delete(MapPack.Root, true); } catch { } _cacheAt = -10; Session.Notice = "받은 맵을 비웠어요"; }
            GUI.enabled = o;
            GUILayout.EndHorizontal();

            SettingsHead("단축키", null);
            ShortcutRow(new[] { "F5" }, "준비 / 준비 취소 (맵을 받은 뒤)");
            ShortcutRow(new[] { "Esc" }, "뒤로 · 창 닫기 · 방 나가기");
            GUILayout.Space(4);
            GUILayout.Label("멀티 섬은 메인 섬의 '멀티' 포탈로 들어가요. 멀티 중에는 무적 모드가 고정돼요.", Label(13, A(C_sub, 0.8f), FontStyle.Normal, TextAnchor.UpperLeft, true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void SettingsHead(string title, string sub)
        {
            GUILayout.Space(14);
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, Label(16, C_txt, FontStyle.Bold), GUILayout.ExpandWidth(false));
            if (!string.IsNullOrEmpty(sub)) { GUILayout.Space(10); GUILayout.Label(sub, Label(13, C_sub)); }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        static void ShortcutRow(string[] keys, string text)
        {
            var r = GUILayoutUtility.GetRect(100, 32, GUILayout.ExpandWidth(true));
            float x = r.x, cy = r.center.y;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) { GUI.Label(new Rect(x + 2, r.y, 14, r.height), "+", Label(13, C_sub, FontStyle.Normal, TextAnchor.MiddleCenter)); x += 18; }
                x = Keycap(x, cy, keys[i]) + 2;
            }
            GUI.Label(new Rect(r.x + 170, r.y, r.width - 170, r.height), text, Label(14, C_txt));
        }

        static void CustomEditor()
        {
            var t = Theme.Custom;
            GUILayout.Space(10);
            // 편집할 색 고르기
            GUILayout.BeginHorizontal();
            float cw = 112;
            for (int i = 0; i < 5; i++)
            {
                bool on = i == _editIdx;
                var cr = GUILayoutUtility.GetRect(cw, 64, GUILayout.Width(cw), GUILayout.Height(64));
                if (RowButton(cr, on)) { _editIdx = i; _hexFor = -1; }
                if (Event.current.type == EventType.Repaint)
                    RoundBox(new Rect(cr.x + 12, cr.y + 10, cr.width - 24, 22), GetBase(t, i), A(Color.white, 0.15f), 6);
                GUI.Label(new Rect(cr.x + 12, cr.y + 36, cr.width - 16, 20), EditNames[i], Label(12, on ? C_acc : C_sub, on ? FontStyle.Bold : FontStyle.Normal));
                GUILayout.Space(6);
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
            if (GUILayout.Button("기본에서 시작", S_iconBtn, GUILayout.Width(130))) CopyToCustom(Theme.Slate);
            if (GUILayout.Button("불과 얼음에서 시작", S_iconBtn, GUILayout.Width(160))) CopyToCustom(Theme.FireIce);
            GUILayout.EndHorizontal();
            GUILayout.Label("나머지 색(테두리, 보조 글자, 빛 등)은 이 5색에서 자동으로 맞춰져요.", Label(13, C_sub));
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
            var cr = Modal(500, 260, "방 나가기", "exit", () => _confirmLeave = false);
            if (cr.width <= 0) return;
            GUI.Label(new Rect(cr.x, cr.y, cr.width, 60), Net.IsHost
                ? "방장이 나가면 방이 사라지고 모두 로비 밖으로 나가요."
                : "나간 뒤 다시 들어오려면 공개 방 목록이나 참가 코드로 들어와야 해요.", Label(15, C_sub, FontStyle.Normal, TextAnchor.UpperLeft, true));
            int hit = ModalButtonsSame(cr, ("취소", S_btn), ("방 나가기", S_btn), ("메인 메뉴로", S_btnDanger));
            if (hit == 0) _confirmLeave = false;
            if (hit == 1) { _confirmLeave = false; Session.LeaveRoom(); ShowResults = false; }
            if (hit == 2) { _confirmLeave = false; Session.LeaveRoom(); Island.Leave(); }
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
                Banner("모두 들어오면 바로 시작해요...");
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

        }

        static void Banner(string text)
        {
            var st = Label(20, C_txt, FontStyle.Bold);
            float w = st.CalcSize(new GUIContent(text)).x + 120, h = 64;
            var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.1f, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(new Rect(r.x, r.y + 4, r.width, r.height), A(Color.black, 0.35f), Color.clear, h / 2f);
                RoundBox(r, A(Hex("14171c"), 0.94f), A(Color.white, 0.10f), h / 2f);
            }
            Orbit(new Vector2(r.x + 40, r.center.y), 9, 5.5f, 1.4f);
            GUI.Label(new Rect(r.x + 66, r.y, r.width - 80, h), text, st);
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
