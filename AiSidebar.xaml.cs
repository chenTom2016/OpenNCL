using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using OpenNCL_Lancher.Runtime;

namespace OpenNCL_Lancher
{
    public sealed partial class AiSidebar : UserControl
    {
        // config
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _model = "gpt-4o";

        // context
        private string _cwd = "";
        private string _osVer = "Windows";
        private string _mode = "normal";
        private string _recentCmds = "";

        // state
        private readonly List<AiMessage> _messages = new();
        private AgentLoop? _agent;
        private bool _streaming;

        // events
        public event Action? CloseRequested;
        public event Func<string, string, Task<string?>>? ToolCallRequested;
        public event Action<string>? RunInTerminalRequested;
        public event Func<string>? TerminalContextRequested;

        // shared brushes
        private static readonly SolidColorBrush s_fg = new(global::Windows.UI.Color.FromArgb(255, 208, 208, 216));
        private static readonly SolidColorBrush s_dim = new(global::Windows.UI.Color.FromArgb(255, 102, 102, 128));
        private static readonly SolidColorBrush s_accent = new(global::Windows.UI.Color.FromArgb(255, 0, 229, 160));
        private static readonly SolidColorBrush s_err = new(global::Windows.UI.Color.FromArgb(255, 255, 107, 107));
        private static readonly SolidColorBrush s_tool = new(global::Windows.UI.Color.FromArgb(255, 255, 183, 77));
        private static readonly SolidColorBrush s_userBg = new(global::Windows.UI.Color.FromArgb(255, 0, 229, 160));
        private static readonly SolidColorBrush s_asstBg = new(global::Windows.UI.Color.FromArgb(255, 26, 26, 46));
        private static readonly SolidColorBrush s_errBg = new(global::Windows.UI.Color.FromArgb(255, 61, 18, 24));
        private static readonly SolidColorBrush s_toolBg = new(global::Windows.UI.Color.FromArgb(255, 15, 15, 26));
        private static readonly SolidColorBrush s_codeBg = new(global::Windows.UI.Color.FromArgb(255, 10, 10, 20));
        private static readonly SolidColorBrush s_white = new(global::Windows.UI.Color.FromArgb(255, 255, 255, 255));
        private static readonly SolidColorBrush s_italic = new(global::Windows.UI.Color.FromArgb(255, 176, 176, 192));

        // tool card tracking
        private readonly Dictionary<string, Border> _toolCards = new();

        public AiSidebar()
        {
            InitializeComponent();
        }

        // ======== public API ========

        public void SetConfig(string apiUrl, string apiKey, string model, string provider)
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
            _model = model;
            ModelBadge.Text = model;
            if (!string.IsNullOrEmpty(apiKey))
            {
                InputBox.IsEnabled = true;
                SendBtn.IsEnabled = true;
            }
        }

