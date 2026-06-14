using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenNCL_Lancher.Runtime;

namespace OpenNCL_Lancher
{
    public sealed partial class MainWindow : Window
    {
        private readonly PythonLauncher _launcher = new();
        private bool _nativeKernel;
        private string? _nativeKernelErr;
        private DebugWindow? _debugWindow;
        private readonly List<string> _history = new();
        private readonly Stopwatch _sw = new();
        private int _historyIndex = -1, _cmdCount;
        private readonly DispatcherQueue _dispatch;
        private string _inputBuf = "";
        private bool _promptRendered;
        private string _mode = "normal";
        private string _promptStr = "openncl> ";
        private string _proPromptStr = "root@Command:~# ";
        private string _aiProvider = "openai";
        private string _aiModel = "gpt-4o";
        private string _aiApiUrl = "https://api.openai.com/v1/chat/completions";
        private string _aiApiKey = "";
        private Windows.UI.Color _normalAccent;
        private DispatcherTimer? _flowTimer;
        private readonly Random _rng = new();
        private readonly List<Microsoft.UI.Xaml.Shapes.Ellipse> _orbs = new();
        private readonly (double x, double y, double size, Windows.UI.Color color)[] _orbTargets = new (double, double, double, Windows.UI.Color)[4];
        private readonly (double x, double y, double size)[] _orbState = new (double, double, double)[4];
        private readonly double[] _orbSpeed = new double[4];
        private readonly RadialGradientBrush[] _orbBrushes = new RadialGradientBrush[4];

        private SolidColorBrush _fg   = new(Windows.UI.Color.FromArgb(255, 220, 220, 220));
        private SolidColorBrush _err  = new(Windows.UI.Color.FromArgb(255, 255, 70, 70));
        private SolidColorBrush _dim  = new(Windows.UI.Color.FromArgb(255, 100, 100, 110));
        private SolidColorBrush _accent = new(Windows.UI.Color.FromArgb(255, 0, 229, 160));
        private SolidColorBrush _path = new(Windows.UI.Color.FromArgb(255, 136, 136, 170));
        private SolidColorBrush _ok   = new(Windows.UI.Color.FromArgb(255, 0, 229, 160));
        private SolidColorBrush _warn = new(Windows.UI.Color.FromArgb(255, 255, 212, 59));

        private static readonly string[] BootLogoLines =
        {
            "          ██████╗ ██████╗ ███████╗███╗   ██╗███╗   ██╗ ██████╗██╗",
            "         ██╔═══██╗██╔══██╗██╔════╝████╗  ██║████╗  ██║██╔════╝██║",
            "         ██║   ██║██████╔╝█████╗  ██╔██╗ ██║██╔██╗ ██║██║     ██║",
            "         ██║   ██║██╔═══╝ ██╔══╝  ██║╚██╗██║██║╚██╗██║██║     ██║",
            "         ╚██████╔╝██║     ███████╗██║ ╚████║██║ ╚████║╚██████╗███████╗",
            "          ╚═════╝ ╚═╝     ╚══════╝╚═╝  ╚═══╝╚═╝  ╚═══╝ ╚═════╝╚══════╝",
            "                        Open New Command Line  v4.0",
        };

        private static readonly Dictionary<string, Windows.UI.Color> ColorNames = new()
        {
            ["black"]   = Windows.UI.Color.FromArgb(255, 0, 0, 0),
            ["red"]     = Windows.UI.Color.FromArgb(255, 255, 70, 70),
            ["green"]   = Windows.UI.Color.FromArgb(255, 0, 229, 160),
            ["blue"]    = Windows.UI.Color.FromArgb(255, 77, 171, 247),
            ["yellow"]  = Windows.UI.Color.FromArgb(255, 255, 212, 59),
            ["cyan"]    = Windows.UI.Color.FromArgb(255, 68, 212, 200),
            ["magenta"] = Windows.UI.Color.FromArgb(255, 204, 68, 255),
            ["white"]   = Windows.UI.Color.FromArgb(255, 220, 220, 220),
            ["gray"]    = Windows.UI.Color.FromArgb(255, 128, 128, 128),
            ["orange"]  = Windows.UI.Color.FromArgb(255, 255, 165, 0),
            ["purple"]  = Windows.UI.Color.FromArgb(255, 160, 120, 255),
        };

        private static readonly Windows.UI.Color[] RainbowPalette = {
            Windows.UI.Color.FromArgb(255, 68, 136, 255),
            Windows.UI.Color.FromArgb(255, 136, 68, 255),
            Windows.UI.Color.FromArgb(255, 68, 212, 200),
            Windows.UI.Color.FromArgb(255, 255, 68, 170),
            Windows.UI.Color.FromArgb(255, 255, 136, 68),
            Windows.UI.Color.FromArgb(255, 204, 68, 255),
        };

        private static readonly string[] KnownCmds = {
            "help","about","version","ver","date","time","dir","ls","pwd","sysinfo","system",
            "modules","clear","cls","exit","quit","echo","calc","calculate","cd",
            "google","bing","youtube","open","ip","encrypt","decrypt","install",
            "cmd","powershell","explorer","notepad","control","taskmgr","mspaint","regedit",
            "mode pro","x++","linux","wsl","sandbox","logo","bridge start","edit","translate",
            "calculator","screenshot","qrcode","kill","search","terminfo","diag","debug","debug status","log",
            "ai","ask","config ai","config ai model","config ai api","config ai key","config ai provider","config ai show"
        };

        // Command palette catalog: name + short description (Ctrl+Shift+P)
        private static readonly (string Name, string Desc)[] CommandCatalog =
        {
            ("help", "Show help"),
            ("about", "About OpenNCL"),
            ("version", "Kernel version"),
            ("date", "Current date/time"),
            ("dir", "List current directory"),
            ("pwd", "Print working directory"),
            ("cd ", "Change directory"),
            ("sysinfo", "System information"),
            ("ip", "Show IP address"),
            ("modules", "List loaded modules"),
            ("plugins", "List loaded plugins"),
            ("terminfo", "Terminal status"),
            ("diag", "Diagnostics"),
            ("clear", "Clear the terminal"),
            ("exit", "Exit / quit"),
            ("echo ", "Echo text"),
            ("calc ", "Calculator"),
            ("encrypt ", "Encrypt text"),
            ("decrypt ", "Decrypt text"),
            ("search ", "Everything file search"),
            ("kill ", "Kill a process"),
            ("install ", "Install a package"),
            ("google ", "Search Google"),
            ("bing ", "Search Bing"),
            ("youtube ", "Search YouTube"),
            ("open ", "Open URL"),
            ("color(fg,bg)", "Change terminal theme"),
            ("mode pro", "Enter professional mode"),
            ("x++", "x++ command"),
            ("logo", "Show ASCII logo"),
            ("sandbox ", "Sandbox command"),
            ("bridge start", "Start bridge"),
            ("edit ", "Edit a file"),
            ("translate ", "Translate text"),
            ("qrcode", "Generate QR code"),
            ("screenshot", "Take a screenshot"),
            ("calculator", "Open calculator"),
            ("linux", "WSL / Linux status"),
            ("cmd", "Open cmd"),
            ("powershell", "Open PowerShell"),
            ("explorer", "Open Explorer"),
            ("notepad", "Open Notepad"),
            ("taskmgr", "Open Task Manager"),
            ("regedit", "Open Registry Editor"),
            ("debug", "Open backend debug window"),
            ("ai", "Open AI sidebar"),
            ("ask ", "Ask the AI"),
            ("config ai show", "Show AI config"),
            ("config ai key ", "Set AI API key"),
            ("config ai model ", "Set AI model"),
            ("config prompt ", "Set custom prompt"),
            ("config about ", "Edit brand info"),
        };

        private List<(string Name, string Desc)> _paletteFiltered = new();

