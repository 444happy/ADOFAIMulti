using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// UI 설계판에서 만든 배치(Layout)를 그대로 그리는 부분.
    /// 위치·크기·색·둥글기·글자는 설계 그대로, 동작은 각 부품의 "기능"(func)에 연결.
    /// 글자 맨 앞에 {i:아이콘} 을 쓰면 아이콘이 붙음 (예: "{i:gear} 설정").
    /// </summary>
    public static partial class UI
    {
        static float _ls = 1, _lox, _loy;
        static bool _lBlocked;                         // 위에 팝업이 떠 있으면 아래 화면은 입력을 받지 않음
        static Rect _lSortBtn; static bool _lSortShown;
        static string _lSortScreen;                    // 정렬 메뉴가 붙은 화면 (지난 프레임 기준)
        static readonly Color C_stageBg = new Color32(0x0d, 0x0f, 0x12, 0xff);

        static void LBegin()
        {
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            _ls = Mathf.Min(W / 1920f, H / 1080f);
            _lox = (W - 1920f * _ls) / 2f; _loy = (H - 1080f * _ls) / 2f;
        }
        static Rect LR(Layout.El e) => new Rect(_lox + e.X * _ls, _loy + e.Y * _ls, e.W * _ls, e.H * _ls);
        static int LS(float v) => Mathf.Max(1, Mathf.RoundToInt(v * _ls));

        // ---------------- 상자 (배경색 + 테두리 + 둥글기) ----------------
        static readonly Dictionary<string, GUIStyle> _lBox = new Dictionary<string, GUIStyle>();
        static int _lBoxBuilt = -1;

        static void LBox(Rect r, Color fill, Color stroke, float radius)
        {
            if (fill.a <= 0.001f && stroke.a <= 0.001f) return;
            if (Event.current.type != EventType.Repaint) return;
            if (_lBoxBuilt != _builtVer) { _lBox.Clear(); _lBoxBuilt = _builtVer; }
            int rr = Mathf.Clamp(LS(radius), 1, Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(r.width, r.height) / 2f)));
            rr = Mathf.Min(rr, 48);
            string key = ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(stroke) + rr;
            if (!_lBox.TryGetValue(key, out var st) || st.normal.background == null)
            {
                var fillC = fill.a <= 0.001f ? new Color(stroke.r, stroke.g, stroke.b, 0) : fill;
                var tex = Round(fillC, rr, stroke.a > 0.001f ? (Color?)stroke : null);
                st = new GUIStyle { border = new RectOffset(rr + 1, rr + 1, rr + 1, rr + 1) };
                st.normal.background = tex;
                _lBox[key] = st;
            }
            st.Draw(r, false, false, false, false);
        }

        static TextAnchor LAnchor(Layout.El e, bool top = false)
        {
            if (top) return e.Align == "center" ? TextAnchor.UpperCenter : e.Align == "right" ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            return e.Align == "center" ? TextAnchor.MiddleCenter : e.Align == "right" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }
        static GUIStyle LStyle(Layout.El e, Color? col = null, bool top = false) =>
            Label(LS(e.Size), col ?? e.Color, e.Weight >= 700 ? FontStyle.Bold : FontStyle.Normal, LAnchor(e, top));

        static void LText(Layout.El e, Rect r, string text, Color? col = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool top = e.Type == "panel" || e.Type == "list" || e.Type == "ranking";
            float px = (top ? 20 : e.Type == "text" ? 0 : 14) * _ls, py = top ? 16 * _ls : 0;
            var tr = new Rect(r.x + px, r.y + py, r.width - px * 2, r.height - py * 2);
            if (top) tr.height = Mathf.Min(tr.height, LS(e.Size) * 1.5f * (1 + text.Count(c => c == '\n')));
            text = SplitIcon(text, out var icon);
            IconLabel(tr, icon, text, LStyle(e, col, top));
        }

        // ---------------- 글자 속 표시 값 ----------------
        static readonly Regex _tokRe = new Regex(@"\{(\w+)\}");

        /// <summary>{name} 값. 모르는 이름이면 null</summary>
        static string TokVal(string name)
        {
            bool noMap = Session.MapHash == null;
            var res = Session.Results;
            switch (name)
            {
                case "code": return Net.InviteCode ?? "";
                case "judge": return Session.JudgeNames[Session.Judge];
                case "roomName": return Net.RoomName ?? "";
                case "myName": return Net.MyName;
                case "song": return noMap ? (Net.IsHost ? "선택한 맵 없음" : "방장이 맵을 고르는 중") : (string.IsNullOrEmpty(Session.MapSong) ? Session.MapTitle : Session.MapSong);
                case "artist": return noMap ? "" : Session.MapArtist ?? "";
                case "creator": return noMap ? "" : Session.MapAuthor ?? "";
                case "diff": return noMap ? "" : Session.MapDiff ?? "";
                case "tufId": return !noMap && Session.MapSource == 1 ? Session.MapTufId.ToString() : "";
                case "source": return noMap ? "" : Session.MapSource == 1 ? "TUF #" + Session.MapTufId : "내 컴퓨터 맵";
                case "size": return noMap || Session.MapSize <= 0 ? "" : $"{Session.MapSize / 1048576f:0.0}MB";
                case "players": return Session.Board.Count.ToString();
                case "notice": return Session.Notice ?? "";
                case "hostName": return Session.Board.FirstOrDefault(x => x.Id == Net.HostId)?.Name ?? "";
                case "roomVis": return Net.RoomPublic ? "공개 방" : "비공개 방";
                case "phase": return PhaseText();
                case "readyCount": return $"{Session.ReadyCount}/{Session.ReadyNeeded}";
                case "roomCount":
                    if (!string.IsNullOrEmpty(RoomList.Error)) return RoomList.Error;
                    return !RoomList.Loaded ? "불러오는 중..." : RoomList.Rooms.Count == 0 ? "열린 방 없음" : $"{RoomList.Rooms.Count}개 방";
                case "dlStatus": return DlStatus(out _);
                case "dlPct": { DlStatus(out float p); return p >= 0 ? $"{p * 100:0}%" : ""; }
                case "r1name": case "r2name": case "r3name": { int i = name[1] - '1'; return i < res.Count ? Trim(res[i].Name, 12) : "-"; }
                case "r1acc": case "r2acc": case "r3acc": { int i = name[1] - '1'; return i < res.Count ? $"{res[i].Stats.Acc * 100:0.00}%" : ""; }
            }
            return null;
        }

        /// <summary>설계 글자의 {값}을 게임 값으로. 글자에 든 값이 모두 비어 있으면 (예: "제작 {creator}"인데 제작자 없음) 통째로 숨김</summary>
        static string Tok(string t)
        {
            if (string.IsNullOrEmpty(t) || t.IndexOf('{') < 0) return Esc(t);
            bool known = false, any = false;
            string res = _tokRe.Replace(Esc(t), m =>
            {
                var v = TokVal(m.Groups[1].Value);
                if (v == null) return m.Value;
                known = true;
                if (v.Length > 0) any = true;
                return Esc(v);
            });
            return known && !any ? "" : res;
        }

        /// <summary>맵 받기 진행 글자 + 비율(0~1, 없으면 -1)</summary>
        static string DlStatus(out float p)
        {
            p = -1;
            var parts = Session.Board.Where(x => x.State != PState.Cancelled).ToList();
            if (Session.Phase == Phase.Packing)
            {
                p = Mathf.Max(0, Session.HostFetchProgress);
                return Session.HostFetchProgress >= 0 ? "방장이 TUF에서 맵을 받는 중" : "맵 준비 중...";
            }
            if (Session.Phase == Phase.Distributing)
            {
                int have = parts.Count(x => x.State >= PState.HasMap);
                p = parts.Count == 0 ? 0 : parts.Average(x => x.State >= PState.HasMap ? 1f : x.Download);
                return have >= parts.Count ? $"모두 맵을 받았어요 · 준비 {Session.ReadyCount}/{Session.ReadyNeeded}" : $"맵 받는 중 · {have}/{parts.Count}명 완료";
            }
            if (Session.MapHash == null) return "";
            p = 1;
            return Session.InMatch ? "게임 진행 중" : "맵 준비됨";
        }

        // ---------------- 공용 조작 ----------------
        static bool LClick(Rect r, bool enabled = true, float radius = 8)
        {
            bool hover = enabled && !_lBlocked && r.Contains(Event.current.mousePosition);
            if (hover && Event.current.type == EventType.Repaint) LBox(r, A(Color.white, GUIUtility.hotControl != 0 ? 0.10f : 0.06f), Color.clear, radius);
            var o = GUI.enabled; GUI.enabled = enabled && !_lBlocked && o;
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.enabled = o;
            return hit;
        }

        static void LButton(Layout.El e, Rect r, bool enabled, System.Action onClick, string text = null)
        {
            var oc = GUI.color;
            if (!enabled) GUI.color = A(oc, oc.a * 0.4f);
            LBox(r, e.Fill, e.Stroke, e.Radius);
            LText(e, r, text ?? Tok(e.Text));
            GUI.color = oc;
            if (LClick(r, enabled, e.Radius)) onClick?.Invoke();
        }

        static void LToggle(Layout.El e, Rect r, bool on, System.Action onClick, bool enabled = true)
        {
            var oc = GUI.color;
            if (!enabled) GUI.color = A(oc, oc.a * 0.5f);
            if (on) LBox(r, A(C_acc, 0.18f), C_acc, e.Radius); else LBox(r, Hex("1f242c"), Hex("323a46"), e.Radius);
            LText(e, r, Tok(e.Text), on ? C_acc : Hex("c9d0da"));
            GUI.color = oc;
            if (LClick(r, enabled, e.Radius)) onClick?.Invoke();
        }

        static readonly Dictionary<string, GUIStyle> _lField = new Dictionary<string, GUIStyle>();
        static string LInput(Layout.El e, Rect r, string value, string placeholder, int max = 40, bool password = false, Rect? box = null)
        {
            var br = box ?? r;
            bool focus = GUI.GetNameOfFocusedControl() == "lin" + e.Id;
            LBox(br, e.Fill, focus ? A(C_acc, 0.8f) : e.Stroke, e.Radius);
            string key = LS(e.Size) + "|" + e.Weight;
            if (!_lField.TryGetValue(key, out var st) || st.font != _font)
            {
                st = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = LS(e.Size), fontStyle = e.Weight >= 700 ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(LS(16), LS(16), 0, 0), clipping = TextClipping.Clip };
                st.normal.background = st.focused.background = st.hover.background = st.active.background = null;
                st.normal.textColor = st.focused.textColor = st.hover.textColor = C_txt;
                _lField[key] = st;
            }
            var o = GUI.enabled; GUI.enabled = !_lBlocked && o;
            GUI.SetNextControlName("lin" + e.Id);
            value = password ? GUI.PasswordField(r, value ?? "", '*', max, st) : GUI.TextField(r, value ?? "", max, st);
            GUI.enabled = o;
            if (string.IsNullOrEmpty(value) && Event.current.type == EventType.Repaint)
                GUI.Label(new Rect(r.x + LS(16), r.y, r.width - LS(32), r.height), placeholder, Label(LS(e.Size), A(e.Color, 0.9f), FontStyle.Normal, TextAnchor.MiddleLeft));
            return value;
        }

        static void LArea(Rect r, float pad, System.Action body)
        {
            var o = GUI.enabled; GUI.enabled = !_lBlocked && o;
            GUILayout.BeginArea(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2));
            body();
            GUILayout.EndArea();
            GUI.enabled = o;
        }

        // ---------------- 화면 그리기 ----------------
        static void DrawLayout(Layout.Screen s, bool background)
        {
            LBegin();
            if (background) Fill(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), C_stageBg);
            _lSortShown = false;

            // 정렬 메뉴가 열려 있으면 아래 부품보다 먼저 마우스를 판정:
            // IMGUI는 먼저 그린 부품이 클릭을 가져가므로, 메뉴 위의 클릭이 아래 맵 목록으로 새지 않게 막는다
            var ev = Event.current;
            bool menuHere = _tufSortOpen && _lSortScreen == s.Id && !_lBlocked;
            bool saved = _lBlocked;
            if (menuHere && ev.isMouse || menuHere && ev.type == EventType.ScrollWheel)
            {
                var box = SortMenuBox();
                if (box.Contains(ev.mousePosition))
                {
                    _lBlocked = true;                              // 아래 부품은 이 이벤트를 못 받음
                    if (ev.type == EventType.ScrollWheel) ev.Use();
                }
                else if (ev.type == EventType.MouseDown && !_lSortBtn.Contains(ev.mousePosition))
                {
                    _tufSortOpen = false; ev.Use();                // 메뉴 밖 클릭은 닫기만
                }
            }

            // 화면 전체가 막혀 있으면(위에 팝업·서랍) 휠로 아래 목록이 같이 스크롤되지 않게 위치를 되돌림
            bool keepScroll = _lBlocked && ev.type == EventType.ScrollWheel;
            Vector2 k1 = _sRooms, k2 = _sTuf, k3 = _sMaps, k4 = _sRes;

            foreach (var e in s.Els)
            {
                var oc = GUI.color;
                if (e.Opacity < 0.999f) GUI.color = A(oc, oc.a * Mathf.Clamp01(e.Opacity));
                DrawEl(e, LR(e));
                GUI.color = oc;
            }
            if (keepScroll) { _sRooms = k1; _sTuf = k2; _sMaps = k3; _sRes = k4; }
            _lBlocked = saved;
            if (_lSortShown) _lSortScreen = s.Id; else if (_lSortScreen == s.Id) _lSortScreen = null;
            if (_lSortShown && _tufSortOpen) DrawSortMenu();
        }

        static Rect SortMenuBox()
        {
            float w = 150 * _ls, h = 40 * _ls;
            return new Rect(_lSortBtn.x, _lSortBtn.yMax + 6 * _ls, w * 2 + 12 * _ls, h * 5 + 12 * _ls);
        }

        static void DrawEl(Layout.El e, Rect r)
        {
            bool host = Net.IsHost, inRoom = Session.InRoom;
            bool tufTab = _drawerTab == 1;
            bool busy = Session.Phase == Phase.Packing || (Session.InMatch && Session.Phase != Phase.Results);
            switch (e.Func)
            {
                // ---- 공통 ----
                case "로고": DrawLogo(e, r); return;
                case "방 나가기": LButton(e, r, true, AskLeave); return;
                case "설정 열기": LButton(e, r, true, () => { _settings = !_settings; _profile = false; }); return;
                case "프로필 열기": LButton(e, r, true, () => { _profile = !_profile; _settings = false; _nickEdit = Profile.Nick; }); return;
                case "테마 바꾸기": LButton(e, r, true, () => { _settings = true; _profile = false; }); return;
                case "업데이트 확인": LButton(e, r, Runner.Instance != null, () => Updater.CheckNow(Runner.Instance)); return;
                case "알림 메시지":
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    if (inRoom)
                    {
                        float head = string.IsNullOrEmpty(e.Text) ? 0 : LS(e.Size) * 1.6f + LS(8);
                        LText(e, r, Tok(e.Text));
                        StatusPanel(new Rect(r.x + LS(20), r.y + head + LS(10), r.width - LS(40), r.height - head - LS(26)));
                    }
                    else LText(e, r, Esc(string.IsNullOrEmpty(Session.Notice) ? Net.Status : Session.Notice));
                    return;

                // ---- 입구 ----
                case "공개 방 목록":
                    RoomList.Tick(Runner.Instance);
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    LArea(r, LS(12), () =>
                    {
                        _sRooms = GUILayout.BeginScrollView(_sRooms);
                        if (RoomList.Loaded && RoomList.Rooms.Count == 0)
                        {
                            GUILayout.Space(LS(150));
                            var ir = GUILayoutUtility.GetRect(10, LS(56), GUILayout.ExpandWidth(true));
                            Icons.Draw(new Rect(ir.center.x - LS(28), ir.y, LS(56), LS(56)), "users", A(C_sub, 0.5f));
                            GUILayout.Space(LS(14));
                            GUILayout.Label("지금 열린 공개 방이 없어요", Label(LS(22), C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                        }
                        else if (!RoomList.Loaded)
                        {
                            GUILayout.Space(LS(150));
                            GUILayout.Label(string.IsNullOrEmpty(RoomList.Error) ? "방 목록을 불러오는 중..." : Esc(RoomList.Error), Label(LS(16), C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
                        }
                        foreach (var rm in RoomList.Rooms) DrawRoomCard(rm);
                        GUILayout.EndScrollView();
                    });
                    return;
                case "공개 방 새로고침":
                    LButton(e, r, !RoomList.Loading, RoomList.RefreshNow, RoomList.Loading ? "{i:refresh} 불러오는 중" : null);
                    return;
                case "방 이름 입력":
                    _roomName = LInput(e, r, _roomName, string.IsNullOrEmpty(e.Text) ? Esc(Net.MyName) + "의 방" : Tok(e.Text)); return;
                case "공개/비공개 전환":
                {
                    bool wantPrivate = (e.Text ?? "").Contains("비공개");
                    bool pub = inRoom ? Net.RoomPublic : _newPublic;
                    bool can = !inRoom || host;
                    LToggle(e, r, wantPrivate ? !pub : pub, () =>
                    {
                        if (inRoom) { if (wantPrivate) AskSetPassword(); else if (!Net.RoomPublic) { Net.SetPublic(true); Session.Notice = "공개 방이 됐어요"; } }
                        else _newPublic = !wantPrivate;
                    }, can);
                    return;
                }
                case "방 만들기": LButton(e, r, !inRoom, TryCreateRoom); return;
                case "비밀번호 입력":
                    if (inRoom || _newPublic) return;                // 비공개를 골랐을 때만 보임
                {
                    float ew = Mathf.Min(r.height, 40) + 4;
                    _roomPw = LInput(e, new Rect(r.x, r.y, r.width - ew, r.height), _roomPw, Tok(e.Text), 32, !_pwShowCreate, r);
                    var o = GUI.enabled; GUI.enabled = !_lBlocked && o;
                    EyeButton(r, ref _pwShowCreate);
                    GUI.enabled = o;
                    return;
                }
                case "방 코드로 참가":
                    if (e.Type == "input") _joinId = Net.NormalizeCode(LInput(e, r, _joinId, Tok(e.Text), 8));
                    else LButton(e, r, Net.IsValidCode(_joinId), () => Net.JoinByCode(_joinId));
                    return;

                // ---- 로비 ----
                case "방 코드 복사":
                    if (!host && string.IsNullOrEmpty(Net.InviteCode)) return;
                    LButton(e, r, !Net.InviteBusy, InviteClick,
                        string.IsNullOrEmpty(Net.InviteCode) ? (Net.InviteBusy ? "{i:refresh} 만드는 중..." : "{i:plus} 참가 코드 만들기") : "{i:copy} 코드  " + Net.InviteCode);
                    return;
                case "판정 바꾸기":
                {
                    // 지금 판정 아이콘 하나. 누를 때마다 보통 → 엄격 → 느슨 → 보통
                    // 테두리 없이 아이콘만 칸을 꽉 채움 (마우스를 올리면 살짝 커짐)
                    int j = Session.Judge;
                    var oc = GUI.color;
                    bool hov = host && !_lBlocked && r.Contains(Event.current.mousePosition);
                    float s = Mathf.Min(r.width, r.height) * (hov ? 1.06f : 1f);
                    JudgeIcon(new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s), j);
                    GUI.color = oc;
                    if (hov)
                    {
                        string tip = Session.JudgeNames[j] + "  ·  눌러서 바꾸기";
                        var ts = Label(LS(13), C_txt, FontStyle.Bold, TextAnchor.MiddleCenter);
                        float tw = ts.CalcSize(new GUIContent(tip)).x + LS(20);
                        var tr = new Rect(r.center.x - tw / 2f, r.y - LS(32), tw, LS(26));
                        LBox(tr, Hex("0d0f12"), Hex("3a4452"), 8);
                        GUI.Label(tr, tip, ts);
                    }
                    // LClick 은 마우스를 올리면 네모 칸을 그려서, 여기서는 칸 없이 누르기만 받음
                    var og = GUI.enabled; GUI.enabled = host && !_lBlocked && og;
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Session.HostSetJudge(j == 1 ? 2 : j == 2 ? 0 : 1);
                    GUI.enabled = og;
                    return;
                }
                case "판정 설정":
                {
                    // 글자 대신 얼불춤 판정 아이콘 (글자는 어느 판정인지 고르는 데만 씀)
                    string t = e.Text ?? ""; int j = t.Contains("느슨") ? 0 : t.Contains("엄격") ? 2 : 1;
                    bool on = Session.Judge == j;
                    var oc = GUI.color;
                    if (!host) GUI.color = A(oc, oc.a * 0.6f);
                    if (on) LBox(r, A(C_acc, 0.18f), C_acc, e.Radius); else LBox(r, Hex("1f242c"), Hex("323a46"), e.Radius);
                    float s = Mathf.Min(r.width, r.height) * 0.68f;
                    var oi = GUI.color; if (!on) GUI.color = A(oi, oi.a * 0.4f);
                    JudgeIcon(new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s), j);
                    GUI.color = oc;
                    // 마우스를 올리면 이름 표시
                    if (!_lBlocked && r.Contains(Event.current.mousePosition))
                    {
                        var ts = Label(LS(13), C_txt, FontStyle.Bold, TextAnchor.MiddleCenter);
                        float tw = ts.CalcSize(new GUIContent(Session.JudgeNames[j])).x + LS(20);
                        var tr = new Rect(r.center.x - tw / 2f, r.y - LS(32), tw, LS(26));
                        LBox(tr, Hex("0d0f12"), Hex("3a4452"), 8);
                        GUI.Label(tr, Session.JudgeNames[j], ts);
                    }
                    if (LClick(r, host, e.Radius)) Session.HostSetJudge(j);
                    return;
                }
                case "Steam 초대": if (Net.CanInvite) LButton(e, r, true, Net.InviteOverlay); return;
                case "플레이어 명단": LBox(r, e.Fill, e.Stroke, e.Radius); { var o = GUI.enabled; GUI.enabled = !_lBlocked && o; DrawLineup(r, false); GUI.enabled = o; } return;
                case "맵 고르기 열기": case "맵 선택 열기":
                    if (host) LButton(e, r, !busy, () => { _drawer = true; _drawerTab = 1; });
                    return;
                case "한 판 더":
                    if (host && !Game.IsScnGame && Session.MapHash != null && !string.IsNullOrEmpty(Session.HostLevelPath) && (Session.Phase == Phase.Lobby || Session.Phase == Phase.Results || ShowResults))
                        LButton(e, r, true, () => { ShowResults = false; Session.HostRedo(); });
                    return;
                case "메인 버튼": case "메인 버튼 (상황별)": DrawMainButton(e, r); return;
                case "준비 (F5)":
                    if (host) LButton(e, r, Session.CanStart, Session.HostStart, Session.Phase == Phase.Distributing ? $"시작  {Session.ReadyCount}/{Session.ReadyNeeded} 준비" : "시작");
                    else LButton(e, r, Session.CanReady, Session.ToggleReady, Session.MyReady ? "{i:check} 준비 완료 · 취소" : null);
                    return;
                case "게임 시작": if (host) LButton(e, r, Session.CanStart, Session.HostStart, Session.Phase == Phase.Distributing ? $"{Tok(e.Text)}  {Session.ReadyCount}/{Session.ReadyNeeded}" : null); return;
                case "받기 일시정지/재개":
                    if (!host && Session.Phase == Phase.Distributing && Session.DlActive)
                        LButton(e, r, true, () => { if (Session.DlPaused) Session.DlResume(); else Session.DlPause(); }, Session.DlPaused ? "{i:play} 이어 받기" : "{i:pause} 일시정지");
                    return;
                case "받기 취소":
                    if (!host && Session.Phase == Phase.Distributing && Session.MapSize > 0)
                    {
                        if (Session.DlCancelled) LButton(e, r, true, Session.DlRetry, "{i:download} 다시 받기");
                        else if (Session.DlActive) LButton(e, r, true, Session.DlCancel, "{i:close} 취소하고 관전");
                    }
                    return;
                case "채팅": DrawChat(e, r); return;
                case "진행 단계 표시": LBox(r, e.Fill, e.Stroke, e.Radius); DrawStepperL(r); return;
                case "맵 받기 진행률":
                {
                    DlStatus(out float p);
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    if (p > 0.001f) LBox(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(p)), r.height), p >= 0.999f && Session.Phase == Phase.Distributing ? C_ok : e.Color, Color.clear, e.Radius);
                    return;
                }
                case "맵 난이도":
                    if (string.IsNullOrEmpty(Session.MapDiff) || Session.MapHash == null) return;
                    { var c = Session.MapDiffColor; LBox(r, A(c, 0.2f), c, e.Radius); LText(e, r, Tok(e.Text.Length > 0 ? e.Text : "{diff}"), c); }
                    return;
                case "맵 난이도 아이콘":
                {
                    var ic = string.IsNullOrEmpty(Session.MapDiff) || Session.MapHash == null ? null : Tuf.Icon(Session.MapDiff);
                    if (ic != null)
                    {
                        LBox(r, A(Session.MapDiffColor, 0.08f), A(Session.MapDiffColor, 0.35f), e.Radius);
                        float pad = r.width * 0.12f;
                        GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2), ic, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        LBox(r, e.Fill, e.Stroke, e.Radius);
                        float s = Mathf.Min(r.width, r.height) * 0.3f;
                        if (!string.IsNullOrEmpty(Session.MapDiff) && Session.MapHash != null)
                            GUI.Label(r, Esc(Session.MapDiff), Label(LS(40), Session.MapDiffColor, FontStyle.Bold, TextAnchor.MiddleCenter));
                        else
                        {
                            Icons.Draw(new Rect(r.center.x - s / 2f, r.center.y - s / 2f - LS(12), s, s), "music", A(C_sub, 0.45f));
                            GUI.Label(new Rect(r.x, r.center.y + s / 2f - LS(4), r.width, LS(28)), Session.MapHash == null ? "맵 없음" : "내 컴퓨터 맵", Label(LS(15), A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter));
                        }
                    }
                    return;
                }

                // ---- 맵 고르기 ----
                case "닫기": if (e.Type == "panel") { LBox(r, e.Fill, e.Stroke, e.Radius); if (LClick(r, true, 0)) _drawer = false; } else LButton(e, r, true, () => _drawer = false); return;
                case "TUF 탭": LToggle(e, r, tufTab, () => _drawerTab = 1); return;
                case "내 컴퓨터 맵 목록":
                    if (e.Type == "list")
                    {
                        if (tufTab) return;
                        LBox(r, e.Fill, e.Stroke, e.Radius);
                        LArea(r, LS(8), () => { _sMaps = GUILayout.BeginScrollView(_sMaps, GUILayout.ExpandHeight(true)); DrawLocalRows(); GUILayout.EndScrollView(); });
                    }
                    else LToggle(e, r, !tufTab, () => { _drawerTab = 0; MapBrowser.ScanOnce(); });
                    return;
                case "TUF 검색":
                    if (e.Type == "input")
                    {
                        if (tufTab) { var before = _tufQuery; _tufQuery = LInput(e, r, _tufQuery, Tok(e.Text), 60); if (_tufQuery != before) _tufTypedAt = Time.unscaledTime; TufSearchTick(); }
                        else _search = LInput(e, r, _search, "곡명 · 아티스트 · 제작자 검색", 60);
                    }
                    else
                    {
                        LBox(r, e.Fill, e.Stroke, e.Radius);
                        if (tufTab) TufSearchTick();
                        LArea(r, LS(10), () =>
                        {
                            if (tufTab)
                            {
                                string info = Tuf.Loading && Tuf.Results.Count == 0 ? "검색 중..." : !string.IsNullOrEmpty(Tuf.Error) ? Tuf.Error : $"<b>{Tuf.Total:N0}</b>개   ·   {Tuf.MinName} ~ {Tuf.MaxName}   ·   {Tuf.SortName} {(Tuf.Asc ? "낮은 순" : "높은 순")}";
                                GUILayout.Label(info, Label(13, C_sub));
                                GUILayout.Space(4);
                                _sTuf = GUILayout.BeginScrollView(_sTuf, GUILayout.ExpandHeight(true));
                                DrawTufRows();
                                GUILayout.EndScrollView();
                            }
                            else
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label($"<b>{MapBrowser.Items.Count:N0}</b>개   ·   워크샵 · 게임 폴더 · 문서 · 다운로드", Label(13, C_sub));
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(MapBrowser.Scanning ? "찾는 중..." : "다시 찾기", S_iconBtn, GUILayout.Width(100))) MapBrowser.Rescan();
                                GUILayout.EndHorizontal();
                                GUILayout.Space(4);
                                _sMaps = GUILayout.BeginScrollView(_sMaps, GUILayout.ExpandHeight(true));
                                DrawLocalRows();
                                GUILayout.EndScrollView();
                            }
                        });
                    }
                    return;
                case "난이도 범위 필터": if (tufTab) { LBox(r, e.Fill, e.Stroke, e.Radius); var o = GUI.enabled; GUI.enabled = !_lBlocked; TufDiffRange(r); GUI.enabled = o; } return;
                case "미평가 포함": if (tufTab) LToggle(e, r, Tuf.IncUnranked, () => { Tuf.IncUnranked = !Tuf.IncUnranked; _tufDirty = true; }); return;
                case "Q 포함": if (tufTab) LToggle(e, r, Tuf.IncQ, () => { Tuf.IncQ = !Tuf.IncQ; _tufDirty = true; }); return;
                case "기타 포함": if (tufTab) LToggle(e, r, Tuf.IncEtc, () => { Tuf.IncEtc = !Tuf.IncEtc; _tufDirty = true; }); return;
                case "정렬 바꾸기":
                    if (!tufTab) return;
                    if ((e.Text ?? "").Contains("높은")) LToggle(e, r, !Tuf.Asc, () => { if (Tuf.Asc) { Tuf.Asc = false; _tufDirty = true; } });
                    else if ((e.Text ?? "").Contains("낮은")) LToggle(e, r, Tuf.Asc, () => { if (!Tuf.Asc) { Tuf.Asc = true; _tufDirty = true; } });
                    else
                    {
                        LButton(e, r, true, () => _tufSortOpen = !_tufSortOpen, Esc(Tuf.SortName));
                        Icons.Draw(new Rect(r.xMax - LS(30), r.center.y - LS(8), LS(16), LS(16)), _tufSortOpen ? "back" : "down", C_sub);
                        _lSortBtn = r; _lSortShown = true;
                    }
                    return;
                case "이 맵으로 시작":
                    if (tufTab) LButton(e, r, !busy && _tufSel != null && _tufSel.CanDownload, () => { _drawer = false; ShowResults = false; Session.HostDistributeTuf(_tufSel); },
                        _tufSel != null ? null : "{i:music} 목록에서 맵을 고르세요");
                    else LButton(e, r, !busy && !string.IsNullOrEmpty(_mapPath), () => { _drawer = false; ShowResults = false; Session.HostDistribute(_mapPath.Trim().Trim('"')); },
                        string.IsNullOrEmpty(_mapPath) ? "{i:music} 목록에서 맵을 고르세요" : null);
                    return;

                // ---- 인게임 · 결과 ----
                case "실시간 순위표": DrawRanking(e, r); return;
                case "카운트다운":
                    if (Session.Phase == Phase.Countdown)
                    {
                        float left = Session.CountdownLeft;
                        int n = Mathf.CeilToInt(left);
                        float frac = n > 0 ? left - (n - 1) : 0;          // 1 → 0 으로 줄어듦
                        float pop = 1 + 0.35f * Mathf.Pow(Mathf.Clamp01(frac - 0.6f) / 0.4f, 2);
                        var oc = GUI.color; GUI.color = A(oc, oc.a * Mathf.Clamp01(0.35f + frac));
                        GUI.Label(new Rect(r.x - r.width, r.y - r.height, r.width * 3, r.height * 3), n > 0 ? n.ToString() : "GO!",
                            Label(Mathf.RoundToInt(LS(e.Size) * pop), n > 0 ? e.Color : C_acc, FontStyle.Bold, TextAnchor.MiddleCenter));
                        GUI.color = oc;
                    }
                    return;
                case "결과 순위":
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    LArea(r, LS(16), () => { ResultsHeader(); _sRes = GUILayout.BeginScrollView(_sRes); ResultsTable(); GUILayout.EndScrollView(); });
                    return;
                case "시상대":
                {
                    string t = e.Text ?? "";
                    int rank = t.Contains("1") ? 1 : t.Contains("2") ? 2 : t.Contains("3") ? 3 : 1;
                    PodiumCard(r, rank, e.Radius * _ls);
                    return;
                }
                case "로비로 돌아가기":
                    if (Game.IsScnGame) { if (host) LButton(e, r, true, Session.HostBackToLobby); }
                    else LButton(e, r, true, () => ShowResults = false);
                    return;
            }

            // 기능 없는 부품: 설계 그대로 (이미지 자리는 설계용이라 게임에서는 안 그림)
            if (e.Type == "image") return;
            if (e.Type == "progress") { LBox(r, e.Fill, e.Stroke, e.Radius); LBox(new Rect(r.x, r.y, r.width * 0.6f, r.height), e.Color, Color.clear, e.Radius); return; }
            LBox(r, e.Fill, e.Stroke, e.Radius);
            LText(e, r, Tok(e.Text));
        }

        // ---------------- 채팅 ----------------
        static string _chatInput = "";
        static Vector2 _sChat;
        static int _chatSeen = -1;

        static void DrawChat(Layout.El e, Rect r)
        {
            LBox(r, e.Fill, e.Stroke, e.Radius);
            float head = string.IsNullOrEmpty(e.Text) ? LS(12) : LS(e.Size) * 1.6f + LS(12);
            if (!string.IsNullOrEmpty(e.Text)) LText(e, r, Tok(e.Text));
            float pad = LS(16), ih = LS(48), bw = LS(88);
            var inR = new Rect(r.x + pad, r.yMax - pad - ih, r.width - pad * 3 - bw, ih);
            var btR = new Rect(inR.xMax + pad, inR.y, bw, ih);
            var list = new Rect(r.x + pad, r.y + head, r.width - pad * 2, inR.y - r.y - head - LS(10));

            // 메시지 목록 (새 줄이 오면 맨 아래로)
            if (_chatSeen != Session.ChatVersion) { _chatSeen = Session.ChatVersion; _sChat.y = float.MaxValue; }
            var nameSt = Label(LS(15), C_acc, FontStyle.Bold, TextAnchor.UpperLeft);
            var textSt = Label(LS(15), C_txt, FontStyle.Normal, TextAnchor.UpperLeft, true);
            var sysSt = Label(LS(13), C_sub, FontStyle.Normal, TextAnchor.UpperLeft, true);
            LArea(list, 0, () =>
            {
                _sChat = GUILayout.BeginScrollView(_sChat);
                foreach (var m in Session.Chat)
                {
                    if (m.Id == 0) { GUILayout.Label(Esc(m.Text), sysSt); GUILayout.Space(LS(4)); continue; }
                    bool me = m.Id == Net.MyId;
                    GUILayout.BeginHorizontal();
                    var nm = Trim(m.Name, 12);
                    var ns = me ? Label(LS(15), C_gold, FontStyle.Bold, TextAnchor.UpperLeft) : nameSt;
                    GUILayout.Label(nm, ns, GUILayout.Width(ns.CalcSize(new GUIContent(nm)).x + LS(10)));
                    GUILayout.Label(Esc(m.Text), textSt);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(LS(4));
                }
                GUILayout.EndScrollView();
            });

            // 입력 (Enter 로 보내기)
            bool focus = GUI.GetNameOfFocusedControl() == "lin" + e.Id;
            var ev = Event.current;
            if (focus && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter))
            {
                Session.SendChat(_chatInput); _chatInput = "";
                ev.Use();
            }
            var fe = new Layout.El { Id = e.Id, Size = 15, Weight = 500, Color = C_sub, Fill = Hex("141820"), Stroke = Hex("2c333d"), Radius = 10 };
            _chatInput = LInput(fe, inR, _chatInput, "", Session.ChatMax);
            var be = new Layout.El { Id = e.Id + "s", Size = 15, Weight = 700, Color = Theme.Current.AccText, Fill = C_acc, Stroke = C_acc, Radius = 10, Text = "보내기", Type = "button", Align = "center" };
            LButton(be, btR, !string.IsNullOrEmpty(_chatInput.Trim()), () => { Session.SendChat(_chatInput); _chatInput = ""; });
        }

        // ---------------- 로고 (도는 두 행성 + ADOFAI MULTI) ----------------
        static void DrawLogo(Layout.El e, Rect r)
        {
            float h = r.height, d = h * 0.26f;
            Orbit(new Vector2(r.x + h * 0.42f, r.center.y), h * 0.26f, d * 0.62f, 0.9f);
            GUI.Label(new Rect(r.x + h * 0.95f, r.y, r.width - h, r.height), $"ADOFAI <color=#{HexOf(C_acc)}>MULTI</color>", Label(LS(e.Size), e.Color, FontStyle.Bold));
        }

        // ---------------- 메인 버튼 (지금 해야 할 일 한 가지) ----------------
        static bool _layoutMainBtn;

        static void DrawMainButton(Layout.El e, Rect r)
        {
            bool host = Net.IsHost;
            var ph = Session.Phase;
            string label, sub = null, icon = null; bool on = true, ghost = false; System.Action act = null;
            if (host)
            {
                // 방장은 항상 "게임 시작" (맵 고르기는 방 설정의 맵 선택 버튼)
                string rd = $"준비 {Session.ReadyCount}/{Session.ReadyNeeded}";
                if (ph == Phase.Results && !(ShowResults && Session.Results.Count > 0)) { label = "결과 발표"; sub = "지금 바로"; icon = "trophy"; act = Session.HostShowResults; }
                else if (ph == Phase.Distributing) { label = "게임 시작"; sub = rd; icon = "play"; on = Session.CanStart; act = Session.HostStart; }
                else if (ph == Phase.Packing) { label = "게임 시작"; sub = Session.HostFetchProgress >= 0 ? $"맵 준비 중 {Session.HostFetchProgress * 100:0}%" : "맵 준비 중"; icon = "play"; on = false; }
                else if (ph == Phase.Lobby || ShowResults)
                {
                    // 같은 맵을 다시 보내고 준비 받기 시작 (맵이 없으면 꺼짐)
                    label = "게임 시작"; sub = rd; icon = "play";
                    on = Session.MapHash != null && !string.IsNullOrEmpty(Session.HostLevelPath);
                    act = () => { ShowResults = false; Session.HostRedo(); };
                }
                else { label = "게임 진행 중"; on = false; }
            }
            else
            {
                if (ph == Phase.Distributing && Session.DlCancelled) { label = "다시 받기"; sub = "받고 참가하기"; icon = "download"; act = Session.DlRetry; }
                else if (ph == Phase.Distributing && Session.DlActive)
                {
                    float p = Session.MapSize > 0 ? (float)Session.DlReceived / Session.MapSize : 0;
                    label = Session.DlPaused ? "받기 일시정지" : "맵 받는 중"; sub = $"{p * 100:0}%"; on = false;
                }
                else if (Session.CanReady)
                {
                    if (Session.MyReady) { label = "준비 완료"; sub = "눌러서 취소 · F5"; icon = "check"; ghost = true; }
                    else { label = "준비"; sub = "F5"; icon = "check"; }
                    act = Session.ToggleReady;
                }
                else if (ph == Phase.Distributing) { label = "맵 확인 중"; on = false; }
                else if (ph == Phase.Lobby) { label = "대기 중"; sub = Session.MapHash == null ? "방장이 맵을 고르는 중" : "방장이 다음 판을 준비 중"; on = false; }
                else { label = "게임 진행 중"; on = false; }
            }

            bool hover = on && !_lBlocked && r.Contains(Event.current.mousePosition);
            Color fill = !on || ghost ? Hex("232a33") : e.Fill;
            Color stroke = ghost ? C_ok : !on ? Hex("323a46") : e.Stroke;
            Color txt = ghost ? C_ok : !on ? C_sub : e.Color;
            if (hover && !ghost) fill = Color.Lerp(fill, Color.white, 0.12f);
            if (hover && ghost) fill = A(C_ok, 0.12f);
            LBox(r, fill, stroke, e.Radius);
            // 준비 가능할 때 은은하게 숨쉬는 테두리
            if (on && !ghost && (label == "준비" || label == "게임 시작"))
            {
                // 색은 고정하고 GUI.color 로 깜빡임 (알파마다 텍스처를 새로 만들지 않게)
                var oc = GUI.color; GUI.color = A(oc, oc.a * (0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f)));
                LBox(new Rect(r.x - LS(3), r.y - LS(3), r.width + LS(6), r.height + LS(6)), Color.clear, A(e.Fill, 1), e.Radius + 3);
                GUI.color = oc;
            }

            int fs = LS(e.Size);
            bool two = !string.IsNullOrEmpty(sub) && r.height >= LS(64);
            var main = new Rect(r.x, two ? r.center.y - fs * 0.95f : r.y, r.width, two ? fs * 1.3f : r.height);
            IconLabel(main, icon, label, Label(fs, txt, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (two) GUI.Label(new Rect(r.x, main.yMax - LS(2), r.width, fs), sub, Label(Mathf.RoundToInt(fs * 0.58f), A(txt, 0.75f), FontStyle.Normal, TextAnchor.MiddleCenter));
            if (LClick(r, on, e.Radius) && act != null) act();
        }

        // ---------------- 진행 단계 (맵 선택 → 맵 받기 → 준비 → 플레이 → 결과) ----------------
        static void DrawStepperL(Rect r)
        {
            int cur = CurrentStep();
            if (Session.Phase == Phase.Distributing && Session.CanStart) cur = 2;
            if (ShowResults && Session.Results.Count > 0) cur = 4;
            if (Session.Phase == Phase.Lobby && Session.MapHash == null) cur = 0;
            float pad = LS(24), seg = (r.width - pad * 2) / (Steps.Length - 1);
            float cy = r.center.y, d = LS(28);
            for (int i = 0; i < Steps.Length - 1; i++)
            {
                float x0 = r.x + pad + seg * i + d / 2f + LS(6), x1 = r.x + pad + seg * (i + 1) - d / 2f - LS(6);
                Fill(new Rect(x0, cy - 1, x1 - x0, 2), i < cur ? C_acc : A(C_line, 1));
            }
            for (int i = 0; i < Steps.Length; i++)
            {
                float cx = r.x + pad + seg * i;
                var cr = new Rect(cx - d / 2f, cy - d / 2f, d, d);
                if (i < cur)
                {
                    Dot(cr.center, d / 2f, C_acc);
                    Icons.Draw(new Rect(cr.x + d * 0.22f, cr.y + d * 0.22f, d * 0.56f, d * 0.56f), "check", Theme.Current.AccText);
                }
                else if (i == cur)
                {
                    var o = GUI.color; GUI.color = A(o, 0.25f + Mathf.PingPong(Time.unscaledTime, 0.6f) * 0.4f);
                    Dot(cr.center, d / 2f + LS(5), C_acc);
                    GUI.color = o;
                    Dot(cr.center, d / 2f, C_acc);
                    GUI.Label(cr, (i + 1).ToString(), Label(LS(14), Theme.Current.AccText, FontStyle.Bold, TextAnchor.MiddleCenter));
                }
                else
                {
                    if (Event.current.type == EventType.Repaint) LBox(cr, Hex("171b21"), Hex("3a4452"), 14);
                    GUI.Label(cr, (i + 1).ToString(), Label(LS(14), A(C_sub, 0.7f), FontStyle.Bold, TextAnchor.MiddleCenter));
                }
            }
        }

        // ---------------- 상태 패널 (지금 무슨 일이 일어나는지) ----------------
        static void StatusPanel(Rect r)
        {
            bool host = Net.IsHost;
            var ph = Session.Phase;
            string icon = "clock", title, desc; Color ic = C_sub;
            float big = -1; string bigSub = null;

            if (ShowResults && Session.Results.Count > 0 && ph != Phase.Results) { icon = "trophy"; ic = C_gold; title = "결과 발표 완료"; desc = host ? "같은 맵으로 한 판 더 하거나 다른 맵을 고르세요." : "방장이 다음 판을 고르는 중이에요."; }
            else if (host)
            {
                switch (ph)
                {
                    case Phase.Lobby:
                        if (Session.MapHash == null) { icon = "search"; ic = C_acc; title = "맵을 골라주세요"; desc = "TUF나 내 컴퓨터에서 맵을 고르면 모두에게 자동으로 보내져요. 이미 받은 사람은 건너뛰어요."; }
                        else { icon = "refresh"; ic = C_acc; title = "다음 판 준비"; desc = "같은 맵으로 한 판 더 하거나, 다른 맵을 선택하세요."; }
                        break;
                    case Phase.Packing:
                        icon = "download"; ic = C_acc; title = "맵 준비 중"; desc = Session.HostFetchProgress >= 0 ? "TUF에서 맵을 받고 있어요. 다 받으면 모두에게 보내요." : "맵을 압축하는 중이에요.";
                        if (Session.HostFetchProgress >= 0) big = Session.HostFetchProgress;
                        break;
                    case Phase.Distributing:
                        if (Session.CanStart) { icon = "check"; ic = C_ok; title = "모두 준비 완료!"; desc = "시작을 누르면 모두 레벨에 들어가고 바로 카운트다운이 시작돼요."; }
                        else
                        {
                            icon = "clock"; ic = C_gold; title = $"준비 기다리는 중  {Session.ReadyCount}/{Session.ReadyNeeded}";
                            var wait = Session.Board.Where(x => x.Id != Net.HostId && x.State != PState.Cancelled && !(x.State == PState.HasMap && x.LobbyReady)).Select(x => Trim(x.Name, 10)).ToArray();
                            desc = wait.Length > 0 ? "아직: " + string.Join(", ", wait) : "모두 맵을 받고 준비(F5)를 눌러야 시작할 수 있어요.";
                            if (Session.ReadyNeeded == 0) desc = "혼자서도 시작할 수 있어요. 친구를 초대하면 같이 대결해요.";
                        }
                        break;
                    case Phase.Results:
                        icon = "trophy"; ic = C_gold; title = "결과 기다리는 중"; desc = "아직 플레이 중인 사람이 있어요. 아래 버튼으로 지금 바로 발표할 수 있어요.";
                        break;
                    default: icon = "play"; ic = C_ice; title = "게임 진행 중"; desc = "레벨에 들어가는 중이에요..."; break;
                }
            }
            else
            {
                switch (ph)
                {
                    case Phase.Distributing when Session.DlCancelled:
                        icon = "close"; title = "관전 모드"; desc = "이번 판은 다운로드를 취소했어요. 다시 받으면 참가할 수 있어요."; break;
                    case Phase.Distributing when Session.DlActive:
                    {
                        icon = Session.DlPaused ? "pause" : "download"; ic = Session.DlPaused ? C_gold : C_acc;
                        title = Session.DlPaused ? "받기 일시정지됨" : "맵 받는 중";
                        big = Session.MapSize > 0 ? (float)Session.DlReceived / Session.MapSize : 0;
                        bigSub = $"{Session.DlReceived / 1048576f:0.0} / {Session.MapSize / 1048576f:0.0}MB";
                        desc = Session.MapSource == 1 ? "TUF에서 바로 받고 있어요." : "방장에게서 받고 있어요.";
                        break;
                    }
                    case Phase.Distributing:
                        if (Session.MyReady) { icon = "check"; ic = C_ok; title = "준비 완료!"; desc = "방장이 시작을 누르면 바로 카운트다운이 시작돼요."; }
                        else if (Session.MyHasMap) { icon = "check"; ic = C_gold; title = "맵 받기 완료 — 준비를 눌러주세요"; desc = "F5 또는 아래 준비 버튼. 모두 준비하면 방장이 시작해요."; }
                        else { title = "맵 확인 중"; desc = "받은 맵을 확인하고 있어요."; }
                        break;
                    case Phase.Results: icon = "trophy"; ic = C_gold; title = "결과 기다리는 중"; desc = "다른 사람이 끝나길 기다리는 중이에요."; break;
                    case Phase.Lobby:
                        icon = "clock"; title = Session.MapHash == null ? "방장이 맵을 고르는 중" : "다음 판 기다리는 중";
                        desc = $"맵이 정해지면 자동으로 받아요.   판정: {Session.JudgeNames[Session.Judge]}"; break;
                    default: icon = "play"; ic = C_ice; title = "게임 진행 중"; desc = "레벨에 들어가는 중이에요..."; break;
                }
            }

            float d = LS(44);
            var cr = new Rect(r.x, r.y, d, d);
            Dot(cr.center, d / 2f, A(ic, 0.16f));
            Icons.Draw(new Rect(cr.x + d * 0.25f, cr.y + d * 0.25f, d * 0.5f, d * 0.5f), icon, ic);
            float tx = cr.xMax + LS(16), tw = r.xMax - tx;
            GUI.Label(new Rect(tx, r.y - LS(2), tw, LS(30)), title, Label(LS(21), C_txt, FontStyle.Bold));
            GUI.Label(new Rect(tx, r.y + LS(30), tw, LS(48)), Esc(desc), Label(LS(15), C_sub, FontStyle.Normal, TextAnchor.UpperLeft, true));

            if (big >= 0)
            {
                float by = r.y + LS(92);
                GUI.Label(new Rect(tx, by, LS(140), LS(48)), $"{big * 100:0}%", Label(LS(40), ic, FontStyle.Bold));
                if (bigSub != null) GUI.Label(new Rect(tx + LS(140), by + LS(14), tw - LS(140), LS(26)), bigSub, Label(LS(15), C_sub));
                var br = new Rect(tx, by + LS(58), tw, LS(8));
                LBox(br, Hex("232a33"), Color.clear, 4);
                if (big > 0.001f) LBox(new Rect(br.x, br.y, Mathf.Max(br.height, br.width * Mathf.Clamp01(big)), br.height), ic, Color.clear, 4);
            }

            // 방장: 결과 대기 중이면 지금 발표 (메인 버튼이 없는 설계용)
            if (host && ph == Phase.Results && !_layoutMainBtn)
            {
                var b = new Rect(tx, r.yMax - LS(48), LS(200), LS(44));
                var o = GUI.enabled; GUI.enabled = !_lBlocked && o;
                if (GUI.Button(b, "지금 결과 발표", S_btn)) Session.HostShowResults();
                GUI.enabled = o;
            }
        }

        static void DrawSortMenu()
        {
            float w = 150 * _ls, h = 40 * _ls;
            var box = SortMenuBox();
            LBox(new Rect(box.x, box.y + LS(4), box.width, box.height), A(Color.black, 0.4f), Color.clear, 12);
            LBox(box, Hex("1b1f26"), Hex("3a4452"), 12);
            for (int i = 0; i < Tuf.SortKeys.Length; i++)
            {
                var r = new Rect(box.x + 6 * _ls + (i % 2) * w, box.y + 6 * _ls + (i / 2) * h, w, h - 4 * _ls);
                bool on = Tuf.Sort == Tuf.SortKeys[i];
                if (on) LBox(r, A(C_acc, 0.18f), A(C_acc, 0.8f), 8);
                IconLabel(new Rect(r.x + LS(14), r.y, r.width - LS(20), r.height), on ? "check" : null, Tuf.SortNames[i], Label(LS(15), on ? C_acc : C_txt, on ? FontStyle.Bold : FontStyle.Normal));
                if (LClick(r, true, 8)) { Tuf.Sort = Tuf.SortKeys[i]; _tufSortOpen = false; _tufDirty = true; }
            }
        }

        // ---------------- 실시간 순위표 (플레이 중 왼쪽 위) ----------------
        static void DrawRanking(Layout.El e, Rect r)
        {
            var board = Session.Board.Where(x => x.State >= PState.Loading && x.State != PState.Cancelled).ToList();
            if (board.Count == 0) board = Session.Board.ToList();
            board = board.OrderByDescending(x => x.Stats.XAcc).ToList();   // 절대 정확도 순
            float head = LS(40), rowH = LS(40);
            var box = new Rect(r.x, r.y, r.width, Mathf.Min(r.height, head + board.Count * rowH + LS(8)));
            LBox(box, e.Fill, e.Stroke, e.Radius);
            // 머리: 제목 + 인원
            GUI.Label(new Rect(box.x + LS(16), box.y, box.width - LS(32), head), Tok(e.Text), Label(LS(13), A(C_sub, 0.95f), FontStyle.Bold));
            GUI.Label(new Rect(box.x + LS(16), box.y, box.width - LS(32), head), $"{board.Count}명", Label(LS(13), A(C_sub, 0.8f), FontStyle.Normal, TextAnchor.MiddleRight));
            Fill(new Rect(box.x + LS(12), box.y + head - 1, box.width - LS(24), 1), A(Color.white, 0.07f));

            int fs = LS(Mathf.Max(12, e.Size));
            for (int i = 0; i < board.Count; i++)
            {
                float ry = box.y + head + LS(4) + i * rowH;
                if (ry + rowH > r.yMax) break;
                var en = board[i];
                bool me = en.Id == Net.MyId;
                int slot = Session.Board.IndexOf(en);
                var col = SlotColor(slot < 0 ? i : slot);
                var row = new Rect(box.x + LS(6), ry, box.width - LS(12), rowH - LS(4));
                if (me)
                {
                    LBox(row, A(C_acc, 0.14f), Color.clear, 8);
                    LBox(new Rect(row.x, row.y + LS(6), LS(3), row.height - LS(12)), C_acc, Color.clear, 1.5f);
                }
                float x = row.x + LS(10);
                GUI.Label(new Rect(x, row.y, LS(22), row.height - LS(4)), (i + 1).ToString(), Label(fs, i < 3 ? RankColor(i + 1) : A(C_sub, 0.8f), FontStyle.Bold, TextAnchor.MiddleCenter));
                x += LS(28);
                Dot(new Vector2(x + LS(5), row.center.y - LS(1)), LS(5), col);
                x += LS(18);
                bool fin = en.State == PState.Finished;
                float accW = LS(78);
                GUI.Label(new Rect(x, row.y, row.xMax - x - accW - LS(8), row.height - LS(4)), Trim(en.Name, 10), Label(fs, me ? C_txt : A(e.Color, 0.92f), me ? FontStyle.Bold : FontStyle.Normal));
                var ar = new Rect(row.xMax - accW - LS(8), row.y, accW, row.height - LS(4));
                if (fin) Icons.Draw(new Rect(ar.x - LS(14), ar.center.y - LS(7), LS(14), LS(14)), "check", C_ok);
                GUI.Label(ar, $"{en.Stats.XAcc * 100:0.00}%", Label(fs, me ? C_acc : e.Color, FontStyle.Bold, TextAnchor.MiddleRight));
                // 진행 막대
                var pr = new Rect(x, row.yMax - LS(7), row.xMax - x - LS(8), Mathf.Max(1, LS(2)));
                Fill(pr, A(Color.white, 0.08f));
                Fill(new Rect(pr.x, pr.y, pr.width * Mathf.Clamp01(en.Stats.Progress), pr.height), fin ? C_ok : A(col, 0.85f));
            }
        }

        // ---------------- 섬 / 인게임 진입점 ----------------
        /// <summary>설계 배치가 있으면 그걸로 섬을 그리고 true</summary>
        static bool DrawIslandLayout()
        {
            Layout.Screen main;
            bool inRoom = Session.InRoom;
            if (!inRoom) main = Layout.Get("s_entrance");
            else main = Layout.Get("s_lobby");
            if (main == null) return false;
            _layoutMapBtn = inRoom && main.Els.Any(x => x.Func == "맵 고르기 열기" || x.Func == "맵 선택 열기");
            _layoutMainBtn = inRoom && main.Els.Any(x => x.Func == "메인 버튼" || x.Func == "메인 버튼 (상황별)");

            bool results = inRoom && ShowResults && Session.Results.Count > 0 && Layout.Get("s_results") != null;
            bool drawer = _drawer && Layout.Get("s_maps") != null;
            bool popup = _settings || _profile || _confirmLeave || _pwMode != 0;

            _lBlocked = results || drawer || popup;
            DrawLayout(main, true);
            if (results) { _lBlocked = drawer || popup; DrawLayout(Layout.Get("s_results"), false); }
            if (_drawer)
            {
                _lBlocked = popup;
                if (drawer) DrawLayout(Layout.Get("s_maps"), false); else DrawMapDrawer(UnityEngine.Screen.width, UnityEngine.Screen.height);
            }
            _lBlocked = false;

            DrawToast();
            DrawFooter(inRoom);
            return true;
        }

        /// <summary>인게임: 설계의 순위표·카운트다운·결과. 없으면 false</summary>
        static bool DrawInGameLayout()
        {
            var s = Layout.Get("s_ingame");
            if (s == null) return false;
            var ph = Session.Phase;
            bool results = ph == Phase.Results && Session.Results.Count > 0 && Layout.Get("s_results") != null;
            _lBlocked = results;
            DrawLayout(s, false);
            _lBlocked = false;
            if (ph == Phase.WaitingStart)
                Banner(Session.GameReady ? $"준비 완료  {Session.GameReadyCount}/{Session.GameReadyNeeded}" : $"아무 키나 눌러 준비  {Session.GameReadyCount}/{Session.GameReadyNeeded}");
            else if (ph == Phase.Countdown && !s.Els.Any(e => e.Func == "카운트다운"))
            {
                int n = Mathf.CeilToInt(Session.CountdownLeft);
                GUI.Label(new Rect(0, UnityEngine.Screen.height * 0.3f, UnityEngine.Screen.width, 150), n > 0 ? n.ToString() : "GO!", S_big);
            }
            else if (ph == Phase.Results)
            {
                if (results) DrawLayout(Layout.Get("s_results"), false);
                else if (Session.Results.Count == 0) Banner("완주! 다른 사람을 기다리는 중...");
                else
                {
                    GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), T_dim);
                    var r = new Rect((UnityEngine.Screen.width - 900) / 2f, (UnityEngine.Screen.height - 560) / 2f, 900, 560);
                    DrawPodium(r, true);
                    if (Net.IsHost) { GUILayout.BeginArea(new Rect(r.x + 24, r.yMax - 76, r.width - 48, 58)); if (GUILayout.Button("모두 멀티 섬으로", S_cta)) Session.HostBackToLobby(); GUILayout.EndArea(); }
                }
            }
            return true;
        }
    }
}
