using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ADOFAIMultiInstaller
{
    static class Program
    {
        const string ModId = "ADOFAIMulti";
        const string GameFolder = "A Dance of Fire and Ice";
        const string UmmUrl = "https://www.nexusmods.com/site/mods/21";

        static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Console.Title = "얼불춤 멀티 모드 설치기";
            try { Run(args); }
            catch (Exception ex)
            {
                Err("알 수 없는 오류: " + ex.Message);
            }
            Console.WriteLine();
            Console.WriteLine("아무 키나 누르면 닫힙니다...");
            try { Console.ReadKey(true); } catch { }
            return 0;
        }

        static void Run(string[] args)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("   얼불춤 멀티 모드 (ADOFAI Multi) 설치기");
            Console.WriteLine("   버전 " + ReadEmbeddedVersion());
            Console.WriteLine("==========================================");
            Console.WriteLine();

            // 1. 게임 폴더 찾기
            string game = FindGame();
            if (game == null)
            {
                Warn("얼불춤 설치 폴더를 자동으로 못 찾았습니다.");
                Console.WriteLine("Steam > 얼불춤 우클릭 > 관리 > 로컬 파일 보기 에서 폴더 경로를 복사해 붙여넣어 주세요.");
                Console.Write("경로: ");
                game = (Console.ReadLine() ?? "").Trim().Trim('"');
                if (!IsGameDir(game)) { Err("올바른 얼불춤 폴더가 아닙니다 (A Dance of Fire and Ice.exe 가 없음)."); return; }
            }
            Ok("게임 폴더: " + game);

            // 2. 게임 실행 중이면 중단
            if (Process.GetProcessesByName("A Dance of Fire and Ice").Length > 0)
            {
                Err("얼불춤이 실행 중입니다. 게임을 끄고 다시 실행해 주세요.");
                return;
            }

            // 3. UMM 확인
            string ummDir = Path.Combine(game, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager");
            if (!File.Exists(Path.Combine(ummDir, "UnityModManager.dll")))
            {
                Err("UnityModManager(UMM)가 설치되어 있지 않습니다.");
                Console.WriteLine("UMM을 먼저 설치한 뒤 이 설치기를 다시 실행해 주세요.");
                Console.WriteLine("다운로드 페이지를 열까요? (Y/N)");
                if (char.ToUpperInvariant(Console.ReadKey(true).KeyChar) == 'Y') OpenUrl(UmmUrl);
                return;
            }
            Ok("UnityModManager 확인");

            string modDir = Path.Combine(game, "Mods", ModId);
            bool installed = File.Exists(Path.Combine(modDir, ModId + ".dll"));

            // 4. 메뉴
            Console.WriteLine();
            Console.WriteLine(installed ? "  [1] 업데이트 (덮어쓰기)" : "  [1] 설치");
            if (installed) Console.WriteLine("  [2] 제거");
            Console.WriteLine("  [0] 취소");
            Console.Write("선택: ");
            var key = Console.ReadKey(true).KeyChar;
            Console.WriteLine(key);

            if (key == '1') Install(modDir, game);
            else if (key == '2' && installed) Uninstall(modDir);
            else Console.WriteLine("취소했습니다.");
        }

        static void Install(string modDir, string game)
        {
            try
            {
                Directory.CreateDirectory(modDir);
                // 캐시(받은 맵)는 남겨두고 dll/Info.json만 교체
                WriteResource(ModId + ".dll", Path.Combine(modDir, ModId + ".dll"));
                WriteResource("Info.json", Path.Combine(modDir, "Info.json"));
            }
            catch (UnauthorizedAccessException)
            {
                Err("폴더에 쓸 권한이 없습니다. 이 설치기를 우클릭 > '관리자 권한으로 실행' 해 주세요.");
                return;
            }
            Ok("설치 완료! → " + modDir);

            if (Directory.Exists(Path.Combine(game, "Mods", "TogetherBootstrap")))
                Warn("Together 모드가 깔려 있습니다. 충돌이 나면 UMM(Ctrl+F10)에서 Together를 꺼주세요.");

            Console.WriteLine();
            Console.WriteLine("사용법: 게임 실행 → Ctrl + M 으로 멀티 창 열기");
        }

        static void Uninstall(string modDir)
        {
            try
            {
                Directory.Delete(modDir, true);
                Ok("제거 완료 (받아둔 맵 캐시도 함께 삭제됨)");
            }
            catch (UnauthorizedAccessException)
            {
                Err("권한이 없습니다. 관리자 권한으로 실행해 주세요.");
            }
        }

        // ---------------- 게임 폴더 탐색 ----------------
        static string FindGame()
        {
            var libs = new List<string>();
            foreach (var steam in SteamRoots())
            {
                libs.Add(steam);
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            // 흔한 기본 위치도 확인
            foreach (var d in new[] { "C", "D", "E", "F" })
            {
                libs.Add(d + @":\Program Files (x86)\Steam");
                libs.Add(d + @":\SteamLibrary");
                libs.Add(d + @":\Steam");
            }
            foreach (var lib in libs)
            {
                var p = Path.Combine(lib, "steamapps", "common", GameFolder);
                if (IsGameDir(p)) return p;
            }
            return null;
        }

        static IEnumerable<string> SteamRoots()
        {
            var list = new List<string>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (k?.GetValue("SteamPath") is string s) list.Add(s.Replace('/', '\\'));
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    if (k?.GetValue("InstallPath") is string s) list.Add(s);
            }
            catch { }
            return list;
        }

        static bool IsGameDir(string p) =>
            !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, "A Dance of Fire and Ice.exe"));

        // ---------------- 리소스 ----------------
        static void WriteResource(string name, string dest)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("설치 파일이 손상됐습니다: " + name);
                using (var f = File.Create(dest)) s.CopyTo(f);
            }
        }

        static string ReadEmbeddedVersion()
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Info.json"))
                using (var r = new StreamReader(s))
                {
                    var m = Regex.Match(r.ReadToEnd(), "\"Version\"\\s*:\\s*\"([^\"]+)\"");
                    return m.Success ? m.Groups[1].Value : "?";
                }
            }
            catch { return "?"; }
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        static void Ok(string s) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("✔ " + s); Console.ResetColor(); }
        static void Warn(string s) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("! " + s); Console.ResetColor(); }
        static void Err(string s) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("✘ " + s); Console.ResetColor(); }
    }
}
