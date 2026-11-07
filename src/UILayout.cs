using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// UI 설계판에서 만든 배치(Layout)를 그대로 그리는 부분.
    /// 위치·크기·색·둥글기·글자는 설계 그대로, 동작은 각 부품의 "기능"(func)에 연결.
    /// </summary>
    public static partial class UI
    {
        static float _ls = 1, _lox, _loy;
        static bool _lBlocked;                         // 위에 팝업이 떠 있으면 아래 화면은 입력을 받지 않음
        static Rect _lSortBtn; static bool _lSortShown;
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
            if (!_lBox.TryGetValue(key, out var st))
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
            float px = (top ? 18 : 14) * _ls, py = top ? 14 * _ls : 0;
            var tr = new Rect(r.x + px, r.y + py, r.width - px * 2, r.height - py * 2);
            if (top) tr.height = Mathf.Min(tr.height, LS(e.Size) * 1.5f * (1 + text.Count(c => c == '\n')));
            GUI.Label(tr, text, LStyle(e, col, top));
        }

        // ---------------- 글자 속 표시 값 ----------------
        static string Tok(string t)
        {
            if (string.IsNullOrEmpty(t) || t.IndexOf('{') < 0) return Esc(t);
            string song = string.IsNullOrEmpty(Session.MapSong) ? Session.MapTitle : Session.MapSong;
            bool noMap = Session.MapHash == null;
            if (noMap && (t.Contains("{artist}") || t.Contains("{creator}") || t.Contains("{size}") || t.Contains("{diff}"))) return "";
            var res = Session.Results;
            string RN(int i) => i < res.Count ? Trim(res[i].Name, 12) : "-";
            string RA(int i) => i < res.Count ? $"{res[i].Stats.Acc * 100:0.00}%" : "";
            return Esc(t)
                .Replace("{code}", Net.RoomCode ?? "")
                .Replace("{roomName}", Esc(Net.RoomName ?? ""))
                .Replace("{myName}", Esc(Net.MyName))
                .Replace("{song}", noMap ? (Net.IsHost ? "어떤 맵으로 할까요?" : "방장이 맵을 고르는 중...") : Esc(song))
                .Replace("{artist}", noMap ? "" : Esc(Session.MapArtist))
                .Replace("{creator}", noMap ? "" : Esc(Session.MapAuthor))
                .Replace("{diff}", Esc(Session.MapDiff ?? ""))
                .Replace("{tufId}", Session.MapSource == 1 ? Session.MapTufId.ToString() : "")
                .Replace("{size}", noMap ? "" : $"{Session.MapSize / 1048576f:0.0}MB")
                .Replace("{players}", Session.Board.Count.ToString())
                .Replace("{notice}", Esc(Session.Notice ?? ""))
                .Replace("{r1name}", RN(0)).Replace("{r2name}", RN(1)).Replace("{r3name}", RN(2))
                .Replace("{r1acc}", RA(0)).Replace("{r2acc}", RA(1)).Replace("{r3acc}", RA(2));
        }

        // ---------------- 공용 조작 ----------------
        static bool LClick(Rect r, bool enabled = true)
        {
            bool hover = enabled && !_lBlocked && r.Contains(Event.current.mousePosition);
            if (hover && Event.current.type == EventType.Repaint) Fill(r, A(Color.white, 0.06f));
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
            if (LClick(r, enabled)) onClick?.Invoke();
        }

        static void LToggle(Layout.El e, Rect r, bool on, System.Action onClick, bool enabled = true)
        {
            var oc = GUI.color;
            if (!enabled) GUI.color = A(oc, oc.a * 0.5f);
            if (on) LBox(r, A(C_acc, 0.2f), C_acc, e.Radius); else LBox(r, Hex("232a33"), Hex("3a4452"), e.Radius);
            LText(e, r, Tok(e.Text), on ? C_acc : Hex("e8ecf1"));
            GUI.color = oc;
            if (LClick(r, enabled)) onClick?.Invoke();
        }

        static readonly Dictionary<string, GUIStyle> _lField = new Dictionary<string, GUIStyle>();
        static string LInput(Layout.El e, Rect r, string value, string placeholder, int max = 40)
        {
            LBox(r, e.Fill, e.Stroke, e.Radius);
            string key = LS(e.Size) + "|" + e.Weight;
            if (!_lField.TryGetValue(key, out var st) || st.font != _font)
            {
                st = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = LS(e.Size), fontStyle = e.Weight >= 700 ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(LS(14), LS(14), 0, 0), clipping = TextClipping.Clip };
                st.normal.background = st.focused.background = st.hover.background = st.active.background = null;
                st.normal.textColor = st.focused.textColor = st.hover.textColor = C_txt;
                _lField[key] = st;
            }
            var o = GUI.enabled; GUI.enabled = !_lBlocked && o;
            value = GUI.TextField(r, value ?? "", max, st);
            GUI.enabled = o;
            if (string.IsNullOrEmpty(value) && Event.current.type == EventType.Repaint)
                GUI.Label(new Rect(r.x + LS(14), r.y, r.width - LS(28), r.height), placeholder, Label(LS(e.Size), A(e.Color, 0.9f), FontStyle.Normal, TextAnchor.MiddleLeft));
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
            foreach (var e in s.Els)
            {
                var oc = GUI.color;
                if (e.Opacity < 0.999f) GUI.color = A(oc, oc.a * Mathf.Clamp01(e.Opacity));
                DrawEl(e, LR(e));
                GUI.color = oc;
            }
            if (_lSortShown && _tufSortOpen) DrawSortMenu();
        }

        static void DrawEl(Layout.El e, Rect r)
        {
            bool host = Net.IsHost, inRoom = Session.InRoom;
            bool tufTab = _drawerTab == 1;
            bool busy = Session.Phase == Phase.Packing || (Session.InMatch && Session.Phase != Phase.Results);
            switch (e.Func)
            {
                // ---- 공통 ----
                case "방 나가기": LButton(e, r, true, AskLeave); return;
                case "설정 열기": LButton(e, r, true, () => { _settings = !_settings; _profile = false; }); return;
                case "프로필 열기": LButton(e, r, true, () => { _profile = !_profile; _settings = false; _nickEdit = Profile.Nick; }); return;
                case "테마 바꾸기": LButton(e, r, true, () => { _settings = true; _profile = false; }); return;
                case "업데이트 확인": LButton(e, r, Runner.Instance != null, () => Updater.CheckNow(Runner.Instance)); return;
                case "알림 메시지":
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    if (inRoom)
                    {
                        float head = string.IsNullOrEmpty(e.Text) ? 0 : LS(e.Size) * 1.6f;
                        LText(e, r, Tok(e.Text));
                        LArea(new Rect(r.x, r.y + head, r.width, r.height - head), LS(16), () => { if (host) HostPanel(); else MemberPanel(); });
                    }
                    else LText(e, r, Esc(string.IsNullOrEmpty(Session.Notice) ? Net.Status : Session.Notice));
                    return;

                // ---- 입구 ----
                case "공개 방 목록":
                    RoomList.Tick(Runner.Instance);
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    LArea(r, LS(10), () =>
                    {
                        _sRooms = GUILayout.BeginScrollView(_sRooms);
                        if (RoomList.Loaded && RoomList.Rooms.Count == 0)
                        {
                            GUILayout.Space(LS(60));
                            GUILayout.Label("지금 열린 공개 방이 없어요", Label(LS(22), C_txt, FontStyle.Bold, TextAnchor.MiddleCenter));
                            GUILayout.Label("방을 만들어 보세요", Label(LS(15), C_sub, FontStyle.Normal, TextAnchor.MiddleCenter));
                        }
                        else if (!RoomList.Loaded) GUILayout.Label(string.IsNullOrEmpty(RoomList.Error) ? "불러오는 중..." : Esc(RoomList.Error), S_sub);
                        foreach (var rm in RoomList.Rooms) DrawRoomCard(rm);
                        GUILayout.EndScrollView();
                    });
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
                        if (inRoom) { Net.SetPublic(!wantPrivate); Session.Notice = !wantPrivate ? "공개 방이 됐어요" : "비공개 방이 됐어요"; }
                        else _newPublic = !wantPrivate;
                    }, can);
                    return;
                }
                case "방 만들기": LButton(e, r, !inRoom, () => Net.CreateRoom(_newPublic, _roomName)); return;
                case "방 코드로 참가":
                    if (e.Type == "input") _joinId = Net.NormalizeCode(LInput(e, r, _joinId, Tok(e.Text), 8));
                    else LButton(e, r, Net.IsValidCode(_joinId), () => Net.Join(_joinId));
                    return;

                // ---- 로비 ----
                case "방 코드 복사":
                    if (string.IsNullOrEmpty(Net.RoomCode)) return;
                    LButton(e, r, true, () => { GUIUtility.systemCopyBuffer = Net.RoomCode; Session.Notice = "방 코드를 복사했어요"; },
                        e.Text.Contains("{code}") ? Tok(e.Text) : "코드 " + Net.RoomCode);
                    return;
                case "Steam 초대": if (Net.CanInvite) LButton(e, r, true, Net.InviteOverlay); return;
                case "플레이어 명단": LBox(r, e.Fill, e.Stroke, e.Radius); DrawLineup(r, false); return;
                case "맵 고르기 열기": if (host) LButton(e, r, !busy, () => { _drawer = true; _drawerTab = 1; }); return;
                case "한 판 더": if (host && Session.MapHash != null && !string.IsNullOrEmpty(Session.HostLevelPath)) LButton(e, r, true, () => { ShowResults = false; Session.HostRedo(); }); return;
                case "준비 (F5)":
                    if (host) LButton(e, r, Session.CanStart, Session.HostStart, Session.Phase == Phase.Distributing ? $"시작  {Session.ReadyCount}/{Session.ReadyNeeded} 준비" : "시작");
                    else LButton(e, r, Session.CanReady, Session.ToggleReady, Session.MyReady ? "준비 완료 · 취소" : null);
                    return;
                case "게임 시작": if (host) LButton(e, r, Session.CanStart, Session.HostStart, Session.Phase == Phase.Distributing ? $"{Tok(e.Text)}  {Session.ReadyCount}/{Session.ReadyNeeded}" : null); return;
                case "기권": if (Session.Phase == Phase.Countdown || Session.Phase == Phase.Playing) LButton(e, r, true, Session.Forfeit); return;
                case "맵 받기 진행률":
                {
                    var parts = Session.Board.Where(x => x.State != PState.Cancelled).ToList();
                    float p = Session.Phase == Phase.Packing ? Mathf.Max(0, Session.HostFetchProgress)
                            : Session.Phase == Phase.Distributing ? (parts.Count == 0 ? 0 : parts.Average(x => x.State >= PState.HasMap ? 1f : x.Download))
                            : Session.MapHash != null ? 1 : 0;
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    if (p > 0.001f) LBox(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * p), r.height), e.Color, Color.clear, e.Radius);
                    return;
                }
                case "맵 난이도":
                    if (string.IsNullOrEmpty(Session.MapDiff)) return;
                    { var c = Session.MapDiffColor; LBox(r, A(c, 0.2f), c, e.Radius); LText(e, r, Tok(e.Text.Length > 0 ? e.Text : "{diff}"), c); }
                    return;
                case "맵 난이도 아이콘":
                {
                    var ic = string.IsNullOrEmpty(Session.MapDiff) ? null : Tuf.Icon(Session.MapDiff);
                    if (ic != null) GUI.DrawTexture(r, ic, ScaleMode.ScaleToFit);
                    else if (!string.IsNullOrEmpty(Session.MapDiff)) { LBox(r, e.Fill, e.Stroke, e.Radius); LText(e, r, Esc(Session.MapDiff), Session.MapDiffColor); }
                    return;
                }

                // ---- 맵 고르기 ----
                case "닫기": if (e.Type == "panel") { LBox(r, e.Fill, e.Stroke, e.Radius); if (LClick(r)) _drawer = false; } else LButton(e, r, true, () => _drawer = false); return;
                case "TUF 탭": LToggle(e, r, tufTab, () => _drawerTab = 1); return;
                case "내 컴퓨터 맵 목록": LToggle(e, r, !tufTab, () => { _drawerTab = 0; MapBrowser.ScanOnce(); }); return;
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
                        LArea(r, LS(8), () =>
                        {
                            if (tufTab)
                            {
                                GUILayout.Label(Tuf.Loading && Tuf.Results.Count == 0 ? "검색 중..." : !string.IsNullOrEmpty(Tuf.Error) ? Tuf.Error : $"총 {Tuf.Total:N0}개   ·   {Tuf.MinName} ~ {Tuf.MaxName}   ·   {Tuf.SortName} {(Tuf.Asc ? "낮은 순" : "높은 순")}", S_small);
                                _sTuf = GUILayout.BeginScrollView(_sTuf, GUILayout.ExpandHeight(true));
                                DrawTufRows();
                                GUILayout.EndScrollView();
                            }
                            else
                            {
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
                    else { LButton(e, r, true, () => _tufSortOpen = !_tufSortOpen, Esc(Tuf.SortName)); _lSortBtn = r; _lSortShown = true; }
                    return;
                case "이 맵으로 시작":
                    if (tufTab) LButton(e, r, !busy && _tufSel != null && _tufSel.CanDownload, () => { _drawer = false; ShowResults = false; Session.HostDistributeTuf(_tufSel); });
                    else LButton(e, r, !busy && !string.IsNullOrEmpty(_mapPath), () => { _drawer = false; ShowResults = false; Session.HostDistribute(_mapPath.Trim().Trim('"')); });
                    return;

                // ---- 인게임 · 결과 ----
                case "실시간 순위표": DrawRanking(e, r); return;
                case "카운트다운":
                    if (Session.Phase == Phase.Countdown) { int n = Mathf.CeilToInt(Session.CountdownLeft); LText(e, r, n > 0 ? n.ToString() : "GO!"); }
                    return;
                case "결과 순위":
                    LBox(r, e.Fill, e.Stroke, e.Radius);
                    LArea(r, LS(16), () => { _sRes = GUILayout.BeginScrollView(_sRes); ResultsTable(); GUILayout.EndScrollView(); });
                    return;
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


        static void DrawSortMenu()
        {
            float w = 150 * _ls, h = 40 * _ls;
            var box = new Rect(_lSortBtn.x, _lSortBtn.yMax + 6 * _ls, w * 2 + 12 * _ls, h * 5 + 12 * _ls);
            LBox(box, Hex("171a1f"), Hex("3a4452"), 12);
            for (int i = 0; i < Tuf.SortKeys.Length; i++)
            {
                var r = new Rect(box.x + 6 * _ls + (i % 2) * (w + 0 * _ls), box.y + 6 * _ls + (i / 2) * h, w, h - 4 * _ls);
                bool on = Tuf.Sort == Tuf.SortKeys[i];
                if (on) LBox(r, A(C_acc, 0.2f), C_acc, 8);
                GUI.Label(r, Tuf.SortNames[i], Label(LS(16), on ? C_acc : C_txt, FontStyle.Normal, TextAnchor.MiddleCenter));
                if (LClick(r)) { Tuf.Sort = Tuf.SortKeys[i]; _tufSortOpen = false; _tufDirty = true; }
            }
        }

        static void DrawRanking(Layout.El e, Rect r)
        {
            var board = Session.Board.Where(x => x.State >= PState.Loading && x.State != PState.Cancelled).ToList();
            if (board.Count == 0) board = Session.Board.ToList();
            float head = LS(e.Size) * 2.2f, rowH = LS(32);
            var box = new Rect(r.x, r.y, r.width, Mathf.Min(r.height, head + board.Count * rowH + LS(8)));
            LBox(box, e.Fill, e.Stroke, e.Radius);
            LText(e, box, Tok(e.Text));
            int fs = LS(Mathf.Max(12, e.Size - 1));
            for (int i = 0; i < board.Count; i++)
            {
                float ry = box.y + head + i * rowH;
                if (ry + rowH > r.yMax) break;
                var en = board[i];
                bool me = en.Id == Net.MyId;
                int slot = Session.Board.IndexOf(en);
                float x = box.x + LS(12), w = box.width - LS(24);
                if (me) Fill(new Rect(box.x + LS(6), ry, box.width - LS(12), rowH - LS(4)), A(C_acc, 0.10f));
                GUI.Label(new Rect(x, ry, LS(22), rowH - LS(4)), (i + 1).ToString(), Label(fs, i == 0 ? C_gold : C_sub, FontStyle.Bold));
                Dot(new Vector2(x + LS(30), ry + (rowH - LS(4)) / 2f), LS(5), SlotColor(slot < 0 ? i : slot));
                GUI.Label(new Rect(x + LS(42), ry, w * 0.5f, rowH - LS(4)), Trim(en.Name, 9) + (en.State == PState.Finished ? $"  <size={Mathf.Max(9, fs - 4)}>완주</size>" : ""), Label(fs, me ? C_acc : e.Color, me ? FontStyle.Bold : FontStyle.Normal));
                GUI.Label(new Rect(box.x, ry, box.width - LS(12), rowH - LS(4)), $"{en.Stats.Acc * 100:0.00}%", Label(fs, me ? C_acc : e.Color, FontStyle.Bold, TextAnchor.MiddleRight));
                Fill(new Rect(x + LS(42), ry + rowH - LS(8), (w - LS(42)) * Mathf.Clamp01(en.Stats.Progress), Mathf.Max(1, LS(2))), A(SlotColor(slot < 0 ? i : slot), 0.7f));
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

            bool results = inRoom && ShowResults && Session.Results.Count > 0 && Layout.Get("s_results") != null;
            bool drawer = _drawer && Layout.Get("s_maps") != null;
            bool popup = _settings || _profile || _confirmLeave;

            _lBlocked = results || drawer || popup;
            DrawLayout(main, true);
            if (results) { _lBlocked = drawer || popup; DrawLayout(Layout.Get("s_results"), false); }
            if (_drawer)
            {
                _lBlocked = popup;
                if (drawer) DrawLayout(Layout.Get("s_maps"), false); else DrawMapDrawer(UnityEngine.Screen.width, UnityEngine.Screen.height);
            }
            _lBlocked = false;

            // 아래 상태 줄 (연결 상태·오류는 항상 보이게)
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            string st = Esc(string.Join("   ·   ", new[] { Net.Status, inRoom ? "" : Session.Notice }.Where(s => !string.IsNullOrEmpty(s)).ToArray()));
            if (st.Length > 0) GUI.Label(new Rect(_lox + 28 * _ls, H - 26, W - 300, 22), st, S_small);
            GUI.Label(new Rect(W - 300, H - 26, 272, 22), "v" + Main.Version, Label(12, A(C_sub, 0.7f), FontStyle.Normal, TextAnchor.MiddleRight));
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
                Banner("모두 들어오면 바로 시작해요...");
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
            if (ph == Phase.Countdown || ph == Phase.Playing)
                GUI.Label(new Rect(UnityEngine.Screen.width - 260, UnityEngine.Screen.height - 30, 248, 24), "기권  Ctrl + Shift + Q", Label(12, C_sub, FontStyle.Normal, TextAnchor.MiddleRight));
            return true;
        }
    }
}
