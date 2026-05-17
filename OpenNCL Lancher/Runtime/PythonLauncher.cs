using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNCL_Lancher.Runtime
{
    public class PythonLauncher : IDisposable
    {
        private Process? _pythonProcess;
        private StreamWriter? _stdin;
        private StreamReader? _stdout;
        private StreamReader? _stderr;
        private readonly StringBuilder _outputBuffer = new();
        private readonly object _bufferLock = new();
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private Task? _readErrTask;
        private volatile bool _isReady;
        private volatile bool _kernelReady;
        private TaskCompletionSource<bool>? _readyTcs;

        public event Action<string>? OutputReceived;
        public event Action? ProcessExited;
        public bool IsRunning => _pythonProcess != null && !_pythonProcess.HasExited;
        public bool IsReady => _isReady;
        public bool KernelReady => _kernelReady;
        public string? LastError { get; private set; }

        public async Task<bool> StartAsync()
        {
            if (IsRunning) return true;
            LastError = null;
            _kernelReady = false;
            _readyTcs = new TaskCompletionSource<bool>();

            try
            {
                var scriptPath = GetPythonScriptPath();

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

                _pythonProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _pythonProcess.Exited += (s, e) =>
                {
                    _isReady = false;
                    _kernelReady = false;
                    ProcessExited?.Invoke();
                };

                _pythonProcess.Start();
                _stdin = _pythonProcess.StandardInput;
                _stdout = _pythonProcess.StandardOutput;
                _stderr = _pythonProcess.StandardError;

                _cts = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadOutputLoop(_cts.Token));
                _readErrTask = Task.Run(() => ReadErrorLoop(_cts.Token));

                _isReady = true;

                var timeoutTask = Task.Delay(5000);
                var completed = await Task.WhenAny(_readyTcs.Task, timeoutTask);
                _kernelReady = completed == _readyTcs.Task;

                if (!_kernelReady)
                    LastError = "Kernel handshake timeout (5s)";

                return _kernelReady;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Stop();
                return false;
            }
        }

        public void SendCommand(string command)
        {
            if (!IsReady || _stdin == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(command)) return;

            try
            {
                _stdin.WriteLine(command);
                _stdin.Flush();
            }
            catch { }
        }

        private async Task ReadOutputLoop(CancellationToken ct)
        {
            var buf = new char[4096];
            try
            {
                while (!ct.IsCancellationRequested && _stdout != null)
                {
                    var readTask = _stdout.ReadAsync(buf, 0, buf.Length);
                    var completed = await Task.WhenAny(readTask, Task.Delay(-1, ct));
                    if (completed != readTask) break;

                    var count = await readTask;
                    if (count == 0) break;

                    var text = new string(buf, 0, count);

                    if (!string.IsNullOrEmpty(text))
                    {
                        OnOutputReceived(text);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        private async Task ReadErrorLoop(CancellationToken ct)
        {
            var buf = new char[4096];
            try
            {
                while (!ct.IsCancellationRequested && _stderr != null)
                {
                    var readTask = _stderr.ReadAsync(buf, 0, buf.Length);
                    var completed = await Task.WhenAny(readTask, Task.Delay(-1, ct));
                    if (completed != readTask) break;

                    var count = await readTask;
                    if (count == 0) break;

                    var text = new string(buf, 0, count);
                    if (!string.IsNullOrEmpty(text))
                    {
                        OnOutputReceived(text);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        public void Stop()
        {
            _isReady = false;
            _cts?.Cancel();

            try
            {
                if (_stdin != null && _pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _stdin.WriteLine("__exit__");
                    _stdin.Flush();
                    _pythonProcess.WaitForExit(2000);
                }
            }
            catch { }

            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                try { _pythonProcess.Kill(); } catch { }
            }

            _stdin?.Dispose();
            _stdout?.Dispose();
            _stderr?.Dispose();
            _pythonProcess?.Dispose();
            _cts?.Dispose();

            _stdin = null;
            _stdout = null;
            _stderr = null;
            _pythonProcess = null;
            _cts = null;
            _readTask = null;
            _readErrTask = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnOutputReceived(string text)
        {
            if (!_kernelReady && text.Contains("__READY__"))
            {
                _kernelReady = true;
                _readyTcs?.TrySetResult(true);
                text = text.Replace("__READY__\n", "").Replace("__READY__\r\n", "").Replace("__READY__", "");
                if (string.IsNullOrWhiteSpace(text)) return;
            }

            lock (_bufferLock)
            {
                _outputBuffer.Append(text);
            }
            OutputReceived?.Invoke(text);
        }

        private static string GetPythonScriptPath()
        {
            var baseDir = AppContext.BaseDirectory;

            var candidates = new List<string>
            {
                Path.Combine(baseDir, "python", "launcher.py"),
                Path.Combine(Directory.GetCurrentDirectory(), "python", "launcher.py"),
            };

            var dir = baseDir;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
                candidates.Add(Path.Combine(dir, "python", "launcher.py"));
            }

            foreach (var p in candidates)
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }

            throw new FileNotFoundException(
                "Cannot find python/launcher.py. Searched: " +
                string.Join(", ", candidates.GetRange(0, Math.Min(4, candidates.Count))));
        }
    }
}