        public void SendPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return;
            _ = SendMessageAsync(prompt);
        }

        public void FocusPanel()
        {
            if (string.IsNullOrEmpty(_apiKey)) return;
            InputBox.IsEnabled = true;
            SendBtn.IsEnabled = true;
            InputBox.Focus(FocusState.Programmatic);
        }

        public void FocusInput() => FocusPanel();

        public void SendContext(string cwd, string os, string mode, string recentCommands)
        {
            _cwd = cwd;
            _osVer = os;
            _mode = mode;
            _recentCmds = recentCommands;
        }

        // ======== event handlers ========

        private void AgentToggle_Changed(object sender, RoutedEventArgs e) { }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
            MessagePanel.Children.Clear();
            ShowEmptyState();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _agent?.Cancel();
            SetStreaming(false);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }

        private void InputBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !_streaming)
            {
                e.Handled = true;
                SendBtn_Click(sender, e);
            }
        }

        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || _streaming) return;
            InputBox.Text = "";
            _ = SendMessageAsync(text);
        }

        // ======== core ========

        private async Task SendMessageAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                AddError("API key not configured. Use: config ai key <key>");
                return;
            }

            AddUserBubble(prompt);
            SetStreaming(true);

            var agent = MakeAgent();
            agent.ApiUrl = _apiUrl;
            agent.ApiKey = _apiKey;
            agent.Model = _model;
            _agent = agent;

            var sysPrompt = BuildSystemPrompt();

            agent.TextDelta += delta =>
                DispatcherQueue.TryEnqueue(() => AppendStreamingText(delta));

            agent.ToolCallStarted += (id, name, argsStr) =>
                DispatcherQueue.TryEnqueue(() => AddToolCard(id, name, argsStr));

            agent.ToolCallFinished += (id, resultStr, errorStr) =>
                DispatcherQueue.TryEnqueue(() => UpdateToolCard(id, resultStr, errorStr));

            agent.Error += msg =>
                DispatcherQueue.TryEnqueue(() => AddError(msg));

            agent.TurnComplete += () =>
                DispatcherQueue.TryEnqueue(() => { /* flush streaming bubble */ });

            try
            {
                var finalText = await agent.RunAsync(sysPrompt, _messages, prompt);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!string.IsNullOrEmpty(finalText) && !HasRecentAssistantMsg(finalText))
                        AddAssistantBubble(finalText);
                    SetStreaming(false);
                });
            }
            catch (OperationCanceledException)
            {
                DispatcherQueue.TryEnqueue(() => SetStreaming(false));
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    AddError(ex.Message);
                    SetStreaming(false);
                });
            }
        }

        private bool HasRecentAssistantMsg(string text)
        {
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                if (_messages[i].Role == "assistant" && _messages[i].Content == text)
                    return true;
                if (_messages[i].Role == "user") break;
            }
            return false;
        }

        // ======== message rendering ========

        private void AddUserBubble(string text)
        {
            _messages.Add(new AiMessage { Role = "user", Content = text });
            RemoveEmptyState();
            var b = new Border
            {
                Background = s_userBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                MaxWidth = 370,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock { Text = text, Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 13, 13, 18)), FontSize = 13, TextWrapping = TextWrapping.Wrap }
            };
            MessagePanel.Children.Add(b);
            ScrollDown();
        }

        private void AppendStreamingText(string delta)
        {
            // Ensure we have an assistant message to append to
            if (_messages.Count == 0 || _messages[^1].Role != "assistant")
            {
                _messages.Add(new AiMessage { Role = "assistant", Content = delta });
                RemoveEmptyState();
                var b = MakeMarkdownBubble(delta);
                b.Tag = "__streaming__";
                MessagePanel.Children.Add(b);
            }
            else
            {
                var msg = _messages[^1];
                _messages[^1] = new AiMessage { Role = "assistant", Content = msg.Content + delta };
                // Find streaming bubble and update
                foreach (var child in MessagePanel.Children)
                {
                    if (child is Border border && border.Tag is string t && t == "__streaming__")
                    {
                        UpdateBubbleMd(border, _messages[^1].Content!);
                        break;
                    }
                }
            }
            ScrollDown();
        }

        private void AddAssistantBubble(string markdown)
        {
            _messages.Add(new AiMessage { Role = "assistant", Content = markdown });
            RemoveEmptyState();
            var b = MakeMarkdownBubble(markdown);
            b.Tag = "assistant";
            MessagePanel.Children.Add(b);
            ScrollDown();
        }

        private Border MakeMarkdownBubble(string markdown)
        {
            var sp = new StackPanel();
            RenderMd(sp, markdown);
            return new Border
            {
                Background = s_asstBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                MaxWidth = 380,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = sp
            };
        }

        private void UpdateBubbleMd(Border bubble, string markdown)
        {
            if (bubble.Child is StackPanel sp)
            {
                sp.Children.Clear();
                RenderMd(sp, markdown);
            }
        }

        private void AddError(string text)
        {
            _messages.Add(new AiMessage { Role = "error", Content = text });
            RemoveEmptyState();
            var b = new Border
            {
                Background = s_errBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 380,
                Child = new TextBlock { Text = text, Foreground = s_err, FontSize = 12, TextWrapping = TextWrapping.Wrap }
            };
            MessagePanel.Children.Add(b);
            ScrollDown();
        }

        // ======== markdown ========

        private void RenderMd(StackPanel parent, string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return;

            // Extract code blocks
            var cbs = new List<(string lang, string code)>();
            var text = Regex.Replace(markdown, @"```(\w*)\n([\s\S]*?)```", m =>
            {
                var idx = cbs.Count;
                cbs.Add((m.Groups[1].Value, m.Groups[2].Value.TrimEnd('\n')));
                return $"\x00CB{idx}\x00";
            });

            var paras = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var para in paras)
            {
                var t = para.Trim();
                if (string.IsNullOrEmpty(t)) continue;

                // Code block placeholder
                var cbMatch = Regex.Match(t, @"\x00CB(\d+)\x00");
                if (cbMatch.Success && int.TryParse(cbMatch.Groups[1].Value, out var idx) && idx < cbs.Count)
                {
                    var cb = cbs[idx];
                    parent.Children.Add(MakeCodeBlock(cb.lang, cb.code));
                    continue;
                }

                // Headings
                if (t.StartsWith("### ")) { parent.Children.Add(MakeHeading(t[4..], 14)); continue; }
                if (t.StartsWith("## ")) { parent.Children.Add(MakeHeading(t[3..], 15)); continue; }
                if (t.StartsWith("# ")) { parent.Children.Add(MakeHeading(t[2..], 17)); continue; }

                // HR
                if (t == "---") { parent.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 6, 0, 6) }); continue; }

                // Blockquote
                if (t.StartsWith("> "))
                {
                    parent.Children.Add(new Border { BorderBrush = s_accent, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 4, 0, 4), Margin = new Thickness(0, 4, 0, 4), Child = RenderInline(t[2..].Trim()) });
                    continue;
                }

                // List item
                if (Regex.IsMatch(t, @"^[\-\*] "))
                {
                    var item = Regex.Replace(t, @"^[\-\*] ", "");
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var dot = new TextBlock { Text = "\u2022", Foreground = s_dim, FontSize = 12, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 8, 0) };
                    Grid.SetColumn(dot, 0);
                    Grid.SetColumn(RenderInline(item), 1);
                    grid.Children.Add(dot);
                    grid.Children.Add(RenderInline(item));
                    parent.Children.Add(grid);
                    continue;
                }

                // Normal paragraph
                parent.Children.Add(RenderInline(t));
            }
        }

        private static TextBlock MakeHeading(string text, int size)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                FontSize = size,
                Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 224, 224, 224)),
                Margin = new Thickness(0, size > 14 ? 12 : 8, 0, 4)
            };
        }

        private static TextBlock RenderInline(string text)
        {
            var tb = new TextBlock { Foreground = s_fg, FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            var parts = Regex.Split(text, @"(\*\*.*?\*\*|\*[^*]+\*|`[^`]+`)");
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (part.StartsWith("**") && part.EndsWith("**"))
                    tb.Inlines.Add(new Run { Text = part[2..^2], FontWeight = FontWeights.Bold, Foreground = s_white });
                else if (part.StartsWith("*") && part.EndsWith("*") && part.Length > 2)
                    tb.Inlines.Add(new Run { Text = part[1..^1], FontStyle = global::Windows.UI.Text.FontStyle.Italic, Foreground = s_italic });
                else if (part.StartsWith("`") && part.EndsWith("`"))
                    tb.Inlines.Add(new Run { Text = part[1..^1], FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12, Foreground = s_accent });
                else
                    tb.Inlines.Add(new Run { Text = part });
            }
            return tb;
        }

        private FrameworkElement MakeCodeBlock(string lang, string code)
        {
            var outer = new StackPanel();

            // Lang label
            if (!string.IsNullOrEmpty(lang))
            {
                outer.Children.Add(new TextBlock { Text = lang, FontSize = 10, Foreground = s_dim, Margin = new Thickness(0, 8, 0, 2) });
            }

            // Buttons
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4 };
            var capturedCode = code;

            var copyBtn = new Button
            {
                Content = "Copy", FontSize = 10, Foreground = s_dim,
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                Padding = new Thickness(8, 2, 8, 2)
            };
            copyBtn.Click += (_, _) =>
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(capturedCode);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
                copyBtn.Content = "Copied!";
                Task.Delay(1500).ContinueWith(_2 => DispatcherQueue.TryEnqueue(() => copyBtn.Content = "Copy"));
            };
            btnRow.Children.Add(copyBtn);

            var runBtn = new Button
            {
                Content = "Run", FontSize = 10, Foreground = s_accent,
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(20, 0, 229, 160)),
                Padding = new Thickness(8, 2, 8, 2)
            };
            runBtn.Click += (_, _) =>
            {
                RunInTerminalRequested?.Invoke(capturedCode);
                runBtn.Content = "Running...";
                Task.Delay(1000).ContinueWith(_2 => DispatcherQueue.TryEnqueue(() => runBtn.Content = "Run"));
            };
            btnRow.Children.Add(runBtn);

            outer.Children.Add(btnRow);

            // Code
            outer.Children.Add(new Border
            {
                Background = s_codeBg,
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Child = new TextBlock
                {
                    Text = code,
                    FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                    FontSize = 12,
                    Foreground = s_fg,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            });

            return outer;
        }

        // ======== tool cards ========

        private void AddToolCard(string callId, string toolName, string args)
        {
            RemoveEmptyState();
            var displayArgs = args.Length > 80 ? args[..80] + "..." : args;
            var icon = toolName switch
            {
                "exec" => ">",
                "read_file" => "R",
                "write_file" => "W",
                "list_dir" => "L",
                "search_files" => "S",
                _ => "?"
            };

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var iconTb = new TextBlock { Text = icon, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = s_tool, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var nameTb = new TextBlock { Text = toolName, FontWeight = FontWeights.SemiBold, Foreground = s_tool, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var argsTb = new TextBlock { Text = displayArgs, Foreground = s_dim, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var arrowTb = new TextBlock { Text = "\u25B6", Foreground = s_dim, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };

            Grid.SetColumn(iconTb, 0); Grid.SetColumn(nameTb, 1); Grid.SetColumn(argsTb, 2); Grid.SetColumn(arrowTb, 3);
            header.Children.Add(iconTb); header.Children.Add(nameTb); header.Children.Add(argsTb); header.Children.Add(arrowTb);

            var body = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
            body.Children.Add(new TextBlock { Text = "Running...", Foreground = s_dim, FontSize = 10 });

            var outer = new StackPanel();
            outer.Children.Add(header);
            outer.Children.Add(body);

            var card = new Border
            {
                Background = s_toolBg,
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 255, 183, 77)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Tag = callId,
                Child = outer
            };

            header.PointerPressed += (_, _) =>
            {
                body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                arrowTb.Text = body.Visibility == Visibility.Visible ? "\u25BC" : "\u25B6";
            };

            _toolCards[callId] = card;
            MessagePanel.Children.Add(card);
            ScrollDown();
        }

        private void UpdateToolCard(string callId, string? resultStr, string? errorStr)
        {
            if (!_toolCards.TryGetValue(callId, out var card)) return;
            if (card.Child is not StackPanel outer || outer.Children.Count < 2) return;
            if (outer.Children[1] is not StackPanel body) return;

            body.Children.Clear();
            body.Visibility = Visibility.Visible;

            // Update arrow
            if (outer.Children[0] is Grid hdr && hdr.Children.Count >= 4 && hdr.Children[3] is TextBlock arr)
                arr.Text = "\u25BC";

            if (errorStr != null)
            {
                body.Children.Add(new TextBlock { Text = "[ERROR] " + errorStr, Foreground = s_err, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                body.Children.Add(new TextBlock { Text = "OK", Foreground = s_accent, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) });
                if (!string.IsNullOrEmpty(resultStr) && resultStr != "OK")
                {
                    var display = resultStr.Length > 2000 ? resultStr[..2000] + "\n... (truncated)" : resultStr;
                    body.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(40, 0, 0, 0)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 6, 8, 6),
                        Child = new TextBlock { Text = display, Foreground = s_fg, FontSize = 11, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), TextWrapping = TextWrapping.Wrap }
                    });
                }
                card.BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(80, 0, 229, 160));
            }
        }

        // ======== helpers ========

        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are OpenNCL AI, an agent integrated into OpenNCL terminal (v4.0).");
            sb.AppendLine();
            sb.AppendLine($"Working directory: {_cwd}");
            sb.AppendLine($"OS: {_osVer}");
            sb.AppendLine($"Terminal mode: {_mode}");
            if (!string.IsNullOrWhiteSpace(_recentCmds))
                sb.AppendLine($"Recent commands: {_recentCmds}");
            sb.AppendLine();
            sb.AppendLine("You can call tools: exec, read_file, write_file, list_dir, search_files, get_context.");
            sb.AppendLine("Be concise. Use tools to DO things. Use markdown for formatting.");
            return sb.ToString();
        }

        private AgentLoop MakeAgent()
        {
            var tools = new List<AiTool>();

            void AddTool(string name, string desc, string propsJson)
            {
                var schema = JsonDocument.Parse(propsJson).RootElement;
                tools.Add(new AiTool
                {
                    Name = name,
                    Description = desc,
                    InputSchema = schema,
                    IsReadOnly = name != "exec" && name != "write_file",
                    Execute = async (args, ct2) =>
                    {
                        var handler = ToolCallRequested;
                        if (handler == null) return "Tool executor not available";
                        return await handler(name, args.GetRawText());
                    }
                });
            }

            AddTool("exec", "Execute a shell command and return output.",
                "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"}},\"required\":[\"command\"]}");
            AddTool("read_file", "Read file contents.",
                "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"]}");
            AddTool("write_file", "Write content to a file.",
                "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"path\",\"content\"]}");
            AddTool("list_dir", "List directory contents.",
                "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[]}");
            AddTool("search_files", "Search for files by pattern.",
                "{\"type\":\"object\",\"properties\":{\"pattern\":{\"type\":\"string\"},\"directory\":{\"type\":\"string\"}},\"required\":[\"pattern\"]}");
            AddTool("get_context", "Get current terminal context.",
                "{\"type\":\"object\",\"properties\":{},\"required\":[]}");

            return new AgentLoop(tools, maxRounds: AgentToggle.IsChecked == true ? 10 : 0);
        }

        private void SetStreaming(bool streaming)
        {
            _streaming = streaming;
            SendBtn.IsEnabled = !streaming;
            InputBox.IsEnabled = !streaming;
            StopBtn.Visibility = streaming ? Visibility.Visible : Visibility.Collapsed;
            if (!streaming) InputBox.Focus(FocusState.Programmatic);
        }

        private void RemoveEmptyState()
        {
            for (int i = MessagePanel.Children.Count - 1; i >= 0; i--)
            {
                if (MessagePanel.Children[i] is TextBlock tb2 && tb2.Tag is string t2 && t2 == "empty")
                    MessagePanel.Children.RemoveAt(i);
            }
        }

        private void ShowEmptyState()
        {
            var tb = new TextBlock
            {
                Text = "\u2728\nAsk anything...",
                Foreground = s_dim,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Opacity = 0.3,
                Tag = "empty",
                Margin = new Thickness(0, 200, 0, 0)
            };
            MessagePanel.Children.Add(tb);
        }

        private void ScrollDown()
        {
            MessageScroller.UpdateLayout();
            MessageScroller.ChangeView(null, double.MaxValue, null);
        }
    }
}
