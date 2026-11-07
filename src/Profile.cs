using System.IO;

namespace ADOFAIMulti
{
    /// <summary>프로필: 멀티에서 쓸 닉네임 (비우면 Steam 이름). 저장: Mods/ADOFAIMulti/profile.txt</summary>
    public static class Profile
    {
        public static string Nick = "";
        static string FilePath => Path.Combine(Main.Mod.Path, "profile.txt");

        public static void Load()
        {
            try { if (File.Exists(FilePath)) Nick = Clean(File.ReadAllText(FilePath)); } catch { }
        }

        public static void Save(string nick)
        {
            Nick = Clean(nick);
            try { File.WriteAllText(FilePath, Nick); } catch { }
        }

        static string Clean(string s)
        {
            s = (s ?? "").Replace("\n", " ").Replace("\r", " ").Replace("<", "").Replace(">", "").Trim();
            return s.Length > 20 ? s.Substring(0, 20) : s;
        }
    }
}
