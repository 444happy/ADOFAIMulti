using System;
using System.IO;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>UI 색 테마 프리셋 (설정 탭에서 변경, Mods/ADOFAIMulti/theme.txt 에 저장)</summary>
    public class Theme
    {
        public string Id, Name;
        public Color Bg, BgTop, Glow, Side, Card, CardHi, Field, Line, Acc, Acc2, AccText, Txt, Sub, Fire, Ice, Gold;

        static Color H(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        public static readonly Theme[] All =
        {
            // 기본: 누구나 무난한 중립 다크 + 차분한 파랑 (Steam/Discord 계열의 익숙한 톤)
            new Theme { Id = "default", Name = "기본",
                Bg = H("111318"), BgTop = H("181b22"), Glow = H("23324d"), Side = H("14161c"), Card = H("1c1f27"), CardHi = H("252a34"),
                Field = H("15181e"), Line = H("2a2f3a"), Acc = H("6aa8ff"), Acc2 = H("8fc0ff"), AccText = H("0c1220"),
                Txt = H("eceff4"), Sub = H("9aa3b2"), Fire = H("ff6a4d"), Ice = H("5ab8ff"), Gold = H("ffd36b") },

            new Theme { Id = "mint", Name = "민트",
                Bg = H("0a120f"), BgTop = H("0f1d18"), Glow = H("1d4d3b"), Side = H("0d1713"), Card = H("17231e"), CardHi = H("1f2f28"),
                Field = H("111b17"), Line = H("25352e"), Acc = H("74e0b0"), Acc2 = H("4fc3c9"), AccText = H("06110c"),
                Txt = H("e7f2ec"), Sub = H("88a196"), Fire = H("ff6a4d"), Ice = H("5ab8ff"), Gold = H("ffd36b") },

            new Theme { Id = "fireice", Name = "불과 얼음",
                Bg = H("0b0d18"), BgTop = H("141833"), Glow = H("3a1f4a"), Side = H("0e1122"), Card = H("171b33"), CardHi = H("20264a"),
                Field = H("11152a"), Line = H("272d52"), Acc = H("ff7a59"), Acc2 = H("5aa9ff"), AccText = H("ffffff"),
                Txt = H("eef0ff"), Sub = H("9aa0c8"), Fire = H("ff6a4d"), Ice = H("5ab8ff"), Gold = H("ffd36b") },

            new Theme { Id = "midnight", Name = "미드나잇",
                Bg = H("0c0a14"), BgTop = H("17122a"), Glow = H("3b2470"), Side = H("100d1c"), Card = H("1a1630"), CardHi = H("241e42"),
                Field = H("13102a"), Line = H("2c2650"), Acc = H("b69cff"), Acc2 = H("ff8fd0"), AccText = H("120c22"),
                Txt = H("f1edff"), Sub = H("a29bc4"), Fire = H("ff6a8a"), Ice = H("7ab8ff"), Gold = H("ffd36b") },

            new Theme { Id = "mono", Name = "모노",
                Bg = H("0d0d0f"), BgTop = H("17171b"), Glow = H("2a2a33"), Side = H("111114"), Card = H("1b1b20"), CardHi = H("25252c"),
                Field = H("141418"), Line = H("2c2c34"), Acc = H("f2f2f2"), Acc2 = H("b8b8c2"), AccText = H("0d0d0f"),
                Txt = H("f4f4f6"), Sub = H("9a9aa6"), Fire = H("ff6a4d"), Ice = H("5ab8ff"), Gold = H("ffd36b") },
        };

        public static Theme Current = All[0];
        public static event Action Changed;

        static string FilePath => Path.Combine(Main.Mod.Path, "theme.txt");

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var id = File.ReadAllText(FilePath).Trim();
                foreach (var t in All) if (t.Id == id) Current = t;
            }
            catch { }
        }

        public static void Set(Theme t)
        {
            if (t == Current) return;
            Current = t;
            try { File.WriteAllText(FilePath, t.Id); } catch { }
            Changed?.Invoke();
        }
    }
}
