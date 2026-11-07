using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ADOFAIMultiInstaller
{
    /// <summary>
    /// 창으로 된 설치기 (exe 하나).
    /// 안에 든 것: 모드(dll, Info.json, layout.json) + UnityModManager(Doorstop 방식) 파일.
    /// 게임 폴더 자동 탐색 → UMM 없으면 같이 설치 → 모드 설치/업데이트/제거.
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
            Application.Run(new MainForm());
        }
    }

    class MainForm : Form
    {
        const string ModId = "ADOFAIMulti";
        const string GameName = "A Dance of Fire and Ice";
        const string SteamAppId = "977950";

        // 불과 얼음 테마
        static readonly Color C_bg = Hex("0b0d18"), C_card = Hex("171b33"), C_field = Hex("10132a"), C_line = Hex("2a2f52"),
            C_txt = Hex("eef0ff"), C_sub = Hex("9096b8"), C_acc = Hex("ff7a45"), C_accHover = Hex("ff9466"), C_accText = Hex("1a0d06"),
            C_btn = Hex("252a4a"), C_btnHover = Hex("30365c"), C_ok = Hex("5fd18b"), C_warn = Hex("ffd166"), C_err = Hex("ff6b6b"), C_ice = Hex("5aa9ff");

        static Color Hex(string h) => Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));

        readonly string _ver = EmbeddedVersion();
        string _game;
        bool _done;

        readonly float _s;   // DPI 배율
        readonly Font _fTitle, _fSub, _fBody, _fBold, _fBtn;
        TextBox _path;
        Label _gameState, _ummState, _modState, _status;
        FlatBtn _main, _browse, _remove;
        Bar _bar;

        int S(float v) => (int)Math.Round(v * _s);

        public MainForm()
        {
            using (var g = CreateGraphics()) _s = g.DpiX / 96f;
            string ff = "Malgun Gothic";
            _fTitle = new Font(ff, 20f, FontStyle.Bold);
            _fSub = new Font(ff, 9.5f);
            _fBody = new Font(ff, 10f);
            _fBold = new Font(ff, 10f, FontStyle.Bold);
            _fBtn = new Font(ff, 12.5f, FontStyle.Bold);

            Text = "ADOFAI Multi 설치";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = C_bg; ForeColor = C_txt;
            ClientSize = new Size(S(560), S(430));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Add(new Label { Text = "ADOFAI Multi", Font = _fTitle, ForeColor = C_txt, AutoSize = true, Location = new Point(S(28), S(22)) });
            Add(new Label { Text = "얼불춤 멀티 모드 설치기  ·  v" + _ver, Font = _fSub, ForeColor = C_sub, AutoSize = true, Location = new Point(S(31), S(64)) });

            // 게임 폴더
            var card = new Card { Location = new Point(S(24), S(100)), Size = new Size(S(512), S(196)), BackColor = C_card, Radius = S(12), Border = C_line };
            Controls.Add(card);
            card.Controls.Add(new Label { Text = "게임 폴더", Font = _fBold, ForeColor = C_sub, AutoSize = true, Location = new Point(S(18), S(16)), BackColor = C_card });
            _path = new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = C_field, ForeColor = C_txt, Font = _fBody, Location = new Point(S(28), S(51)), Width = S(356), TabStop = false };
            var pathBox = new Card { Location = new Point(S(18), S(42)), Size = new Size(S(376), S(36)), BackColor = C_field, Radius = S(8), Border = C_line };
            card.Controls.Add(_path); card.Controls.Add(pathBox); _path.BringToFront();
            _browse = new FlatBtn("찾아보기", C_btn, C_btnHover, C_txt, _fBold) { Location = new Point(S(402), S(42)), Size = new Size(S(92), S(36)), Radius = S(8) };
            _browse.Click += (a, b) => Browse();
            card.Controls.Add(_browse);

            _gameState = Row(card, "얼불춤", S(96));
            _ummState = Row(card, "UnityModManager", S(126));
            _modState = Row(card, "멀티 모드", S(156));

            // 진행 표시
            _bar = new Bar { Location = new Point(S(24), S(314)), Size = new Size(S(512), S(6)), Back = C_card, Fore = C_acc };
            Controls.Add(_bar);
            _status = new Label { Text = "", Font = _fSub, ForeColor = C_sub, AutoSize = false, Location = new Point(S(24), S(326)), Size = new Size(S(512), S(22)), TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_status);

            // 버튼
            _remove = new FlatBtn("제거", C_btn, C_btnHover, C_txt, _fBold) { Location = new Point(S(24), S(360)), Size = new Size(S(110), S(48)), Radius = S(10) };
            _remove.Click += (a, b) => Uninstall();
            Controls.Add(_remove);
            _main = new FlatBtn("설치", C_acc, C_accHover, C_accText, _fBtn) { Location = new Point(S(146), S(360)), Size = new Size(S(390), S(48)), Radius = S(10) };
            _main.Click += (a, b) => MainClick();
            Controls.Add(_main);
            AcceptButton = null;

            _game = FindGame();
            Refresh2();
        }

        void Add(Control c) { c.BackColor = C_bg; Controls.Add(c); }

        Label Row(Control parent, string name, int y)
        {
            parent.Controls.Add(new Label { Text = name, Font = _fBody, ForeColor = C_sub, AutoSize = true, Location = new Point(S(18), y), BackColor = C_card });
            var v = new Label { Text = "", Font = _fBold, ForeColor = C_txt, AutoSize = false, Location = new Point(S(180), y), Size = new Size(S(314), S(24)), TextAlign = ContentAlignment.TopRight, BackColor = C_card };
            parent.Controls.Add(v);
            return v;
        }

        // ---------------- 상태 ----------------
        string Managed => Path.Combine(_game, GameName + "_Data", "Managed");
        string UmmDir => Path.Combine(Managed, "UnityModManager");
        string ModDir => Path.Combine(_game, "Mods", ModId);
        bool HasUmm => _game != null && File.Exists(Path.Combine(UmmDir, "UnityModManager.dll"));
        string InstalledVersion
        {
            get
            {
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

        void Set(Label l, string text, Color c) { l.Text = text; l.ForeColor = c; }

        void Refresh2()
        {
            _path.Text = _game ?? "자동으로 못 찾음 · 찾아보기로 선택";
            _path.ForeColor = _game == null ? C_warn : C_txt;
            if (_done) return;

            if (_game == null)
            {
                Set(_gameState, "못 찾음", C_err); Set(_ummState, "-", C_sub); Set(_modState, "-", C_sub);
                _main.Text = "설치"; _main.Enabled = false; _remove.Visible = false;
                return;
            }
            Set(_gameState, "확인", C_ok);
            Set(_ummState, HasUmm ? "설치됨" : "함께 설치", HasUmm ? C_ok : C_ice);
            var iv = InstalledVersion;
            if (iv == null) Set(_modState, "새로 설치  ·  v" + _ver, C_ice);
            else if (iv == _ver) Set(_modState, "설치됨  ·  v" + iv, C_ok);
            else Set(_modState, "v" + iv + "  →  v" + _ver, C_ice);
            _main.Text = iv == null ? "설치" : iv == _ver ? "다시 설치" : "업데이트";
            _main.Enabled = true;
            _remove.Visible = iv != null;
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog { Description = "얼불춤 폴더 선택 (" + GameName + ".exe 가 있는 폴더)", ShowNewFolderButton = false })
            {
                if (_game != null) d.SelectedPath = _game;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!IsGameDir(d.SelectedPath)) { Status("그 폴더에 " + GameName + ".exe 없음", C_err); return; }
                _game = d.SelectedPath; _done = false; Status("", C_sub); _bar.Value = 0;
                Refresh2();
            }
        }

        void Status(string s, Color c) { _status.Text = s; _status.ForeColor = c; _status.Refresh(); }
        void Step(float v, string s) { _bar.Value = v; Status(s, C_sub); Application.DoEvents(); }

        bool GameRunning() => Process.GetProcessesByName(GameName).Length > 0;

        // ---------------- 설치 ----------------
        void MainClick()
        {
            if (_done) { LaunchGame(); return; }
            if (GameRunning()) { Status("얼불춤을 끄고 다시 눌러주세요", C_err); return; }
            _main.Enabled = _remove.Enabled = _browse.Enabled = false;
            try
            {
                bool umm = !HasUmm;
                if (umm)
                {
                    Step(0.15f, "UnityModManager 설치 중...");
                    Directory.CreateDirectory(UmmDir);
                    foreach (var f in new[] { "UnityModManager.dll", "0Harmony.dll", "dnlib.dll", "UnityModManager.xml", "Config.xml" })
                        WriteRes("umm/" + f, Path.Combine(UmmDir, f));
                    // Doorstop: 게임 시작 때 UMM을 불러옴 (이미 다른 방식으로 UMM이 깔려 있으면 건드리지 않음)
                    WriteRes("umm/winhttp.dll", Path.Combine(_game, "winhttp.dll"));
                    WriteRes("umm/doorstop_config.ini", Path.Combine(_game, "doorstop_config.ini"));
                }
                Step(0.55f, "멀티 모드 설치 중...");
                Directory.CreateDirectory(ModDir);
                // 받은 맵 캐시·설정은 그대로 두고 모드 파일만 교체
                WriteRes(ModId + ".dll", Path.Combine(ModDir, ModId + ".dll"));
                WriteRes("Info.json", Path.Combine(ModDir, "Info.json"));
                WriteRes("layout.json", Path.Combine(ModDir, "layout.json"));
                foreach (var old in Directory.GetFiles(ModDir, "*.old")) { try { File.Delete(old); } catch { } }
                Step(1f, "");

                _done = true;
                Set(_ummState, umm ? "설치 완료" : "설치됨", C_ok);
                Set(_modState, "설치 완료  ·  v" + _ver, C_ok);
                Status("설치 완료 · 게임에서 메인 섬의 \"멀티\" 포탈로 이동", C_ok);
                _main.Text = "게임 실행"; _main.Enabled = true;
                _remove.Visible = false;
            }
            catch (UnauthorizedAccessException) { Fail("권한 없음 · 관리자 권한으로 다시 실행", true); }
            catch (IOException ex) { Fail("파일 쓰기 실패 · 게임을 끄고 다시 시도 (" + ex.Message + ")", false); }
            catch (Exception ex) { Fail("설치 실패 · " + ex.Message, false); }
            finally { _browse.Enabled = true; _remove.Enabled = true; if (!_done) _main.Enabled = true; }
        }

        void Uninstall()
        {
            if (GameRunning()) { Status("얼불춤을 끄고 다시 눌러주세요", C_err); return; }
            if (MessageBox.Show(this, "멀티 모드를 제거할까요?\n받아둔 맵도 함께 삭제됩니다.", "제거", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                Directory.Delete(ModDir, true);
                _bar.Value = 0;
                Status("제거 완료", C_ok);
                Refresh2();
            }
            catch (UnauthorizedAccessException) { Fail("권한 없음 · 관리자 권한으로 다시 실행", true); }
            catch (Exception ex) { Fail("제거 실패 · " + ex.Message, false); }
        }

        void Fail(string msg, bool offerAdmin)
        {
            _bar.Value = 0;
            Status(msg, C_err);
            if (!offerAdmin) return;
            if (MessageBox.Show(this, "게임 폴더에 쓸 권한이 없습니다.\n관리자 권한으로 다시 실행할까요?", "권한 필요", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }); Close(); } catch { }
        }

        void LaunchGame()
        {
            try { Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId) { UseShellExecute = true }); Close(); }
            catch { Status("Steam에서 얼불춤 실행", C_sub); }
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
                    if (IsGameDir(p)) return p;
                }
                catch { }
            }
            return null;
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

        static bool IsGameDir(string p) => !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, GameName + ".exe"));

        // ---------------- 리소스 ----------------
        static void WriteRes(string name, string dest)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("설치 파일 손상: " + name);
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

    // ---------------- 그리기 ----------------
    static class Draw
    {
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, rad * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>둥근 카드 (부모 배경 위에 그림)</summary>
    class Card : Panel
    {
        public int Radius = 10; public Color Border = Color.Empty;
        public Card() { DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Draw.Round(r, Radius))
            using (var b = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(b, p);
                if (Border != Color.Empty) using (var pen = new Pen(Border)) e.Graphics.DrawPath(pen, p);
            }
        }
    }

    /// <summary>둥근 평면 버튼</summary>
    class FlatBtn : Control
    {
        public int Radius = 10;
        readonly Color _bg, _hover, _fg;
        bool _over;
        public FlatBtn(string text, Color bg, Color hover, Color fg, Font f)
        {
            Text = text; _bg = bg; _hover = hover; _fg = fg; Font = f;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { _over = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _over = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var c = !Enabled ? Color.FromArgb(110, _bg) : _over ? _hover : _bg;
            using (var p = Draw.Round(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? _fg : Color.FromArgb(140, _fg),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>진행 막대</summary>
    class Bar : Control
    {
        public Color Back, Fore;
        float _v;
        public float Value { get => _v; set { _v = Math.Max(0, Math.Min(1, value)); Invalidate(); Update(); } }
        public Bar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rad = Height / 2;
            using (var p = Draw.Round(new Rectangle(0, 0, Width - 1, Height - 1), rad)) using (var b = new SolidBrush(Back)) g.FillPath(b, p);
            int w = (int)((Width - 1) * _v);
            if (w > Height) using (var p = Draw.Round(new Rectangle(0, 0, w, Height - 1), rad)) using (var b = new SolidBrush(Fore)) g.FillPath(b, p);
        }
    }
}