        public MainWindow()
        {
            InitializeComponent(); _dispatch = DispatcherQueue;
            _normalAccent = _accent.Color;
            LoadPrompts();
            LoadAiConfig();
            TryInitNativeKernel();
            BackendDebugHub.Info("client", "MainWindow initialized");
            _launcher.OutputReceived += OnOutputReceived;
            _launcher.ProcessExited += () => _dispatch.TryEnqueue(() => { AppendError("Kernel stopped."); StatusText.Text = "Kernel offline"; });
            Closed += (_, _) => { _flowTimer?.Stop(); _launcher.Dispose(); };
            AiPanel.CloseRequested += () => ToggleSidebar(false);
            AiPanel.ToolCallRequested += HandleToolCallAsync;
            AiPanel.RunInTerminalRequested += code => _dispatch.TryEnqueue(() => { _inputBuf = code; RefreshPrompt(_inputBuf); });
            AiPanel.TerminalContextRequested += GetContextJson;
            try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1050, 680)); } catch { }

            InitRainbow();
            Boot();
            RenderPrompt();
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void TryInitNativeKernel()
        {
            try
            {
                // OpenNclNative.dll is optional; when present it replaces stdin/stdout forwarding.
                EnsurePythonHomeForNative();
                _nativeKernel = Runtime.OpenNclNative.openncl_init(AppContext.BaseDirectory) != 0;
                if (!_nativeKernel)
                    _nativeKernelErr = Runtime.OpenNclNative.LastError();
                BackendDebugHub.Info("native", _nativeKernel ? "OpenNclNative.dll loaded" : "OpenNclNative.dll not loaded: " + (_nativeKernelErr ?? "unknown"));
            }
            catch (DllNotFoundException)
            {
                _nativeKernel = false;
                _nativeKernelErr = "OpenNclNative.dll not found";
                BackendDebugHub.Warn("native", _nativeKernelErr);
            }
            catch (BadImageFormatException)
            {
                _nativeKernel = false;
                _nativeKernelErr = "OpenNclNative.dll arch mismatch (x64/x86)";
                BackendDebugHub.Error("native", _nativeKernelErr);
            }
            catch (Exception ex)
            {
                _nativeKernel = false;
                _nativeKernelErr = ex.Message;
                BackendDebugHub.Error("native", "init exception: " + ex.Message);
            }
        }

        private static void EnsurePythonHomeForNative()
        {
            try
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENNCL_PYTHONHOME")))
                    return;

                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dirRaw in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var dir = dirRaw.Trim().Trim('"');
                    if (dir.Length == 0) continue;
                    var candidate = Path.Combine(dir, "python.exe");
                    if (File.Exists(candidate))
                    {
                        Environment.SetEnvironmentVariable("OPENNCL_PYTHONHOME", dir);
                        return;
                    }
                }
            }
            catch { }
        }

        private void Window_Activated(object _, WindowActivatedEventArgs __) { RootGrid.Focus(FocusState.Programmatic); }
        private void TerminalArea_PointerPressed(object _, PointerRoutedEventArgs __) { RootGrid.Focus(FocusState.Programmatic); }

        private void Window_SizeChanged(object _, WindowSizeChangedEventArgs __)
        {
            double w = GradientCanvas.ActualWidth > 0 ? GradientCanvas.ActualWidth : 1050;
            double h = GradientCanvas.ActualHeight > 0 ? GradientCanvas.ActualHeight : 680;
            for (int i = 0; i < _orbs.Count; i++)
                PickTarget(i, w, h);
        }

        private void InitRainbow()
        {
            for (int i = 0; i < 4; i++)
            {
                var orb = new Microsoft.UI.Xaml.Shapes.Ellipse();
                var brush = new RadialGradientBrush();
                brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 0, 0), Offset = 0.0 });
                brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, 0, 0, 0), Offset = 1.0 });
                orb.Fill = brush;
                _orbBrushes[i] = brush;
                GradientCanvas.Children.Add(orb);
                _orbs.Add(orb);
            }
            double w = 1050, h = 680;
            for (int i = 0; i < 4; i++)
            {
                PickTarget(i, w, h);
                _orbState[i] = (_orbTargets[i].x, _orbTargets[i].y, _orbTargets[i].size);
                ApplyOrb(i);
            }
            _flowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _flowTimer.Tick += OnFlowTick;
            _flowTimer.Start();
        }

        private void OnFlowTick(object? _, object __)
        {
            double w = GradientCanvas.ActualWidth > 0 ? GradientCanvas.ActualWidth : 1050;
            double h = GradientCanvas.ActualHeight > 0 ? GradientCanvas.ActualHeight : 680;
            for (int i = 0; i < _orbs.Count; i++)
            {
                var s = _orbState[i];
                var t = _orbTargets[i];
                double dx = t.x - s.x, dy = t.y - s.y, ds = t.size - s.size;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 3 && Math.Abs(ds) < 3)
                {
                    PickTarget(i, w, h);
                    t = _orbTargets[i];
                    dx = t.x - s.x; dy = t.y - s.y; ds = t.size - s.size;
                    dist = Math.Sqrt(dx * dx + dy * dy);
                }
                _orbState[i] = (s.x + dx * _orbSpeed[i], s.y + dy * _orbSpeed[i], s.size + ds * _orbSpeed[i]);
                ApplyOrb(i);
            }
        }

        private void PickTarget(int i, double w, double h)
        {
            _orbTargets[i] = (
                _rng.NextDouble() * (w + 200) - 200,
                _rng.NextDouble() * (h + 100) - 100,
                200 + _rng.NextDouble() * 500,
                RainbowPalette[_rng.Next(RainbowPalette.Length)]
            );
            _orbSpeed[i] = 0.004 + _rng.NextDouble() * 0.010;
        }

        private void ApplyOrb(int i)
        {
            var s = _orbState[i];
            var orb = _orbs[i];
            orb.Width = s.size;
            orb.Height = s.size;
            Canvas.SetLeft(orb, s.x);
            Canvas.SetTop(orb, s.y);
            var c = _orbTargets[i].color;
            var stops = _orbBrushes[i].GradientStops;
            stops[0].Color = c;
            stops[1].Color = Windows.UI.Color.FromArgb(0, c.R, c.G, c.B);
        }

        // ==================== INPUT ====================
        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+Shift+P -> command palette
            if (e.Key == Windows.System.VirtualKey.P && IsCtrlDown() && IsShiftDown())
            {
                OpenCommandPalette();
                e.Handled = true;
                return;
            }
            // Ctrl+V -> paste from clipboard
            if (e.Key == Windows.System.VirtualKey.V && IsCtrlDown())
            {
                _ = PasteFromClipboard();
                e.Handled = true;
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var cmd = _inputBuf.Trim();
                _inputBuf = "";
                if (cmd.Length == 0) return;
                _history.Add(cmd); _historyIndex = -1; _cmdCount++;
                CmdCountText.Text = $"{_cmdCount} commands";
                FlushPrompt(cmd);
                Execute(cmd);
                RenderPrompt();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back)
            {
                if (_inputBuf.Length > 0)
                {
                    _inputBuf = _inputBuf.Substring(0, _inputBuf.Length - 1);
                    RefreshPrompt(_inputBuf);
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                if (_history.Count > 0)
                {
                    _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Math.Max(0, _historyIndex - 1);
                    _inputBuf = _history[_historyIndex];
                    RefreshPrompt(_inputBuf);
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                if (_historyIndex >= 0)
                {
                    _historyIndex++;
                    _inputBuf = _historyIndex < _history.Count ? _history[_historyIndex] : "";
                    RefreshPrompt(_inputBuf);
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Tab)
            {
                ToggleSidebar();
                e.Handled = true;
            }
        }

        private void RootGrid_CharReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (args.Character < 0x20) return; // ignore control chars
            _inputBuf += args.Character;
            RefreshPrompt(_inputBuf);
        }

        private async Task PasteFromClipboard()
        {
            try
            {
                var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (!content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) return;
                var text = await content.GetTextAsync();
                if (string.IsNullOrEmpty(text)) return;
                // Terminal input is single-line: collapse newlines/tabs into spaces
                text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                _inputBuf += text;
                RefreshPrompt(_inputBuf);
            }
            catch { }
        }

        // ==================== PROMPT RENDER ====================
        private string Prompt() => _mode == "pro" ? _proPromptStr : _promptStr;

        private void RenderPrompt()
        {
            RefreshPrompt("");
        }

        private void RefreshPrompt(string input)
        {
            var blocks = TerminalOutput.Blocks;
            if (_promptRendered && blocks.Count > 0)
                blocks.RemoveAt(blocks.Count - 1);

            blocks.Add(MakePara(PathText() + Prompt() + input, _fg));
            _promptRendered = true;
            ScrollDown();
        }

        private void FlushPrompt(string cmd)
        {
            var blocks = TerminalOutput.Blocks;
            if (_promptRendered && blocks.Count > 0)
                blocks.RemoveAt(blocks.Count - 1);
            blocks.Add(MakePara(PathText() + Prompt() + cmd, _fg));
            _promptRendered = false;
        }

        private static Paragraph MakePara(string text, SolidColorBrush color)
        {
            var p = new Paragraph { Margin = new(0) };
            p.Inlines.Add(new Run { Text = text, Foreground = color });
            return p;
        }

        private static string PathText()
        {
            var dir = Environment.CurrentDirectory;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (dir.StartsWith(home, StringComparison.OrdinalIgnoreCase)) dir = "~" + dir.Substring(home.Length);
            return dir + " ";
        }

        // ==================== OUTPUT ====================
        private async void Boot()
        {
            // -------- Boot self-check page (standalone) --------
            TerminalOutput.Blocks.Clear();
            _promptRendered = false;
            _inputBuf = "";

            FillViewport();
            OutStatus(true, "UI init");
            OutStatus(true, "Input hook");
            OutStatus(_nativeKernel, "Native bridge", _nativeKernel ? "OpenNclNative.dll" : (_nativeKernelErr ?? "not loaded"));

            var es = FindEs();
            OutStatus(es != null, "Everything", es != null ? "es.exe found" : "es.exe not found");

            var cfg = CfgPath();
            OutStatus(File.Exists(cfg), "Config", File.Exists(cfg) ? "brand.json OK" : "brand.json missing");

            // Keep this page visible for 1 second.
            await Task.Delay(1000);

            // -------- Normal terminal (logo + prompt) --------
            TerminalOutput.Blocks.Clear();
            _promptRendered = false;
            _cmdCount = 0;
            CmdCountText.Text = "0 commands";

            PrintBootLogo();
            Out("  │  Kernel v4.0", _fg);
            Out("  │  Type \"help\" for commands.", _dim);
            Out("", _dim);

            await StartKernelAsync();

            // Reduce logo-to-prompt gap: do not pad the viewport here.
            RenderPrompt();
        }

        private void PrintBootLogo()
        {
            for (int i = 0; i < BootLogoLines.Length; i++)
                Out(BootLogoLines[i], i == BootLogoLines.Length - 1 ? _accent : _fg);
        }

        private void OutStatus(bool ok, string name, string? details = null)
        {
            var p = new Paragraph { Margin = new(0) };
            p.Inlines.Add(new Run { Text = ok ? "[OK] " : "[FAIL] ", Foreground = ok ? _ok : _err });
            p.Inlines.Add(new Run { Text = details == null ? name : $"{name} - {details}", Foreground = ok ? _fg : _warn });
            TerminalOutput.Blocks.Add(p);
            ScrollDown();
        }

        private async Task StartKernelAsync()
        {
            if (_nativeKernel)
            {
                _dispatch.TryEnqueue(() =>
                {
                    Out("  |  [OK] Native kernel bridge loaded (OpenNclNative.dll).", _fg);
                    StatusText.Text = "Online";
                    // Keep prompt at the bottom after async status line.
                    RenderPrompt();
                });
                BackendDebugHub.Info("kernel", "using native bridge");
                return;
            }

            BackendDebugHub.Info("kernel", "starting python process bridge");
            var online = await _launcher.StartAsync();
            _dispatch.TryEnqueue(() =>
            {
                if (online)
                {
                    Out("  |  [OK] Python kernel connected (handshake OK).", _fg);
                    StatusText.Text = "Online";
                    BackendDebugHub.Info("kernel", "python handshake OK");
                }
                else
                {
                    var err = _launcher.LastError ?? "python not found or script missing";
                    Out("  |  Python kernel unavailable: " + err, _dim);
                    StatusText.Text = "Kernel offline";
                    BackendDebugHub.Warn("kernel", "python offline: " + err);
                }

                // Ensure prompt remains visible after status output.
                RenderPrompt();
            });
        }

        private void FillViewport()
        {
            OutputScroller.UpdateLayout();
            double vh = OutputScroller.ViewportHeight;
            if (vh <= 0) vh = 640;
            int lineH = 20;
            int visible = (int)(vh / lineH);
            int current = TerminalOutput.Blocks.Count;
            int pad = Math.Max(0, visible - current - 2);
            for (int i = 0; i < pad; i++)
                Out(" ", _dim);
        }

        private void Out(string text, SolidColorBrush color)
        {
            TerminalOutput.Blocks.Add(MakePara(text, color));
        }

        private void AppendError(string t) => Out("[ERROR] " + t, _err);
        private void ScrollDown() { OutputScroller.UpdateLayout(); OutputScroller.ChangeView(null, double.MaxValue, null); }

        private void OnOutputReceived(string text)
        {
            BackendDebugHub.Trace("python<-", text.Replace("\r", "\\r").Replace("\n", "\\n"));
            _dispatch.TryEnqueue(() =>
            {
                foreach (var l in text.Split('\n'))
                {
                    if (string.IsNullOrEmpty(l)) continue;
                    Out(l, l.Contains("Error") || l.Contains("Crash") ? _err : _fg);
                }
            });
        }

        // ==================== EXECUTE ====================
        private void Execute(string cmd)
        {
            BackendDebugHub.Info("cmd", cmd);
            _sw.Restart(); var r = Exec(cmd); _sw.Stop();
            var ms = _sw.Elapsed.TotalMilliseconds;
            var perf = ms < 0.5 ? "" : $" [{ms:F1}ms]";

            if (r == null) { var hint = Suggest(cmd); if (hint != null) Out("  Did you mean \"" + hint + "\"?", _dim); TryForward(cmd); return; }
            if (r == "__SIDEBAR__") return;
            if (r == "__FORWARD__") { TryForward(cmd); return; }
            if (r == "__OK__") { if (perf.Length > 0) Out("  OK" + perf, _dim); return; }
            if (r == "__CLEAR__") { TerminalOutput.Blocks.Clear(); _cmdCount = 0; CmdCountText.Text = "0 commands"; FillViewport(); RenderPrompt(); return; }
            if (r == "__EXIT__") { HandleExit(); return; }
            foreach (var l in r.Split('\n')) Out(l, l.StartsWith("[ERROR]") ? _err : _fg);
            if (perf.Length > 0) Out(perf, _dim);
        }

        private void TryForward(string cmd)
        {
            if (_nativeKernel)
            {
                BackendDebugHub.Info("native->", cmd);
                var outText = Runtime.OpenNclNative.Exec(cmd);
                if (outText == null)
                {
                    BackendDebugHub.Error("native<-", Runtime.OpenNclNative.LastError() ?? "unknown");
                    Out("  [ERROR] Native bridge failed: " + (Runtime.OpenNclNative.LastError() ?? "unknown"), _err);
                    return;
                }
                BackendDebugHub.Trace("native<-", outText.Replace("\r", "\\r").Replace("\n", "\\n"));
                foreach (var l in outText.Split('\n'))
                    if (!string.IsNullOrEmpty(l))
                        Out(l, l.StartsWith("[ERROR]") ? _err : _fg);
                return;
            }

            if (_launcher.KernelReady) { BackendDebugHub.Info("python->", cmd); _launcher.SendCommand(cmd); return; }
            Out("  Python kernel offline — cannot forward: " + cmd, _dim);
            BackendDebugHub.Warn("python", "cannot forward (offline): " + cmd);
        }

        private void HandleExit()
        {
            if (_mode == "pro") { _mode = "normal"; _accent = new SolidColorBrush(_normalAccent); ModeIndicator.Text = "normal"; ModeIndicator.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 184, 184, 192)); Out("Exiting pro mode.", _dim); }
            else { Out("Session ended.", _dim); _launcher.Stop(); }
        }

        // ==================== COMMANDS ====================
        private string? Exec(string cmd)
        {
            var c = cmd.ToLower().Trim();
            // === Inline AI: # <natural language> ===
            if (c.StartsWith("#"))
            {
                var prompt = cmd[1..].Trim();
                if (string.IsNullOrWhiteSpace(prompt)) return "# Usage: # <natural language description of what you want to do>";
                if (string.IsNullOrWhiteSpace(_aiApiKey))
                    return "[ERROR] AI API key not set. Use: config ai key <key>";
                _ = GenerateCommandAsync(prompt);
                return "__OK__";
            }
            // ====================================
            if (c == "help") return Help();
            if (c == "about") return About();
            if (c == "debug") { OpenDebugWindow(); return "Opened backend debug window."; }
            if (c == "debug status") return DebugStatus();
            if (c == "log") return "Use \"debug\" to view backend comms. (Tip: \"debug status\")";
            if (c == "version" || c == "ver") return "OpenNCL Kernel v4.0";
            if (c == "date" || c == "time") return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (c == "pwd") return Environment.CurrentDirectory;
            if (c == "dir" || c == "ls") return DirText();
            if (c == "sysinfo" || c == "system") return Sys();
            if (c == "ip") return GetIp();
            if (c == "modules") return "math net fs encrypt qrcode translate bridge ai";
            if (c == "plugins") return "__FORWARD__";
            if (c == "clear" || c == "cls") return "__CLEAR__";
            if (c == "exit" || c == "quit") return "__EXIT__";
            if (c.StartsWith("cd ")) return ChangeDir(cmd);
            if (c.StartsWith("echo ")) return cmd[5..];
            if (c.StartsWith("calc ") || c.StartsWith("calculate ")) return Calc(cmd);
            if (c.StartsWith("install ")) return "Package: " + cmd[8..];
            if (Regex.IsMatch(c, @"^color\(\s*\w+\s*,\s*\w+\s*\)$")) return SetColor(cmd);
            if (c == "diag") return Diag();
            if (c == "terminfo") return TermInfo();
            if (c.StartsWith("google ")) return Search(cmd, "google");
            if (c.StartsWith("bing ")) return Search(cmd, "bing");
            if (c.StartsWith("youtube ")) return Search(cmd, "youtube");
            if (c.StartsWith("open ")) return OpenUrl(cmd);
            if (c.StartsWith("{search:google}:")) return Search(cmd, "google");
            if (c.StartsWith("{search:bing}:")) return Search(cmd, "bing");
            if (c.StartsWith("{search:youtube}:")) return Search(cmd, "youtube");
            if (c.StartsWith("{open:")) return OpenUrl(cmd);
            if (c.StartsWith("encrypt ")) return Enc(cmd[8..]);
            if (c.StartsWith("decrypt ")) return Dec(cmd[8..]);
            if (c.StartsWith("search ")) return Find(cmd);
            if (c.StartsWith("kill ")) return KillProc(cmd[5..].Trim());
            if (IsSys(c)) { LaunchSys(c); return "__OK__"; }
            if (c == "mode pro") { _mode = "pro"; _accent = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)); ModeIndicator.Text = "pro"; ModeIndicator.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 169, 77)); return "Entering Professional Mode.\nType \"exit\" to return."; }
            if (c == "x++") return "__FORWARD__";
            if (c == "logo") return string.Join("\n", BootLogoLines);
            if (c.StartsWith("logo ")) return "__FORWARD__";
            if (c.StartsWith("sandbox ")) return "__FORWARD__";
            if (c == "bridge start") return "__FORWARD__";
            if (c.StartsWith("edit about")) return EditAboutFile();
            if (c.StartsWith("config about show")) return ShowAboutCfg();
            if (c.StartsWith("config about ")) return EditAboutCfg(cmd[13..].Trim());
            if (c.StartsWith("config prompt ")) return SetPrompt(cmd[14..].Trim(), false);
            if (c.StartsWith("config pro_prompt ")) return SetPrompt(cmd[18..].Trim(), true);
            if (c.StartsWith("config ai show")) return ShowAiConfig();
            if (c.StartsWith("config ai model ")) return SetAiConfig("model", cmd[17..].Trim());
            if (c.StartsWith("config ai api ")) return SetAiConfig("api", cmd[14..].Trim());
            if (c.StartsWith("config ai key ")) return SetAiConfig("key", cmd[15..].Trim());
            if (c.StartsWith("config ai provider ")) return SetAiConfig("provider", cmd[20..].Trim());
            if (c == "ai" || c == "ask") { ToggleSidebar(true); return "__SIDEBAR__"; }
            if (c.StartsWith("ai ")) return ExecAi(cmd[3..].Trim());
            if (c.StartsWith("ask ")) return ExecAi(cmd[4..].Trim());
            if (c.StartsWith("edit ")) return "__FORWARD__";
            if (c.StartsWith("translate ")) return "__FORWARD__";
            if (c is "linux" or "wsl") return WslStatus();
            if (c == "qrcode") return "__FORWARD__";
            if (c.StartsWith("/")) return "Use OpenNCL commands. Type \"help\".";
            if (Regex.IsMatch(c, @"^https?://")) { OpenDirect(c); return "__OK__"; }
            return null;
        }

        static string Help() => string.Join("\n",
            "==================================================",
            "  OpenNCL v4.0  |  Help",
            "==================================================",
            "",
            "  Basics",
            "  - help                 Show this help",
            "  - about                About (editable)",
            "  - version | ver        Version",
            "  - date | time          Current date/time",
            "  - dir | ls             List files",
            "  - pwd                  Print working directory",
            "  - cd <path>            Change directory",
            "  - clear | cls          Clear screen",
            "  - exit | quit          Exit / leave pro mode",
            "",
            "  Tools",
            "  - calc <expr>          Calculate",
            "  - echo <text>          Echo",
            "  - encrypt <text>       Base64 encode",
            "  - decrypt <text>       Base64 decode",
            "  - color(fg,bg)         Change theme colors",
            "",
            "  Web",
            "  - google <q>           Search Google",
            "  - bing <q>             Search Bing",
            "  - youtube <q>          Search YouTube",
            "  - open <url|path>      Open URL/file/folder",
            "",
            "  System",
            "  - sysinfo | system     System information",
            "  - ip                   Public IP",
            "  - cmd | powershell     Launch shell",
            "  - explorer | notepad   Open system apps",
            "  - control | taskmgr    System tools",
            "  - mspaint | regedit    System tools",
            "",
            "  Search (Everything)",
            "  - search <name>        File search via es.exe",
            "",
            "  Advanced (needs backend)",
            "  - mode pro             Pro prompt",
            "  - x++                  Forward to kernel",
            "  - logo <...>           Forward to kernel",
            "  - sandbox <...>        Forward to kernel",
            "  - bridge start         Forward to kernel",
            "  - edit <file>          Forward to kernel",
            "  - translate <...>      Forward to kernel",
            "  - qrcode               Forward to kernel",
            "  - linux | wsl          WSL status",
            "",
            "  Debug",
            "  - terminfo             Terminal info",
            "  - diag                 Diagnostics (bridge status)",
            "  - debug                Open backend debug window",
            "  - debug status         Print current status line",
            "",
            "  AI (OpenAI-compatible API)",
            "  - ai <prompt>          Ask AI (GPT-4, Claude, etc.)",
            "  - ask <prompt>         Same as ai",
            "  - config ai show       Show AI configuration",
            "  - config ai model <m>  Set AI model",
            "  - config ai api <url>  Set API endpoint",
            "  - config ai key <k>    Set API key",
            "  - config ai provider <p> Set provider name",
            "",
            "  About editing",
            "  - edit about           Open config file",
            "  - config about show    Show current about config",
            "  - config about <k> <v> Set about field (title/author/platform/kernel/footer/github)",
            "=================================================="
        );
        static string About() { var c = LoadCfg(); return $"{c.title}\n==========================================\nAuthor   : {c.author}\nPlatform : {c.platform}\nKernel   : {c.kernel}\n==========================================\n  {c.footer}\n  {c.github}"; }

        static string DirText() { try { var l = new List<string> { Environment.CurrentDirectory }; foreach (var d in Directory.GetDirectories(Environment.CurrentDirectory)) l.Add("  [DIR]  " + Path.GetFileName(d)); foreach (var f in Directory.GetFiles(Environment.CurrentDirectory)) { var fi = new FileInfo(f); l.Add($"  {fi.Length,8:N0}  {fi.Name}"); } return string.Join("\n", l); } catch (Exception e) { return "[ERROR] " + e.Message; } }
        string? ChangeDir(string cmd)
        {
            var p = cmd[3..].Trim().Trim('"');
            if (string.IsNullOrEmpty(p)) return Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = Path.GetFullPath(p);
                if (_nativeKernel)
                {
                    // Keep python-side cwd in sync when using the native embedded kernel.
                    Runtime.OpenNclNative.Exec("cd " + Environment.CurrentDirectory);
                }
                else if (_launcher.KernelReady)
                {
                    _launcher.SendCommand("cd " + Environment.CurrentDirectory);
                }
                return Environment.CurrentDirectory;
            }
            catch (Exception e)
            {
                return "[ERROR] " + e.Message;
            }
        }
        static string Sys() => $"OS       : {Environment.OSVersion}\nMachine  : {Environment.MachineName}\nUser     : {Environment.UserName}\nCPU      : {Environment.ProcessorCount} cores ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})\nCLR      : {Environment.Version}\nDir      : {Environment.CurrentDirectory}";
        static string GetIp() { try { using var h = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) }; return "Public IP: " + h.GetStringAsync("https://api.ipify.org").Result.Trim(); } catch { return "[ERROR] Cannot reach api.ipify.org."; } }
        static string Calc(string raw) { var e = Regex.Replace(raw, @"^(calc|calculate)\s+", "", RegexOptions.IgnoreCase).Replace("\u00d7", "*").Replace("\u00f7", "/").Replace("^", "**"); try { return e + " = " + new System.Data.DataTable().Compute(e, null); } catch (Exception ex) { return "[ERROR] " + ex.Message; } }
        static bool IsSys(string c) => c is "python" or "node" or "cmd" or "powershell" or "explorer" or "notepad" or "control" or "taskmgr" or "mspaint" or "regedit" or "calculator" or "screenshot";
        static void LaunchSys(string c) { var m = new Dictionary<string, string> { ["python"] = "python", ["node"] = "node", ["cmd"] = "cmd", ["powershell"] = "powershell", ["explorer"] = "explorer", ["notepad"] = "notepad", ["control"] = "control", ["taskmgr"] = "taskmgr", ["mspaint"] = "mspaint", ["regedit"] = "regedit", ["calculator"] = "calc", ["screenshot"] = "SnippingTool" }; var name = m.TryGetValue(c, out var v) ? v : c; try { Process.Start(new ProcessStartInfo(name) { UseShellExecute = true }); } catch { } }
        static string Search(string raw, string eng) { var q = Regex.Replace(raw, @"^(\{search:(google|bing|youtube)\}:\s*|google\s+|bing\s+|youtube\s+)", "", RegexOptions.IgnoreCase); if (string.IsNullOrWhiteSpace(q)) return "[ERROR] Usage: " + eng + " <query>"; var u = new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q=", ["bing"] = "https://www.bing.com/search?q=", ["youtube"] = "https://www.youtube.com/results?search_query=" }; try { Process.Start(new ProcessStartInfo(u[eng] + Uri.EscapeDataString(q)) { UseShellExecute = true }); return "  Opened " + eng + ": " + q; } catch (Exception e) { return "[ERROR] " + e.Message; } }
        static string OpenUrl(string raw) { var url = Regex.Replace(raw, @"^(\{open:\s*|open\s+)", "", RegexOptions.IgnoreCase).Trim().TrimEnd('}'); if (string.IsNullOrWhiteSpace(url)) return "[ERROR] Usage: open <url>"; if (!Regex.IsMatch(url, @"^https?://")) url = "https://" + url; try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return "  Opened: " + url; } catch (Exception e) { return "[ERROR] " + e.Message; } }
        static void OpenDirect(string u) { try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { } }
        static string Enc(string t) => string.IsNullOrWhiteSpace(t) ? "[ERROR] Usage" : "Encrypted: " + Convert.ToBase64String(Encoding.UTF8.GetBytes(t));
        static string Dec(string t) { if (string.IsNullOrWhiteSpace(t)) return "[ERROR] Usage"; try { return "Decrypted: " + Encoding.UTF8.GetString(Convert.FromBase64String(t)); } catch { return "[ERROR] Invalid base64."; } }
        static string WslStatus() { try { var p = Process.Start(new ProcessStartInfo("wsl", "--status") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); if (p == null) return "[ERROR] WSL not available."; p.WaitForExit(3000); return "WSL Status:\n" + p.StandardOutput.ReadToEnd().Trim(); } catch { return "[ERROR] WSL not installed."; } }
        static string KillProc(string name) { if (string.IsNullOrWhiteSpace(name)) return "[ERROR] Usage: kill <process name>"; int k = 0; try { foreach (var p in Process.GetProcessesByName(name)) { try { p.Kill(); p.WaitForExit(2000); k++; } catch { } } return k > 0 ? $"Killed {k} process(es): {name}" : $"[ERROR] No process named \"{name}\"."; } catch (Exception e) { return "[ERROR] " + e.Message; } }

        static string EditAboutFile() { var p = CfgPath(); try { Directory.CreateDirectory(Path.GetDirectoryName(p)!); if (!File.Exists(p)) File.WriteAllText(p, "{}"); Process.Start(new ProcessStartInfo("notepad", "\"" + p + "\"") { UseShellExecute = true }); return "Opened: " + p; } catch (Exception e) { return "[ERROR] " + e.Message; } }
        static string ShowAboutCfg() { var c = LoadCfg(); return $"title    : {c.title}\nauthor   : {c.author}\nplatform : {c.platform}\nkernel   : {c.kernel}\nfooter   : {c.footer}\ngithub   : {c.github}\n\nUse \"edit about\" or \"config about <key> <value>\"."; }
        static string EditAboutCfg(string args) { if (string.IsNullOrEmpty(args)) return "Usage: config about <key> <value>"; var p = args.Split(' ', 2); if (p.Length < 2) return "[ERROR] Usage: config about <key> <value>"; var c = LoadCfg(); switch (p[0].ToLower()) { case "title": c.title = p[1]; break; case "author": c.author = p[1]; break; case "platform": c.platform = p[1]; break; case "kernel": c.kernel = p[1]; break; case "footer": c.footer = p[1]; break; case "github": c.github = p[1]; break; default: return "[ERROR] Unknown key: " + p[0]; } SaveCfg(c); return "Updated " + p[0] + " -> " + p[1]; }
        static string CfgPath() => Path.Combine(AppContext.BaseDirectory, "config", "brand.json");
        static (string title, string author, string platform, string kernel, string footer, string github) LoadCfg() { try { if (File.Exists(CfgPath())) { using var d = JsonDocument.Parse(File.ReadAllText(CfgPath())); var r = d.RootElement.TryGetProperty("about", out var ab) ? ab : d.RootElement; return (r.TryGetProperty("title", out var t) ? t.GetString()! : "OpenNCL v4.0", r.TryGetProperty("author", out var a) ? a.GetString()! : "Chen Tom 2016", r.TryGetProperty("platform", out var p) ? p.GetString()! : "Windows + Python + .NET WinUI 3", r.TryGetProperty("kernel", out var k) ? k.GetString()! : "CL-Kernel v4.0 (Hybrid)", r.TryGetProperty("footer", out var f) ? f.GetString()! : "\"Open source, open mind.\"", r.TryGetProperty("github", out var g) ? g.GetString()! : "github.com/chenTom2016"); } } catch { } return ("OpenNCL v4.0", "Chen Tom 2016", "Windows + Python + .NET WinUI 3", "CL-Kernel v4.0 (Hybrid)", "\"Open source, open mind.\"", "github.com/chenTom2016"); }
        static void SaveCfg((string title, string author, string platform, string kernel, string footer, string github) c) { try { Directory.CreateDirectory(Path.GetDirectoryName(CfgPath())!); var brand = "{\"name\":\"OpenNCL\",\"version\":\"v4.0\",\"prompt\":\"openncl> \",\"pro_prompt\":\"root@Command:~# \"}"; var ai = "{}"; if (File.Exists(CfgPath())) { using var d = JsonDocument.Parse(File.ReadAllText(CfgPath())); if (d.RootElement.TryGetProperty("brand", out var br)) brand = br.GetRawText(); if (d.RootElement.TryGetProperty("ai", out var a)) ai = a.GetRawText(); } var about = $"{{\"title\":\"{JsonEscape(c.title)}\",\"author\":\"{JsonEscape(c.author)}\",\"platform\":\"{JsonEscape(c.platform)}\",\"kernel\":\"{JsonEscape(c.kernel)}\",\"footer\":\"{JsonEscape(c.footer)}\",\"github\":\"{JsonEscape(c.github)}\"}}"; File.WriteAllText(CfgPath(), $"{{\"about\":{about},\"brand\":{brand},\"ai\":{ai}}}"); } catch { } }
        static string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        static string Find(string raw) { var q = Regex.Replace(raw, @"^(search\s+)", "", RegexOptions.IgnoreCase).Trim(); if (string.IsNullOrWhiteSpace(q)) return "[ERROR] Usage: search <file name>"; var exe = FindEs(); if (exe == null) return "[ERROR] Everything CLI (es.exe) not found."; try { var p = Process.Start(new ProcessStartInfo(exe, "-n 50 \"" + q.Replace("\"", "\"\"") + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }); if (p == null) return "[ERROR] Failed to start."; if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return "[ERROR] Timeout."; } var o = (p.StandardOutput.ReadToEnd() ?? "").Trim(); return string.IsNullOrWhiteSpace(o) ? "No results." : o; } catch (Exception e) { return "[ERROR] " + e.Message; } }
        static string? FindEs() { try { foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) { var c = Path.Combine(d.Trim().Trim('"'), "es.exe"); if (File.Exists(c)) return c; } foreach (var p in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }) { var c = Path.Combine(p, "Everything", "es.exe"); if (File.Exists(c)) return c; } } catch { } return null; }

        static string? Suggest(string input) { var lo = input.ToLower(); string? best = null; int bd = 99; foreach (var c in KnownCmds) { int d = Lev(lo, c); if (d < bd && d <= 2) { bd = d; best = c; if (d == 0) break; } } return best; }
        static int Lev(string a, string b) { if (a.Length == 0) return b.Length; if (b.Length == 0) return a.Length; var prev = new int[b.Length + 1]; var cur = new int[b.Length + 1]; for (int j = 0; j <= b.Length; j++) prev[j] = j; for (int i = 1; i <= a.Length; i++) { cur[0] = i; for (int j = 1; j <= b.Length; j++) cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)); var tmp = prev; prev = cur; cur = tmp; } return prev[b.Length]; }

        private string SetColor(string raw)
        {
            var m = Regex.Match(raw, @"color\((.*),(.*)\)");
            var fgName = m.Groups[1].Value.Trim().ToLower();
            var bgName = m.Groups[2].Value.Trim().ToLower();
            if (ColorNames.TryGetValue(fgName, out var fg))
            {
                _fg = new SolidColorBrush(fg);
                _accent = new SolidColorBrush(fg);
            }
            if (ColorNames.TryGetValue(bgName, out var bg))
            {
                _path = new SolidColorBrush(bg);
                _dim = new SolidColorBrush(Windows.UI.Color.FromArgb(255,
                    (byte)(bg.R / 2 + fg.R / 2),
                    (byte)(bg.G / 2 + fg.G / 2),
                    (byte)(bg.B / 2 + fg.B / 2)));
            }
            return $"Theme: fg={fgName} bg={bgName}\nType \"terminfo\" for current settings.";
        }

        private string Diag()
        {
            var c = LoadCfg();
            var lines = new List<string>
            {
                "  Diagnostics",
                "  ===========",
                "",
                "  C# Engine   : OK (43 built-in commands)",
                $"  Native DLL  : {(_nativeKernel ? "Loaded (OpenNclNative.dll)" : "Not loaded")}",
                $"  Native Err  : {_nativeKernelErr ?? "none"}",
                $"  Python      : {(_launcher.KernelReady ? "Connected (handshake OK)" : _launcher.IsRunning ? "Process running, handshake pending..." : "Not running")}",
                $"  Last Error  : {_launcher.LastError ?? "none"}",
                $"  Process     : {(_launcher.IsRunning ? "alive" : "dead")}",
                $"  Mode        : {_mode}",
                $"  CWD         : {Environment.CurrentDirectory}",
                $"  History     : {_history.Count} entries",
                $"  Commands    : {_cmdCount} executed",
                $"  Brand       : {c.title}",
                $"  AI Provider : {_aiProvider}",
                $"  AI Model    : {_aiModel}",
                $"  AI Key      : {(_aiApiKey.Length > 0 ? "configured" : "not set")}",
                "",
                "  Forwarded commands (need Python): x++, logo, sandbox, bridge, edit, translate, qrcode",
                "  Try: help | about | dir | sysinfo | date | pwd | terminfo | diag",
            };
            return string.Join("\n", lines);
        }

        private string TermInfo()
        {
            var c = LoadCfg();
            return $"  Terminal Info\n  ---------------\n  Mode     : {_mode}\n  Prompt   : {Prompt().Trim()}\n  ProPrompt: {_proPromptStr.Trim()}\n  CWD      : {Environment.CurrentDirectory}\n  History  : {_history.Count} entries\n  Kernel   : {(_launcher.KernelReady ? "online" : "offline")}\n  Brand    : {c.title}\n  Commands : {_cmdCount} executed";
        }

        private void OpenDebugWindow()
        {
            if (_debugWindow != null) return;
            var status = DebugStatus();
            _debugWindow = new DebugWindow(status);
            _debugWindow.Closed += (_, _) => _debugWindow = null;
            _debugWindow.Activate();
            BackendDebugHub.Info("debug", "debug window opened");
        }

        private string DebugStatus()
        {
            var bridge = _nativeKernel ? "native(OpenNclNative.dll)" : "python(process)";
            var native = _nativeKernel ? "Loaded" : $"Not loaded ({_nativeKernelErr ?? "unknown"})";
            var py = _launcher.KernelReady ? "handshake OK" : _launcher.IsRunning ? "running (handshake pending)" : "not running";
            return $"Bridge={bridge} | Native={native} | Python={py} | LastError={_launcher.LastError ?? "none"}";
        }

        private string SetPrompt(string value, bool isPro)
        {
            if (string.IsNullOrWhiteSpace(value)) return $"[ERROR] Usage: config {(isPro ? "pro_" : "")}prompt <text>";
            if (isPro) _proPromptStr = value + " ";
            else _promptStr = value + " ";
            SavePromptCfg();
            return $"{(isPro ? "Pro " : "")}Prompt updated: \"{value}\"";
        }

        private void LoadPrompts()
        {
            try
            {
                if (File.Exists(CfgPath()))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(CfgPath()));
                    if (d.RootElement.TryGetProperty("brand", out var brand))
                    {
                        if (brand.TryGetProperty("prompt", out var p)) _promptStr = p.GetString() ?? "openncl> ";
                        if (brand.TryGetProperty("pro_prompt", out var pp)) _proPromptStr = pp.GetString() ?? "root@Command:~# ";
                    }
                }
            }
            catch { }
        }

        private void SavePromptCfg()
        {
            try
            {
                var brand = $"{{\"name\":\"OpenNCL\",\"version\":\"v4.0\",\"prompt\":\"{JsonEscape(_promptStr.Trim())}\",\"pro_prompt\":\"{JsonEscape(_proPromptStr.Trim())}\"}}";
                string about = "{}", ai = "{}";
                if (File.Exists(CfgPath()))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(CfgPath()));
                    if (d.RootElement.TryGetProperty("about", out var ab)) about = ab.GetRawText();
                    if (d.RootElement.TryGetProperty("ai", out var a)) ai = a.GetRawText();
                }
                Directory.CreateDirectory(Path.GetDirectoryName(CfgPath())!);
                File.WriteAllText(CfgPath(), $"{{\"about\":{about},\"brand\":{brand},\"ai\":{ai}}}");
            }
            catch { }
        }

        // ==================== AI SIDEBAR ====================
        private void ToggleSidebar(bool? force = null)
        {
            var isOpen = SidebarColumn.Width.Value > 0;
            var shouldOpen = force ?? !isOpen;
            SidebarColumn.Width = new GridLength(shouldOpen ? 420 : 0);
            if (shouldOpen)
            {
                AiPanel.SetConfig(_aiApiUrl, _aiApiKey, _aiModel, _aiProvider);
                PushContextToSidebar();
                AiPanel.FocusPanel();
            }
            else
            {
                RootGrid.Focus(FocusState.Programmatic);
            }
        }

        // ==================== COMMAND PALETTE (Ctrl+Shift+P) ====================
        private static bool IsCtrlDown() =>
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        private static bool IsShiftDown() =>
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        private bool PaletteOpen => CommandPaletteOverlay.Visibility == Visibility.Visible;

        private void OpenCommandPalette()
        {
            CommandPaletteOverlay.Visibility = Visibility.Visible;
            PaletteSearch.Text = "";
            FilterPalette("");
            PaletteSearch.Focus(FocusState.Programmatic);
        }

        private void CloseCommandPalette()
        {
            CommandPaletteOverlay.Visibility = Visibility.Collapsed;
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void FilterPalette(string query)
        {
            query = query.Trim().ToLower();
            _paletteFiltered = string.IsNullOrEmpty(query)
                ? CommandCatalog.ToList()
                : CommandCatalog.Where(c => c.Name.ToLower().Contains(query) || c.Desc.ToLower().Contains(query)).ToList();

            PaletteList.Items.Clear();
            foreach (var (name, desc) in _paletteFiltered)
                PaletteList.Items.Add($"{name,-20}  {desc}");

            if (PaletteList.Items.Count > 0)
                PaletteList.SelectedIndex = 0;
        }

        private void ApplyPaletteSelection()
        {
            var idx = PaletteList.SelectedIndex;
            if (idx < 0 || idx >= _paletteFiltered.Count) return;
            _inputBuf = _paletteFiltered[idx].Name.TrimEnd();
            CloseCommandPalette();
            RefreshPrompt(_inputBuf);
        }

        private void PaletteSearch_TextChanged(object sender, TextChangedEventArgs e) => FilterPalette(PaletteSearch.Text);

        private void PaletteSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Escape:
                    CloseCommandPalette(); e.Handled = true; break;
                case Windows.System.VirtualKey.Enter:
                    ApplyPaletteSelection(); e.Handled = true; break;
                case Windows.System.VirtualKey.Down:
                    if (PaletteList.Items.Count > 0)
                        PaletteList.SelectedIndex = Math.Min(PaletteList.SelectedIndex + 1, PaletteList.Items.Count - 1);
                    e.Handled = true; break;
                case Windows.System.VirtualKey.Up:
                    if (PaletteList.Items.Count > 0)
                        PaletteList.SelectedIndex = Math.Max(PaletteList.SelectedIndex - 1, 0);
                    e.Handled = true; break;
            }
        }

        private void PaletteList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clicked = e.ClickedItem as string;
            var idx = PaletteList.Items.IndexOf(clicked);
            if (idx >= 0) PaletteList.SelectedIndex = idx;
            ApplyPaletteSelection();
        }

        private void CommandPaletteOverlay_Tapped(object sender, TappedRoutedEventArgs e) => CloseCommandPalette();

        private void CommandPalettePanel_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

        private string ExecAi(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_aiApiKey))
                return "[ERROR] AI API key not set.\nUse: config ai key <your-api-key>\n\nSupports any OpenAI-compatible API:\n  OpenAI, Anthropic (via proxy), Ollama, vLLM, local LLMs, etc.";

            ToggleSidebar(true);
            AiPanel.SendPrompt(prompt);
            return "__SIDEBAR__";
        }

        private string ShowAiConfig()
        {
            var masked = _aiApiKey.Length > 8 ? _aiApiKey[..4] + "****" + _aiApiKey[^4..] : (_aiApiKey.Length > 0 ? "****" : "(not set)");
            return $"AI Configuration\n  Provider : {_aiProvider}\n  Model    : {_aiModel}\n  API URL  : {_aiApiUrl}\n  API Key  : {masked}";
        }

        private string SetAiConfig(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return $"[ERROR] Usage: config ai {key} <value>";
            switch (key)
            {
                case "model": _aiModel = value; break;
                case "api": _aiApiUrl = value; break;
                case "key": _aiApiKey = value; break;
                case "provider": _aiProvider = value; break;
                default: return $"[ERROR] Unknown AI config key: {key}";
            }
            SaveAiConfig();
            return $"AI {key} updated.";
        }

        private void LoadAiConfig()
        {
            try
            {
                if (File.Exists(CfgPath()))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(CfgPath()));
                    if (d.RootElement.TryGetProperty("ai", out var ai))
                    {
                        if (ai.TryGetProperty("provider", out var p)) _aiProvider = p.GetString()!;
                        if (ai.TryGetProperty("model", out var m)) _aiModel = m.GetString()!;
                        if (ai.TryGetProperty("api_url", out var u)) _aiApiUrl = u.GetString()!;
                        if (ai.TryGetProperty("api_key", out var k)) _aiApiKey = k.GetString()!;
                    }
                }
            }
            catch { }
        }

        private void SaveAiConfig()
        {
            try
            {
                string about = "{}", brand = "{}";
                if (File.Exists(CfgPath()))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(CfgPath()));
                    if (d.RootElement.TryGetProperty("about", out var ab)) about = ab.GetRawText();
                    if (d.RootElement.TryGetProperty("brand", out var br)) brand = br.GetRawText();
                }
                var ai = $"{{\"provider\":\"{JsonEscape(_aiProvider)}\",\"model\":\"{JsonEscape(_aiModel)}\",\"api_url\":\"{JsonEscape(_aiApiUrl)}\",\"api_key\":\"{JsonEscape(_aiApiKey)}\"}}";
                Directory.CreateDirectory(Path.GetDirectoryName(CfgPath())!);
                File.WriteAllText(CfgPath(), $"{{\"about\":{about},\"brand\":{brand},\"ai\":{ai}}}");
            }
            catch { }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

        // ==================== INLINE AI (# prefix) ====================
        private async Task GenerateCommandAsync(string prompt)
        {
            try
            {
                var systemMsg = new { role = "system", content = "You are a command-line assistant inside OpenNCL terminal (Windows). Convert the user's natural language request into a SINGLE terminal command. Reply with ONLY the command, no explanation, no markdown, no backticks. Just the raw command." };
                var userMsg = new { role = "user", content = prompt };
                var body = JsonSerializer.Serialize(new
                {
                    model = _aiModel,
                    messages = new[] { systemMsg, userMsg },
                    max_tokens = 200,
                    temperature = 0.1
                });

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var req = new HttpRequestMessage(HttpMethod.Post, _aiApiUrl)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("Authorization", "Bearer " + _aiApiKey);

                var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var errBody = await resp.Content.ReadAsStringAsync();
                    _dispatch.TryEnqueue(() =>
                    {
                        Out("  [AI] API error " + (int)resp.StatusCode, _err);
                        RenderPrompt();
                    });
                    return;
                }

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var content = doc.RootElement.TryGetProperty("choices", out var choices) &&
                              choices[0].TryGetProperty("message", out var msg) &&
                              msg.TryGetProperty("content", out var ct)
                              ? ct.GetString()?.Trim()
                              : null;

                if (string.IsNullOrWhiteSpace(content))
                {
                    _dispatch.TryEnqueue(() => { Out("  [AI] No command generated.", _dim); RenderPrompt(); });
                    return;
                }

                // Strip markdown backticks if the model ignored instructions
                content = System.Text.RegularExpressions.Regex.Replace(content, @"^```\w*\n?|```$", "").Trim();
                if (content.StartsWith("`") && content.EndsWith("`"))
                    content = content[1..^1].Trim();

                _dispatch.TryEnqueue(() =>
                {
                    Out("  AI: " + content, _accent);
                    _inputBuf = content;
                    RefreshPrompt(_inputBuf);
                });
            }
            catch (Exception ex)
            {
                _dispatch.TryEnqueue(() => { Out("  [AI] " + ex.Message, _err); RenderPrompt(); });
            }
        }

        // ==================== TOOL EXECUTOR (Agent Bridge) ====================
        private async Task<string?> HandleToolCallAsync(string toolName, string argsJson)
        {
            BackendDebugHub.Info("tool", $"{toolName} {argsJson}");
            try
            {
                using var args = JsonDocument.Parse(argsJson);
                var r = args.RootElement;

                return toolName switch
                {
                    "exec" => await ExecTool(r),
                    "read_file" => ReadFileTool(r),
                    "write_file" => WriteFileTool(r),
                    "list_dir" => ListDirTool(r),
                    "search_files" => SearchFilesTool(r),
                    "get_context" => GetContextJson(),
                    _ => "[ERROR] Unknown tool: " + toolName
                };
            }
            catch (Exception ex)
            {
                BackendDebugHub.Error("tool", ex.Message);
                return "[ERROR] " + ex.Message;
            }
        }

        private static async Task<string> ExecTool(JsonElement args)
        {
            var cmd = args.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(cmd)) return "[ERROR] No command provided";

            try
            {
                var psi = new ProcessStartInfo("cmd", "/c " + cmd)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                // Inherit current directory
                psi.WorkingDirectory = Environment.CurrentDirectory;

                using var p = Process.Start(psi);
                if (p == null) return "[ERROR] Failed to start process";

                var stdout = await p.StandardOutput.ReadToEndAsync();
                var stderr = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(stdout)) sb.Append(stdout.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append("[STDERR] ").Append(stderr.TrimEnd());
                }
                if (sb.Length == 0) sb.Append("(no output, exit code: ").Append(p.ExitCode).Append(')');
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "[ERROR] " + ex.Message;
            }
        }

        private static string ReadFileTool(JsonElement args)
        {
            var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(path)) return "[ERROR] No path provided";

            try
            {
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Environment.CurrentDirectory, path);
                path = Path.GetFullPath(path);

                if (!File.Exists(path)) return "[ERROR] File not found: " + path;

                var content = File.ReadAllText(path, Encoding.UTF8);
                if (content.Length > 10000)
                    content = content[..10000] + $"\n\n... (truncated, total {content.Length} chars)";
                return content;
            }
            catch (Exception ex)
            {
                return "[ERROR] " + ex.Message;
            }
        }

        private static string WriteFileTool(JsonElement args)
        {
            var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            var content = args.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(path)) return "[ERROR] No path provided";

            try
            {
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Environment.CurrentDirectory, path);
                path = Path.GetFullPath(path);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content, Encoding.UTF8);
                return "File written: " + path + " (" + content.Length + " chars)";
            }
            catch (Exception ex)
            {
                return "[ERROR] " + ex.Message;
            }
        }

        private static string ListDirTool(JsonElement args)
        {
            var path = args.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(path)) path = Environment.CurrentDirectory;

            try
            {
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Environment.CurrentDirectory, path);
                path = Path.GetFullPath(path);

                if (!Directory.Exists(path)) return "[ERROR] Directory not found: " + path;

                var sb = new StringBuilder();
                sb.AppendLine(path);
                sb.AppendLine(new string('-', Math.Min(path.Length, 60)));

                foreach (var d in Directory.GetDirectories(path))
                    sb.AppendLine("  [DIR]  " + Path.GetFileName(d));
                foreach (var f in Directory.GetFiles(path))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        sb.AppendLine($"  {fi.Length,10:N0}  {fi.Name}");
                    }
                    catch { sb.AppendLine($"  {"???",10}  {Path.GetFileName(f)}"); }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "[ERROR] " + ex.Message;
            }
        }

        private static string SearchFilesTool(JsonElement args)
        {
            var pattern = args.TryGetProperty("pattern", out var p) ? p.GetString() ?? "" : "";
            var directory = args.TryGetProperty("directory", out var d) ? d.GetString() : null;
            if (string.IsNullOrWhiteSpace(pattern)) return "[ERROR] No pattern provided";
            if (string.IsNullOrWhiteSpace(directory)) directory = Environment.CurrentDirectory;

            try
            {
                if (!Path.IsPathRooted(directory))
                    directory = Path.Combine(Environment.CurrentDirectory, directory);
                directory = Path.GetFullPath(directory);

                if (!Directory.Exists(directory)) return "[ERROR] Directory not found: " + directory;

                // Try Everything ES first for speed
                var esExe = FindEs();
                if (esExe != null)
                {
                    var psi = new ProcessStartInfo(esExe, "-n 50 \"" + pattern.Replace("\"", "\"\"") + "\" " + directory)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(5000);
                        var output = (proc.StandardOutput.ReadToEnd() ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(output)) return output;
                    }
                }

                // Fallback: .NET Directory.EnumerateFiles
                var results = Directory.EnumerateFiles(directory, pattern, new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 5,
                    IgnoreInaccessible = true
                }).Take(50).ToList();

                if (results.Count == 0) return "No files found matching: " + pattern;
                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return "[ERROR] " + ex.Message;
            }
        }

        // ==================== CONTEXT PROVIDER ====================
        private string GetContextJson()
        {
            var recent = _history.Count > 0
                ? string.Join(" | ", _history.Skip(Math.Max(0, _history.Count - 5)))
                : "(none)";
            var ctx = new
            {
                cwd = Environment.CurrentDirectory,
                os = "Windows " + Environment.OSVersion.VersionString,
                mode = _mode,
                recentCommands = recent,
                kernelType = _nativeKernel ? "native" : "python",
                pythonOnline = _launcher.KernelReady
            };
            return JsonSerializer.Serialize(ctx);
        }

        private void PushContextToSidebar()
        {
            var recent = _history.Count > 0
                ? string.Join(" | ", _history.Skip(Math.Max(0, _history.Count - 5)))
                : "(none)";
            AiPanel.SendContext(
                Environment.CurrentDirectory,
                "Windows " + Environment.OSVersion.VersionString,
                _mode,
                recent
            );
        }

        // ==================== AI SIDEBAR END ====================
    }
}
