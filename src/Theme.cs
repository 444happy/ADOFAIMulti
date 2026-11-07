using System;
using System.IO;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>
    /// UI 색 테마. 프리셋 3종(기본 / 불과 얼음 / 모노) + 사용자가 색 5개를 고르는 커스텀.
    /// 나머지 색(그라데이션, 테두리, 보조 글자 등)은 기본 5색에서 자동 계산한다.
    /// 저장: Mods/ADOFAIMulti/theme.txt (선택한 테마 id), custom_theme.txt (커스텀 5색)
    /// </summary>
    public class Theme
    {
        public string Id, Name;
        // 기본 5색 (커스텀에서 편집)
        public Color Bg, Card, Txt, Acc, Acc2;
        // 파생 색
        public Color BgTop, Glow, Side, CardHi, Field, Line, AccText, Sub, Fire, Ice, Gold, Ok;

        static Color H(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        static Color Mix(Color a, Color b, float t) { var c = Color.Lerp(a, b, t); c.a = 1; return c; }
        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>기본 5색으로 테마 전체 색을 만든다</summary>
        public static Theme FromBase(string id, string name, Color bg, Color card, Color txt, Color acc, Color acc2)
        {
            var t = new Theme { Id = id, Name = name };
            t.ApplyBase(bg, card, txt, acc, acc2);
            return t;
        }

        public void ApplyBase(Color bg, Color card, Color txt, Color acc, Color acc2)
        {
            bg.a = card.a = txt.a = acc.a = acc2.a = 1;
            Bg = bg; Card = card; Txt = txt; Acc = acc; Acc2 = acc2;
            bool dark = Lum(bg) < 0.5f;
            var lift = dark ? Color.white : Color.black;
            BgTop = Mix(bg, lift, 0.04f);
            Side = Mix(bg, lift, 0.02f);
            Glow = Mix(bg, acc, 0.28f);
            CardHi = Mix(card, lift, 0.05f);
            Field = Mix(card, dark ? Color.black : Color.white, 0.18f);
            Line = Mix(card, txt, 0.10f);
            Sub = Mix(txt, bg, 0.40f);
            AccText = Lum(acc) > 0.55f ? Mix(bg, Color.black, 0.3f) : Color.white;
            Fire = H("ff7a45"); Ice = H("5ab8ff"); Gold = H("ffd166"); Ok = H("5fd18b");
        }

        /// <summary>설계판(layout.json) 색과 같은 슬레이트 + 하늘색</summary>
        public static readonly Theme Slate = FromBase("slate", "기본", H("0d0f12"), H("1b1f26"), H("e8ecf1"), H("5ab8ff"), H("7cc6ff"));
        public static readonly Theme FireIce = FromBase("fireice", "불과 얼음", H("0b0d18"), H("171b33"), H("eef0ff"), H("ff7a59"), H("5aa9ff"));
        public static readonly Theme Mono = FromBase("mono", "모노", H("0d0d0f"), H("1b1b20"), H("f4f4f6"), H("f2f2f2"), H("b8b8c2"));
        public static readonly Theme Custom = FromBase("custom", "커스텀", H("101418"), H("1c222a"), H("eceff4"), H("6aa8ff"), H("c58bff"));

        public static readonly Theme[] All = { FireIce, Mono, Custom };

        /// <summary>처음 값: 불과 얼음 ("기본" 테마는 목록에서 뺐고, 예전에 기본을 골랐던 사람도 불과 얼음으로)</summary>
        public static Theme Current = FireIce;
        /// <summary>커스텀 색이 바뀔 때마다 증가 → UI가 스타일을 다시 만든다</summary>
        public static int Version;

        static string Dir => Main.Mod.Path;
        static bool _dirty; static float _dirtyAt;

        public static void Load()
        {
            try
            {
                var cf = Path.Combine(Dir, "custom_theme.txt");
                if (File.Exists(cf))
                {
                    var p = File.ReadAllText(cf).Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 5) Custom.ApplyBase(H(p[0]), H(p[1]), H(p[2]), H(p[3]), H(p[4]));
                }
                var tf = Path.Combine(Dir, "theme.txt");
                if (File.Exists(tf))
                {
                    var id = File.ReadAllText(tf).Trim();
                    foreach (var t in All) if (t.Id == id) Current = t;
                }
            }
            catch { }
        }

        public static void Set(Theme t)
        {
            if (t == Current) return;
            Current = t;
            Version++;
            try { File.WriteAllText(Path.Combine(Dir, "theme.txt"), t.Id); } catch { }
        }

        /// <summary>커스텀 색 변경 (저장은 잠시 후 한 번에)</summary>
        public static void EditCustom(Color bg, Color card, Color txt, Color acc, Color acc2)
        {
            Custom.ApplyBase(bg, card, txt, acc, acc2);
            if (Current != Custom) Set(Custom);
            Version++;
            _dirty = true; _dirtyAt = Time.unscaledTime;
        }

        /// <summary>Runner.Update에서 호출: 슬라이더를 끄는 동안 매 프레임 파일을 쓰지 않도록 0.7초 뒤 저장</summary>
        public static void SaveIfDirty()
        {
            if (!_dirty || Time.unscaledTime - _dirtyAt < 0.7f) return;
            _dirty = false;
            string Hx(Color c) => ColorUtility.ToHtmlStringRGB(c);
            try { File.WriteAllText(Path.Combine(Dir, "custom_theme.txt"), $"{Hx(Custom.Bg)} {Hx(Custom.Card)} {Hx(Custom.Txt)} {Hx(Custom.Acc)} {Hx(Custom.Acc2)}"); } catch { }
        }
    }
}
