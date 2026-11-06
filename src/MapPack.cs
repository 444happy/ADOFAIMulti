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

        public static Task<bool> ExtractAsync(string hash)
        {
            return Task.Run(() =>
            {
                var part = PartPath(hash);
                var dir = MapDir(hash);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                ZipFile.ExtractToDirectory(part, dir);
                File.Delete(part);
                return HashFolder(dir) == hash; // 받은 내용 검증
            });
        }
    }
}
