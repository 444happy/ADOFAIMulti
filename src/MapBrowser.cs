using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace ADOFAIMulti
{
    /// <summary>워크샵/게임 폴더에서 .adofai 맵 목록을 찾아주는 간단한 브라우저</summary>
    public static class MapBrowser
    {
        public class Item
        {
            public string Path, Song, Artist, Author, Folder;
            public string Label => string.IsNullOrEmpty(Song) ? Folder : $"{Artist} - {Song}";
        }

        public static List<Item> Items = new List<Item>();
        public static bool Scanning;
        static bool _scanned;

        static readonly Regex RxSong = new Regex("\"song\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        static readonly Regex RxArtist = new Regex("\"artist\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        static readonly Regex RxAuthor = new Regex("\"author\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        static readonly Regex RxTag = new Regex("<[^>]+>");

        public static IEnumerable<string> Roots()
        {
            var data = Application.dataPath; // .../common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data
            yield return System.IO.Path.GetFullPath(System.IO.Path.Combine(data, "..", "..", "..", "workshop", "content", "977950"));
            yield return System.IO.Path.GetFullPath(System.IO.Path.Combine(data, ".."));
            yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            yield return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static void ScanOnce() { if (!_scanned) Rescan(); }

        public static void Rescan()
        {
            if (Scanning) return;
            _scanned = true;
            Scanning = true;
            Task.Run(() =>
            {
                var list = new List<Item>();
                foreach (var root in Roots().Distinct())
                {
                    try
                    {
                        if (!Directory.Exists(root)) continue;
                        foreach (var f in SafeFind(root, 3))
                        {
                            try { list.Add(Read(f)); } catch { }
                        }
                    }
                    catch { }
                }
                var sorted = list.GroupBy(i => i.Path).Select(g => g.First()).OrderBy(i => i.Label, StringComparer.OrdinalIgnoreCase).ToList();
                MainThread.Post(() => { Items = sorted; Scanning = false; });
            });
        }

        static IEnumerable<string> SafeFind(string dir, int depth)
        {
            string[] files = new string[0], dirs = new string[0];
            try { files = Directory.GetFiles(dir, "*.adofai"); } catch { }
            foreach (var f in files)
                if (!f.EndsWith("backup.adofai", StringComparison.OrdinalIgnoreCase)) yield return f;
            if (depth <= 0) yield break;
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (var d in dirs)
            {
                var name = System.IO.Path.GetFileName(d);
                if (name.StartsWith(".") || name.EndsWith("_Data") || name == "Mods" || name == "MonoBleedingEdge") continue;
                foreach (var f in SafeFind(d, depth - 1)) yield return f;
            }
        }

        static Item Read(string file)
        {
            // 앞부분 settings만 읽으면 충분 (맵 데이터는 클 수 있음)
            string head;
            using (var r = new StreamReader(file))
            {
                var buf = new char[16384];
                int n = r.Read(buf, 0, buf.Length);
                head = new string(buf, 0, n);
            }
            return new Item
            {
                Path = file,
                Folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file)) + "/" + System.IO.Path.GetFileName(file),
                Song = Clean(RxSong.Match(head)),
                Artist = Clean(RxArtist.Match(head)),
                Author = Clean(RxAuthor.Match(head)),
            };
        }

        static string Clean(Match m) => m.Success ? RxTag.Replace(Regex.Unescape(m.Groups[1].Value), "").Trim() : "";
    }
}
