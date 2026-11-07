using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// 여러 화면이 같이 쓰는 작은 부품: 하단 단축키 바, 알림 토스트, 모달 창 틀,
    /// 아바타, 키캡, 아이콘 + 글자, 판정 분포 막대.
    /// </summary>
    public static partial class UI
    {
        static Color C_ok;

        // 얼불춤 판정 색과 비슷하게 (너무 빠름/느림 빨강 · 빠름/느림 주황 · 약간 노랑 · 정확 초록)
        static Color J_too => Hex("ff5b5b");
        static Color J_very => Hex("ffa94d");
        static Color J_near => Hex("d9e86a");
        static Color J_perfect => Hex("5fd18b");
        static Color J_miss => Hex("b04a5a");

        // 순위 색 (금·은·동)
        static Color RankColor(int rank) => rank == 1 ? C_gold : rank == 2 ? Hex("d8dee6") : rank == 3 ? Hex("e2a06a") : C_sub;

        // ---------------- 아이콘 + 글자 ----------------
        /// <summary>"{i:gear} 설정" 처럼 맨 앞의 아이콘 표시를 떼어냄</summary>
        static string SplitIcon(string text, out string icon)
        {
            icon = null;
            if (text == null || !text.StartsWith("{i:")) return text;
            int end = text.IndexOf('}');
            if (end < 0) return text;
            icon = text.Substring(3, end - 3).Trim();
            return text.Substring(end + 1).TrimStart();
        }

        /// <summary>아이콘(있으면) + 글자를 스타일 정렬대로 한 줄에 그림</summary>
        static void IconLabel(Rect r, string icon, string text, GUIStyle st, Color? iconColor = null)
        {
            if (string.IsNullOrEmpty(icon) || Icons.Get(icon) == null) { GUI.Label(r, text, st); return; }
            float fs = st.fontSize > 0 ? st.fontSize : 14;
            float isz = Mathf.Round(fs * 1.2f), gap = string.IsNullOrEmpty(text) ? 0 : Mathf.Round(fs * 0.45f);
            float tw = string.IsNullOrEmpty(text) ? 0 : st.CalcSize(new GUIContent(text)).x;
            float total = Mathf.Min(r.width, isz + gap + tw);
            var a = st.alignment;
            bool center = a == TextAnchor.MiddleCenter || a == TextAnchor.UpperCenter || a == TextAnchor.LowerCenter;
            bool right = a == TextAnchor.MiddleRight || a == TextAnchor.UpperRight || a == TextAnchor.LowerRight;
            float x = center ? r.x + (r.width - total) / 2f : right ? r.xMax - total : r.x;
            bool top = a == TextAnchor.UpperLeft || a == TextAnchor.UpperCenter || a == TextAnchor.UpperRight;
            float iy = top ? r.y + (fs * 1.3f - isz) / 2f + 1 : r.y + (r.height - isz) / 2f;
            Icons.Draw(new Rect(x, iy, isz, isz), icon, iconColor ?? st.normal.textColor);
            if (tw > 0)
            {
                var ls = Label(st.fontSize, st.normal.textColor, st.fontStyle, top ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
                GUI.Label(new Rect(x + isz + gap, r.y, r.xMax - (x + isz + gap), r.height), text, ls);
            }
        }

        // ---------------- 아바타 (이름 첫 글자 + 슬롯 색 원) ----------------
        static readonly Dictionary<Color, Texture2D> _avTex = new Dictionary<Color, Texture2D>();
        static void Avatar(Rect r, string name, Color c, bool solid = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            var key = solid ? new Color(c.r, c.g, c.b, 0.999f) : c;
            if (!_avTex.TryGetValue(key, out var t) || t == null)
                _avTex[key] = t = Round(solid ? c : A(c, 0.16f), 31, A(c, solid ? 0 : 0.9f));
            GUI.DrawTexture(r, t);
            string ini = string.IsNullOrEmpty(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
            GUI.Label(r, Esc(ini), Label(Mathf.Max(10, Mathf.RoundToInt(r.height * 0.44f)), solid ? Hex("0d0f12") : c, FontStyle.Bold, TextAnchor.MiddleCenter));
        }

        // ---------------- 키캡 ----------------
        /// <summary>[F5] 같은 키 모양. 오른쪽 끝 x를 돌려줌</summary>
        static float Keycap(float x, float cy, string key, float scale = 1)
        {
            var st = Label(Mathf.Max(9, Mathf.RoundToInt(12 * scale)), C_txt, FontStyle.Bold, TextAnchor.MiddleCenter);
            float h = Mathf.Round(22 * scale), w = Mathf.Max(h + 2, st.CalcSize(new GUIContent(key)).x + 14 * scale);
            var r = new Rect(x, cy - h / 2f, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(r, Hex("1f242c"), Hex("3a4452"), 5 * scale);
                Fill(new Rect(r.x + 2, r.yMax - 2, r.width - 4, 1), A(Color.black, 0.35f));
            }
            GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), key, st);
            return r.xMax;
        }

        /// <summary>둥근 상자 (픽셀 단위 반지름, 설계 배율 무시)</summary>
        static void RoundBox(Rect r, Color fill, Color stroke, float radiusPx)
        {
            float o = _ls; _ls = 1;
            LBox(r, fill, stroke, radiusPx);
            _ls = o;
        }

        // ---------------- 하단 바 (단축키 + 연결 상태 + 버전) ----------------
        static void DrawFooter(bool inRoom)
        {
            LBegin();
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height, s = Mathf.Max(0.75f, _ls);
            float fh = Mathf.Round(40 * s);
            var r = new Rect(0, H - fh, W, fh);
            Fill(r, A(Hex("08090b"), 0.92f));
            Fill(new Rect(0, r.y, W, 1), A(C_line, 0.9f));
            float cy = r.y + fh / 2f, x = _lox + 32 * s;
            var lab = Label(Mathf.RoundToInt(13 * s), C_sub);

            var keys = new List<string[]>();
            bool popup = _settings || _profile || _confirmLeave || _pwMode != 0;
            if (popup) keys.Add(new[] { "Esc", "닫기" });
            else if (_drawer) keys.Add(new[] { "Esc", "닫기" });
            else if (inRoom)
            {
                if (Session.CanReady) keys.Add(new[] { "F5", Session.MyReady ? "준비 취소" : "준비" });
                keys.Add(new[] { "Esc", "방 나가기" });
            }
            else keys.Add(new[] { "Esc", "메인 메뉴" });

            foreach (var k in keys)
            {
                x = Keycap(x, cy, k[0], s) + 8 * s;
                float w = lab.CalcSize(new GUIContent(k[1])).x;
                GUI.Label(new Rect(x, r.y, w + 4, fh), k[1], lab);
                x += w + 24 * s;
            }

            // 연결 상태 (오류·진행 메시지)
            string st = Esc(Net.Status ?? "");
            if (st.Length > 0)
            {
                Fill(new Rect(x - 12 * s, r.y + fh * 0.3f, 1, fh * 0.4f), C_line);
                GUI.Label(new Rect(x + 4 * s, r.y, W - x - 240 * s, fh), st, Label(Mathf.RoundToInt(13 * s), A(C_sub, 0.9f)));
            }
            GUI.Label(new Rect(W - _lox - 232 * s, r.y, 200 * s, fh), "ADOFAI Multi  v" + Main.Version, Label(Mathf.RoundToInt(12 * s), A(C_sub, 0.6f), FontStyle.Normal, TextAnchor.MiddleRight));
        }

        // ---------------- 알림 토스트 (Session.Notice 가 바뀔 때 잠깐) ----------------
        static string _toastSeen = "", _toastText = "";
        static float _toastAt = -99;
        const float ToastTime = 3.6f;

        static void DrawToast()
        {
            var n = Session.Notice ?? "";
            if (n != _toastSeen) { _toastSeen = n; if (n.Length > 0) { _toastText = n; _toastAt = Time.unscaledTime; } }
            float t = Time.unscaledTime - _toastAt;
            if (t > ToastTime || _toastText.Length == 0) return;
            float a = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((ToastTime - t) / 0.5f);
            float slide = (1 - Mathf.Clamp01(t / 0.22f)) * -12;
            LBegin();
            float s = Mathf.Max(0.75f, _ls);
            var st = Label(Mathf.RoundToInt(16 * s), C_txt, FontStyle.Bold);
            string text = Esc(_toastText);
            float w = Mathf.Min(UnityEngine.Screen.width - 80, st.CalcSize(new GUIContent(text)).x + 72 * s), h = Mathf.Round(48 * s);
            var r = new Rect((UnityEngine.Screen.width - w) / 2f, _loy + 92 * s + slide * s, w, h);
            var o = GUI.color; GUI.color = A(o, o.a * a);
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(new Rect(r.x, r.y + 3, r.width, r.height), A(Color.black, 0.35f), Color.clear, h / 2f);
                RoundBox(r, Hex("20262f"), A(C_acc, 0.55f), h / 2f);
            }
            Dot(new Vector2(r.x + 26 * s, r.y + h / 2f), 5 * s, C_acc);
            GUI.Label(new Rect(r.x + 42 * s, r.y, r.width - 56 * s, h), text, st);
            GUI.color = o;
        }

        // ---------------- 모달 창 틀 ----------------
        static GUIStyle S_modal;
        /// <summary>어둡게 깔고 가운데 창 + 제목 + 닫기(X). 밖을 누르면 닫힘. 내용 영역을 돌려줌 (닫혔으면 width 0)</summary>
        static Rect Modal(float w, float h, string title, string icon, System.Action close, string subtitle = null)
        {
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            GUI.DrawTexture(new Rect(0, 0, W, H), T_dim);
            h = Mathf.Min(h, H - 80);
            var r = new Rect(Mathf.Round((W - w) / 2f), Mathf.Round((H - h) / 2f), w, h);
            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition)) { close?.Invoke(); Event.current.Use(); return new Rect(); }
            if (S_modal == null || S_modal.normal.background == null || _modalVer != _builtVer)
            {
                _modalVer = _builtVer;
                S_modal = Box(Round(Hex("171b21"), 16, A(Color.white, 0.10f)), 16, new RectOffset(0, 0, 0, 0));
            }
            if (Event.current.type == EventType.Repaint)
            {
                RoundBox(new Rect(r.x - 2, r.y + 6, r.width + 4, r.height + 4), A(Color.black, 0.35f), Color.clear, 18);
                S_modal.Draw(r, false, false, false, false);
            }
            float tx = r.x + 28;
            if (!string.IsNullOrEmpty(icon)) { Icons.Draw(new Rect(tx, r.y + 26, 24, 24), icon, C_acc); tx += 34; }
            GUI.Label(new Rect(tx, r.y + 20, r.width - (tx - r.x) - 80, 36), title, Label(22, C_txt, FontStyle.Bold));
            float headH = 72;
            if (!string.IsNullOrEmpty(subtitle)) { GUI.Label(new Rect(r.x + 28, r.y + 56, r.width - 56, 22), subtitle, Label(14, C_sub)); headH = 92; }
            var cr = new Rect(r.xMax - 56, r.y + 18, 38, 38);
            bool hov = cr.Contains(Event.current.mousePosition);
            if (hov && Event.current.type == EventType.Repaint) RoundBox(cr, A(Color.white, 0.08f), Color.clear, 19);
            Icons.Draw(new Rect(cr.x + 9, cr.y + 9, 20, 20), "close", hov ? C_txt : C_sub);
            if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) { close?.Invoke(); return new Rect(); }
            Fill(new Rect(r.x, r.y + headH, r.width, 1), A(Color.white, 0.07f));
            return new Rect(r.x + 28, r.y + headH + 20, r.width - 56, r.height - headH - 44);
        }
        static int _modalVer = -1;

        /// <summary>모달 아래 버튼 줄 (오른쪽 정렬). 누른 버튼 번호, 없으면 -1</summary>
        static int ModalButtons(Rect content, params (string text, bool primary, bool enabled)[] btns)
        {
            // 모든 팝업 버튼을 같은 크기로 (가장 긴 글자에 맞춘 폭, 같은 높이·글자 크기)
            float bh = 46, w = 120, x = content.xMax, y = content.yMax - bh;
            foreach (var b in btns) w = Mathf.Max(w, FreeH(b.primary ? S_btnAcc : S_btn).CalcSize(new GUIContent(b.text)).x + 24);
            int hit = -1;
            for (int i = btns.Length - 1; i >= 0; i--)
            {
                var b = btns[i];
                var st = FreeH(b.primary ? S_btnAcc : S_btn);
                x -= w;
                var o = GUI.enabled; GUI.enabled = o && b.enabled;
                if (GUI.Button(new Rect(x, y, w, bh), b.text, st)) hit = i;
                GUI.enabled = o;
                x -= 10;
            }
            return hit;
        }

        static readonly Dictionary<GUIStyle, GUIStyle> _freeH = new Dictionary<GUIStyle, GUIStyle>();
        /// <summary>스타일마다 고정 높이(40/46)·글자 크기가 달라서, 높이를 풀고 글자를 맞춘 복사본</summary>
        static GUIStyle FreeH(GUIStyle s)
        {
            if (!_freeH.TryGetValue(s, out var st)) _freeH[s] = st = new GUIStyle(s) { fixedHeight = 0, fixedWidth = 0, fontSize = 15, fontStyle = FontStyle.Bold };
            return st;
        }

        /// <summary>모두 같은 크기인 버튼들 (오른쪽 정렬). 누른 번호, 없으면 -1</summary>
        static int ModalButtonsSame(Rect content, params (string text, GUIStyle style)[] btns)
        {
            float bh = 46, w = 120;
            foreach (var b in btns) w = Mathf.Max(w, FreeH(b.style).CalcSize(new GUIContent(b.text)).x + 24);
            float x = content.xMax, y = content.yMax - bh;
            int hit = -1;
            for (int i = btns.Length - 1; i >= 0; i--)
            {
                x -= w;
                if (GUI.Button(new Rect(x, y, w, bh), btns[i].text, FreeH(btns[i].style))) hit = i;
                x -= 10;
            }
            return hit;
        }

        // ---------------- 판정 분포 막대 ----------------
        static void JudgeBar(Rect r, Stats s)
        {
            int[] n = { s.TooEarly, s.VeryEarly, s.EarlyPerfect, s.Perfect, s.LatePerfect, s.VeryLate, s.TooLate, s.Miss + s.Overload };
            Color[] c = { J_too, J_very, J_near, J_perfect, J_near, J_very, J_too, J_miss };
            int total = n.Sum();
            Fill(r, A(C_line, 0.8f));
            if (total <= 0) return;
            float x = r.x;
            for (int i = 0; i < n.Length; i++)
            {
                if (n[i] <= 0) continue;
                float w = Mathf.Max(2, r.width * n[i] / total);
                if (x + w > r.xMax) w = r.xMax - x;
                Fill(new Rect(x, r.y, w, r.height), c[i]);
                x += w;
            }
        }

        static string JudgeNums(Stats s) =>
            $"<color=#{HexOf(J_too)}>{s.TooEarly}</color> <color=#{HexOf(J_very)}>{s.VeryEarly}</color> <color=#{HexOf(J_near)}>{s.EarlyPerfect}</color>  " +
            $"<color=#{HexOf(J_perfect)}><b>{s.Perfect}</b></color>  " +
            $"<color=#{HexOf(J_near)}>{s.LatePerfect}</color> <color=#{HexOf(J_very)}>{s.VeryLate}</color> <color=#{HexOf(J_too)}>{s.TooLate}</color>";

        // ---------------- 판정 아이콘 (얼불춤 판정 선택 화면의 과녁 그림) ----------------
        // 게임은 RDConstants.data.bullseyeSprites[느슨함 0 · 보통 1 · 엄격함 2] 를 판정 아이콘으로 씀 → 같은 그림을 가져다 그림
        static Sprite[] _bullseye;
        static float _bullseyeAt = -99;

        static Sprite JudgeSprite(int j)
        {
            if (_bullseye == null && Time.unscaledTime - _bullseyeAt > 2f)
            {
                _bullseyeAt = Time.unscaledTime;
                try
                {
                    var d = RDConstants.data;
                    if (d != null && d.bullseyeSprites != null && d.bullseyeSprites.Length >= 3) _bullseye = d.bullseyeSprites;
                }
                catch (System.Exception ex) { Main.Log("판정 아이콘을 못 가져옴: " + ex.Message); _bullseyeAt = float.MaxValue; }
            }
            return _bullseye != null && j >= 0 && j < _bullseye.Length ? _bullseye[j] : null;
        }

        /// <summary>판정 j 아이콘을 r 안에 비율 유지해서 그림 (게임 그림이 없으면 직접 그린 과녁)</summary>
        static void JudgeIcon(Rect r, int j)
        {
            if (Event.current.type != EventType.Repaint) return;
            var sp = JudgeSprite(j);
            if (sp != null && sp.texture != null)
            {
                var tex = sp.texture; var tr = sp.textureRect;
                float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
                var fit = new Rect(r.center.x - tr.width * k / 2f, r.center.y - tr.height * k / 2f, tr.width * k, tr.height * k);
                GUI.DrawTextureWithTexCoords(fit, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                return;
            }
            // 대신 그리는 과녁: 느슨할수록 가운데가 큼
            var c = j == 0 ? C_ok : j == 1 ? C_gold : J_too;
            float R = Mathf.Min(r.width, r.height) / 2f;
            Dot(r.center, R, A(c, 0.22f));
            Dot(r.center, R * 0.68f, A(c, 0.45f));
            Dot(r.center, R * (j == 0 ? 0.48f : j == 1 ? 0.34f : 0.2f), c);
        }

        // ---------------- 섹션 머리말 (작고 굵은 회색) ----------------
        static void Overline(Rect r, string text, Color? c = null) => GUI.Label(r, text, Label(13, c ?? C_sub, FontStyle.Bold));
    }
}
