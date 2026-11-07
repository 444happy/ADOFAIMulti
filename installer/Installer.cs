using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ADOFAIMultiInstaller
{
    /// <summary>
    /// 일반적인 Windows 설치 마법사 모양의 설치기 (exe 하나).
    /// 안에 든 것: 모드(dll, Info.json, layout.json) + UnityModManager(Doorstop 방식) 파일.
    /// 시작 → 설치 위치 → 설치 중 → 완료
    /// </summary>
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Wizard());
        }
    }

    class Wizard : Form
    {
        const string ModId = "ADOFAIMulti";
        const string AppName = "ADOFAI Multi";
        const string GameName = "A Dance of Fire and Ice";
        const string SteamAppId = "977950";

        readonly string _ver = EmbeddedVersion();
        string _game;
        int _page;           // 0 시작, 1 설치 위치, 2 설치 중, 3 완료
        bool _uninstallDone;

        readonly float _s;
        int S(float v) => (int)Math.Round(v * _s);

        readonly Font _font, _fHead, _fBig;
        Label _headTitle, _headDesc;
        Panel _body;
        Button _back, _next, _cancel;

        // 페이지별 컨트롤
        RadioButton _rInstall, _rRemove;
        TextBox _path;
        CheckBox _cMod, _cUmm, _cLaunch;
        Label _space, _progLabel;
        ProgressBar _prog;
        ListBox _log;

        public Wizard()
        {
            using (var g = CreateGraphics()) _s = g.DpiX / 96f;
            _font = SystemFonts.MessageBoxFont;
            _fHead = new Font(_font.FontFamily, 9.5f, FontStyle.Bold);
            _fBig = new Font(_font.FontFamily, 13f, FontStyle.Bold);
            Font = _font;

            Text = AppName + " " + _ver + " 설치";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(500), S(390));
            BackColor = SystemColors.Control;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // 위쪽 흰 머리말
            var head = new Panel { Dock = DockStyle.Top, Height = S(64), BackColor = Color.White };
            _headTitle = new Label { Font = _fHead, AutoSize = false, Location = new Point(S(20), S(12)), Size = new Size(S(460), S(20)), BackColor = Color.White };
            _headDesc = new Label { AutoSize = false, Location = new Point(S(34), S(34)), Size = new Size(S(446), S(20)), BackColor = Color.White };
            head.Controls.Add(_headTitle); head.Controls.Add(_headDesc);
            var line1 = new Label { Dock = DockStyle.Top, Height = 2, BorderStyle = BorderStyle.Fixed3D };

            // 아래쪽 버튼 줄
            var foot = new Panel { Dock = DockStyle.Bottom, Height = S(48) };
            var line2 = new Label { Dock = DockStyle.Bottom, Height = 2, BorderStyle = BorderStyle.Fixed3D };
            foot.Controls.Add(new Label { Text = AppName + " " + _ver, ForeColor = SystemColors.GrayText, AutoSize = true, Location = new Point(S(12), S(17)) });
            _back = Btn("< 뒤로(&B)", S(222)); _next = Btn("다음(&N) >", S(304)); _cancel = Btn("취소", S(400));
            foot.Controls.Add(_back); foot.Controls.Add(_next); foot.Controls.Add(_cancel);
            _back.Click += (a, b) => Go(_page - 1);
            _next.Click += (a, b) => Next();
            _cancel.Click += (a, b) => Close();

            _body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(24), S(16), S(24), S(8)) };

            Controls.Add(_body); Controls.Add(line1); Controls.Add(head); Controls.Add(line2); Controls.Add(foot);
            AcceptButton = _next; CancelButton = _cancel;

            _game = FindGame();
            Go(0);
        }

        Button Btn(string t, int x) => new Button { Text = t, Location = new Point(x, S(12)), Size = new Size(S(80), S(25)), UseVisualStyleBackColor = true };

        // ---------------- 상태 ----------------
        string UmmDir => Path.Combine(_game, GameName + "_Data", "Managed", "UnityModManager");
        string ModDir => Path.Combine(_game, "Mods", ModId);
        bool HasUmm => _game != null && File.Exists(Path.Combine(UmmDir, "UnityModManager.dll"));
        string InstalledVersion
        {
            get
            {
                if (_game == null) return null;
                try
                {
                    var f = Path.Combine(ModDir, "Info.json");
                    if (!File.Exists(f) || !File.Exists(Path.Combine(ModDir, ModId + ".dll"))) return null;
                    var m = Regex.Match(File.ReadAllText(f), "\"Version\"\\s*:\\s*\"([^\"]+)\"");
                    return m.Success ? m.Groups[1].Value : "?";
                }
                catch { return null; }
            }
        }

        // ---------------- 페이지 ----------------
        void Head(string title, string desc) { _headTitle.Text = title; _headDesc.Text = desc; }

        Label Text1(string t, int y, int h = 0)
        {
            var l = new Label { Text = t, AutoSize = h == 0, Location = new Point(S(24), y) };
            if (h > 0) l.Size = new Size(S(452), h); else l.MaximumSize = new Size(S(452), 0);
            _body.Controls.Add(l);
            return l;
        }

        void Go(int page)
        {
            if (page < 0) return;
            _page = page;
            _body.Controls.Clear();
            _back.Enabled = page == 1;
            _next.Enabled = true; _cancel.Enabled = true;
            _next.Text = "다음(&N) >";

            if (page == 0)
            {
                Head(AppName + " 설치 마법사", "얼불춤(A Dance of Fire and Ice) 멀티플레이 모드");
                var iv = InstalledVersion;
                Text1(AppName + " " + _ver + " 을(를) 설치합니다.", S(16));
                Text1("UnityModManager가 없으면 함께 설치됩니다.\r\n설치하기 전에 얼불춤을 종료해 주세요.", S(44));
                if (iv != null)
                {
                    Text1("이미 v" + iv + " 이(가) 설치되어 있습니다.", S(100));
                    _rInstall = new RadioButton { Text = iv == _ver ? "다시 설치" : "업데이트 (v" + iv + " → v" + _ver + ")", Checked = true, AutoSize = true, Location = new Point(S(36), S(126)) };
                    _rRemove = new RadioButton { Text = "제거", AutoSize = true, Location = new Point(S(36), S(152)) };
                    _body.Controls.Add(_rInstall); _body.Controls.Add(_rRemove);
                }
                else { _rInstall = null; _rRemove = null; }
                Text1("계속하려면 [다음]을 누르세요.", S(200));
            }
            else if (page == 1)
            {
                Head("설치 위치 선택", "얼불춤이 설치된 폴더를 확인하세요.");
                Text1("얼불춤 폴더 (" + GameName + ".exe 가 있는 폴더):", S(12));
                _path = new TextBox { Text = _game ?? "", Location = new Point(S(24), S(34)), Width = S(360) };
                _path.TextChanged += (a, b) => { _game = IsGameDir(_path.Text.Trim().Trim('"')) ? _path.Text.Trim().Trim('"') : null; UpdateParts(); };
                var br = new Button { Text = "찾아보기(&R)...", Location = new Point(S(392), S(32)), Size = new Size(S(86), S(25)), UseVisualStyleBackColor = true };
                br.Click += (a, b) => Browse();
                _body.Controls.Add(_path); _body.Controls.Add(br);

                Text1("설치할 구성 요소:", S(76));
                _cMod = new CheckBox { Checked = true, Enabled = false, AutoSize = true, Location = new Point(S(36), S(100)) };
                _cUmm = new CheckBox { Enabled = false, AutoSize = true, Location = new Point(S(36), S(124)) };
                _body.Controls.Add(_cMod); _body.Controls.Add(_cUmm);
                _space = Text1("", S(168));
                UpdateParts();
                _next.Text = "설치(&I)";
            }
            else if (page == 2)
            {
                Head("설치 중", AppName + " 을(를) 설치하는 동안 기다려 주세요.");
                _progLabel = Text1("", S(12));
                _prog = new ProgressBar { Location = new Point(S(24), S(36)), Size = new Size(S(452), S(20)), Maximum = 100 };
                _log = new ListBox { Location = new Point(S(24), S(68)), Size = new Size(S(452), S(150)), IntegralHeight = false, SelectionMode = SelectionMode.None };
                _body.Controls.Add(_prog); _body.Controls.Add(_log);
                _back.Enabled = _next.Enabled = _cancel.Enabled = false;
            }
            else if (page == 3)
            {
                Head(_uninstallDone ? "제거 완료" : "설치 완료", _uninstallDone ? AppName + " 을(를) 제거했습니다." : AppName + " " + _ver + " 설치를 마쳤습니다.");
                if (_uninstallDone)
                    Text1(AppName + " 을(를) 컴퓨터에서 제거했습니다.\r\n(UnityModManager는 그대로 남아 있습니다)", S(16));
                else
                {
                    Text1(AppName + " 설치를 마쳤습니다.", S(16));
                    Text1("게임을 실행한 뒤 메인 섬에 생긴 \"멀티\" 포탈로 이동하세요.\r\n이후 업데이트는 게임을 켤 때 자동으로 받습니다 (재시작하면 적용).", S(44));
                    _cLaunch = new CheckBox { Text = "얼불춤 실행(&L)", Checked = true, AutoSize = true, Location = new Point(S(28), S(110)) };
                    _body.Controls.Add(_cLaunch);
                }
                _next.Text = "마침(&F)";
                _cancel.Enabled = false; _back.Enabled = false;
            }
        }

        void UpdateParts()
        {
            if (_cMod == null) return;
            var iv = InstalledVersion;
            _cMod.Text = AppName + " " + _ver + (iv == null ? "" : iv == _ver ? "  (다시 설치)" : "  (v" + iv + "에서 업데이트)");
            if (_game == null) { _cUmm.Text = "UnityModManager"; _cUmm.Checked = false; }
            else if (HasUmm) { _cUmm.Text = "UnityModManager  (이미 설치됨)"; _cUmm.Checked = false; }
            else { _cUmm.Text = "UnityModManager 0.32.4  (필요)"; _cUmm.Checked = true; }
            _space.Text = _game == null ? "얼불춤 폴더를 찾지 못했습니다. [찾아보기]로 선택해 주세요." : "필요한 공간: 약 4 MB";
            _space.ForeColor = _game == null ? Color.Firebrick : SystemColors.ControlText;
            _next.Enabled = _game != null;
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog { Description = "얼불춤 폴더를 선택하세요.", ShowNewFolderButton = false })
            {
                if (_game != null) d.SelectedPath = _game;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!IsGameDir(d.SelectedPath))
                {
                    MessageBox.Show(this, "선택한 폴더에 " + GameName + ".exe 가 없습니다.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _path.Text = d.SelectedPath;
            }
        }

        void Next()
        {
            if (_page == 0)
            {
                if (_rRemove != null && _rRemove.Checked) { Uninstall(); return; }
                Go(1);
            }
            else if (_page == 1)
            {
                if (!CheckGameClosed()) return;
                Go(2);
                Install();
            }
            else if (_page == 3)
            {
                if (_cLaunch != null && _cLaunch.Checked && !_uninstallDone)
                    try { Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId) { UseShellExecute = true }); } catch { }
                Close();
            }
        }

        bool CheckGameClosed()
        {
            while (Process.GetProcessesByName(GameName).Length > 0)
                if (MessageBox.Show(this, "얼불춤이 실행 중입니다.\r\n게임을 종료한 뒤 [다시 시도]를 누르세요.", Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                    return false;
            return true;
        }

        // ---------------- 설치 / 제거 ----------------
        void Log(string s, int pct)
        {
            _progLabel.Text = s; _log.Items.Add(s); _log.TopIndex = _log.Items.Count - 1;
            _prog.Value = pct;
            Application.DoEvents();
        }

        void Install()
        {
            try
            {
                if (!HasUmm)
                {
                    Log("UnityModManager 설치 중...", 10);
                    Directory.CreateDirectory(UmmDir);
                    foreach (var f in new[] { "UnityModManager.dll", "0Harmony.dll", "dnlib.dll", "UnityModManager.xml", "Config.xml" })
                    {
                        WriteRes("umm/" + f, Path.Combine(UmmDir, f));
                        Log("압축 해제: " + f, _prog.Value + 8);
                    }
                    // Doorstop: 게임 시작 때 UMM을 불러옴 (이미 다른 방식으로 UMM이 깔려 있으면 건드리지 않음)
                    WriteRes("umm/winhttp.dll", Path.Combine(_game, "winhttp.dll"));
                    Log("압축 해제: winhttp.dll", 56);
                    WriteRes("umm/doorstop_config.ini", Path.Combine(_game, "doorstop_config.ini"));
                    Log("압축 해제: doorstop_config.ini", 60);
                }
                Log(AppName + " 설치 중...", 65);
                Directory.CreateDirectory(ModDir);
                // 받은 맵 캐시·설정은 그대로 두고 모드 파일만 교체
                foreach (var f in new[] { ModId + ".dll", "Info.json", "layout.json" })
                {
                    WriteRes(f, Path.Combine(ModDir, f));
                    Log("압축 해제: " + f, _prog.Value + 10);
                }
                foreach (var old in Directory.GetFiles(ModDir, "*.old")) { try { File.Delete(old); } catch { } }
                Log("완료", 100);
                _uninstallDone = false;
                Go(3);
            }
            catch (UnauthorizedAccessException) { Failed("게임 폴더에 쓸 권한이 없습니다.", true); }
            catch (IOException ex) { Failed("파일을 쓰지 못했습니다. 얼불춤을 종료했는지 확인해 주세요.\r\n\r\n" + ex.Message, false); }
            catch (Exception ex) { Failed("설치하지 못했습니다.\r\n\r\n" + ex.Message, false); }
        }

        void Uninstall()
        {
            if (!CheckGameClosed()) return;
            if (MessageBox.Show(this, AppName + " 을(를) 제거하시겠습니까?\r\n받아둔 맵도 함께 삭제됩니다.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { Directory.Delete(ModDir, true); _uninstallDone = true; Go(3); }
            catch (UnauthorizedAccessException) { Failed("게임 폴더에 쓸 권한이 없습니다.", true); }
            catch (Exception ex) { Failed("제거하지 못했습니다.\r\n\r\n" + ex.Message, false); }
        }

        void Failed(string msg, bool admin)
        {
            if (admin)
            {
                if (MessageBox.Show(this, msg + "\r\n관리자 권한으로 다시 실행하시겠습니까?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }); Close(); return; } catch { }
                }
            }
            else MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Go(1);
        }

        // ---------------- 게임 폴더 탐색 ----------------
        static string FindGame()
        {
            var libs = new List<string>();
            foreach (var steam in SteamRoots())
            {
                libs.Add(steam);
                try
                {
                    var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }
            foreach (var d in new[] { "C", "D", "E", "F", "G" })
            {
                libs.Add(d + @":\Program Files (x86)\Steam");
                libs.Add(d + @":\Program Files\Steam");
                libs.Add(d + @":\SteamLibrary");
                libs.Add(d + @":\Steam");
            }
            foreach (var lib in libs)
            {
                try
                {
                    var p = Path.Combine(lib, "steamapps", "common", GameName);
                    if (IsGameDir(p)) return NiceCase(p);
                }
                catch { }
            }
            return null;
        }

        /// <summary>레지스트리 경로가 소문자로 오는 경우가 있어 실제 대소문자로 맞춤</summary>
        static string NiceCase(string p)
        {
            try
            {
                var di = new DirectoryInfo(p);
                if (di.Parent == null) return di.Name.ToUpperInvariant();
                return Path.Combine(NiceCase(di.Parent.FullName), di.Parent.GetDirectories(di.Name)[0].Name);
            }
            catch { return p; }
        }

        static IEnumerable<string> SteamRoots()
        {
            var list = new List<string>();
            void Try(RegistryKey root, string key, string val)
            {
                try { using (var k = root.OpenSubKey(key)) if (k?.GetValue(val) is string s && s.Length > 0) list.Add(s.Replace('/', '\\')); } catch { }
            }
            Try(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            Try(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            Try(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
            return list;
        }

        static bool IsGameDir(string p)
        {
            try { return !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, GameName + ".exe")); } catch { return false; }
        }

        // ---------------- 리소스 ----------------
        static void WriteRes(string name, string dest)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("설치 파일이 손상되었습니다: " + name);
                var tmp = dest + ".new";
                using (var f = File.Create(tmp)) s.CopyTo(f);
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
            }
        }

        static string EmbeddedVersion()
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
    }
}
