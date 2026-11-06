using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ADOFAIMulti
{
    /// <summary>맵 폴더 해시/압축/해제. 무거운 작업이라 백그라운드 스레드에서 돈다.</summary>
    public static class MapPack
    {
        public static string Root => Path.Combine(Main.Mod.Path, "Cache");
        public static string MapDir(string hash) => Path.Combine(Root, hash);
        public static string ZipPath(string hash) => Path.Combine(Root, hash + ".zip");
        public static string PartPath(string hash) => Path.Combine(Root, hash + ".part");

        public class Packed
        {
            public string Hash, ZipFile, RelLevelFile, Title;
            public long Size;
        }

        /// <summary>파일 내용 기반 해시 (zip 타임스탬프와 무관하게 같은 맵이면 같은 값)</summary>
        public static string HashFolder(string dir)
        {
            using (var sha = SHA256.Create())
            {
                var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/'))
                    .OrderBy(f => f, StringComparer.Ordinal).ToList();
                var buf = new byte[1 << 20];
                foreach (var rel in files)
                {
                    var nameBytes = Encoding.UTF8.GetBytes(rel + "\n");
                    sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
                    using (var fs = File.OpenRead(Path.Combine(dir, rel)))
                    {
                        int n;
                        while ((n = fs.Read(buf, 0, buf.Length)) > 0) sha.TransformBlock(buf, 0, n, null, 0);
                    }
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").Substring(0, 24).ToLowerInvariant();
            }
        }

        public static Task<Packed> PackAsync(string levelFile)
        {
            return Task.Run(() =>
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(levelFile));
                var hash = HashFolder(dir);
                Directory.CreateDirectory(Root);
                var zip = ZipPath(hash);
                if (!File.Exists(zip))
                {
                    var tmp = zip + ".tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);
                    ZipFile.CreateFromDirectory(dir, tmp, CompressionLevel.Fastest, false);
                    File.Move(tmp, zip);
                }
                // 방장도 캐시 폴더 기준으로 같은 경로를 쓰도록 복사본 유지는 하지 않고 원본 경로 사용
                return new Packed
                {
                    Hash = hash,
                    ZipFile = zip,
                    RelLevelFile = Path.GetFileName(levelFile),
                    Title = Path.GetFileName(dir),
                    Size = new FileInfo(zip).Length
                };
            });
        }

        public static bool Has(string hash, string relLevelFile) => File.Exists(Path.Combine(MapDir(hash), relLevelFile));

        public static string LevelPath(string hash, string relLevelFile) => Path.Combine(MapDir(hash), relLevelFile);

        public static bool IsTuf(string hash) => hash != null && hash.StartsWith("tuf");

        public static Task<bool> ExtractAsync(string hash)
        {
            return Task.Run(() =>
            {
                var part = PartPath(hash);
                var dir = MapDir(hash);
                SafeExtract(part, dir);
                File.Delete(part);
                // 방장이 직접 묶은 맵은 내용 해시로 검증, TUF 맵은 .adofai 가 있는지만 확인
                return IsTuf(hash) ? FindLevelFile(dir) != null : HashFolder(dir) == hash;
            });
        }

        /// <summary>
        /// zip 풀기. 이름에 쓸 수 없는 문자가 섞인 항목(다른 언어 인코딩 등)도 치환해서 풀고,
        /// 폴더 밖으로 나가는 경로(../)는 막는다.
        /// </summary>
        public static void SafeExtract(string zipPath, string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            string root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
            var bad = Path.GetInvalidFileNameChars();
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var e in zip.Entries)
                {
                    var parts = e.FullName.Replace('\\', '/').Split('/')
                        .Where(p => p.Length > 0 && p != "." && p != "..")
                        .Select(p => new string(p.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()).Trim())
                        .Where(p => p.Length > 0).ToArray();
                    if (parts.Length == 0) continue;
                    var target = Path.GetFullPath(Path.Combine(dir, Path.Combine(parts)));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var src = e.Open())
                    using (var dst = File.Create(target)) src.CopyTo(dst);
                }
            }
        }

        /// <summary>폴더 안의 실제 레벨 파일 (backup 제외, 얕은 경로 우선, 같으면 큰 파일). dir 기준 상대경로</summary>
        public static string FindLevelFile(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            var best = Directory.GetFiles(dir, "*.adofai", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).IndexOf("backup", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(f => f.Substring(dir.Length).Count(c => c == Path.DirectorySeparatorChar || c == '/'))
                .ThenByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
            if (best == null) return null;
            return best.Substring(Path.GetFullPath(dir).TrimEnd('\\', '/').Length).TrimStart('\\', '/');
        }
    }
}
