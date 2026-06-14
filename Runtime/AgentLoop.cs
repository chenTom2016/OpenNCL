using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNCL_Lancher.Runtime
{
    /// <summary>Tool definition matching Claude Code's Tool architecture.</summary>
    public sealed class AiTool
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string[]? Aliases { get; init; }
        public JsonElement InputSchema { get; init; }
        /// <summary>Returns null on success, error message on failure.</summary>
        public Func<JsonElement, CancellationToken, Task<string?>> Execute { get; init; } = null!;
        public bool IsReadOnly { get; init; }

        public object ToApiDefinition()
        {
            return new
            {
                type = "function",
                function = new
                {
                    name = Name,
                    description = Description,
                    parameters = InputSchema.ValueKind == JsonValueKind.Object
                        ? InputSchema
                        : JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}").RootElement
                }
            };
        }
    }

    /// <summary>Message in a conversation.</summary>
    public struct AiMessage
    {
        public string Role;       // "system", "user", "assistant", "tool"
        public string? Content;
        public string? ToolCallId;
        public List<AiToolCall>? ToolCalls;
    }

    public struct AiToolCall
    {
        public string Id;
        public string Name;
        public string Arguments;
    }

    /// <summary>
    /// Claude Code 风格的 Agent Loop。
    /// 1. 构建 system prompt + user message
    /// 2. 调用 API (streaming)
    /// 3. 如果响应包含 tool_calls → 执行 → 追加结果 → 回到 2
    /// 4. 最多 maxRounds 轮
    /// </summary>
    public sealed class AgentLoop : IDisposable
    {
        private readonly HttpClient _http;
        private readonly List<AiTool> _tools;
        private readonly int _maxRounds;

        public string ApiUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "gpt-4o";

        public event Action<string>? TextDelta;     // streaming text chunk
        public event Action<string, string, string>? ToolCallStarted;   // callId, name, args
        public event Action<string, string?, string?>? ToolCallFinished; // callId, result, error
        public event Action<string>? Error;
        public event Action? TurnComplete;

        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public AgentLoop(List<AiTool> tools, int maxRounds = 10)
        {
            _tools = tools;
            _maxRounds = maxRounds;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public void Cancel() { try { _cts.Cancel(); } catch { } }
        public void Dispose() { _cts.Dispose(); _http.Dispose(); }

        /// <summary>
        /// 执行一次 agent run: system prompt + messages → streaming + tool loop.
        /// </summary>
        public async Task<string> RunAsync(
            string systemPrompt,
            List<AiMessage> messages,
            string userPrompt)
        {
            var apiMessages = new List<object>();

            // System prompt
            apiMessages.Add(new { role = "system", content = systemPrompt });

            // History (skip tool messages for API compatibility)
            foreach (var m in messages)
            {
                if (m.Role == "system" || m.Role == "tool") continue;
                if (m.Role == "assistant" && m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    apiMessages.Add(new
                    {
                        role = "assistant",
                        content = (object?)m.Content,
                        tool_calls = m.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        })
                    });
                }
                else
                {
                    apiMessages.Add(new { role = m.Role, content = (object?)m.Content });
                }
            }

            // Current user prompt
            apiMessages.Add(new { role = "user", content = userPrompt });

            // === Agent Loop (Claude Code query.ts pattern) ===
            for (int round = 0; round < _maxRounds; round++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var (finished, content, toolCalls) = await CallApiStreamingAsync(apiMessages);

                if (toolCalls.Count > 0)
                {
                    // Add assistant message with tool calls
                    apiMessages.Add(new
                    {
                        role = "assistant",
                        content = (object?)content,
                        tool_calls = toolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        })
                    });

                    // Execute each tool call
                    foreach (var tc in toolCalls)
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        string? result = null;
                        string? error = null;

                        ToolCallStarted?.Invoke(tc.Id, tc.Name, tc.Arguments);

                        var tool = _tools.FirstOrDefault(t =>
                            t.Name == tc.Name || (t.Aliases?.Contains(tc.Name) ?? false));

                        if (tool == null)
                        {
                            error = $"Unknown tool: {tc.Name}";
                        }
                        else
                        {
                            try
                            {
                                using var argsDoc = JsonDocument.Parse(tc.Arguments);
                                error = await tool.Execute(argsDoc.RootElement, _cts.Token);
                                if (error == null) result = "OK";
                            }
                            catch (Exception ex)
                            {
                                error = ex.Message;
                            }
                        }

                        ToolCallFinished?.Invoke(tc.Id, result, error);

                        apiMessages.Add(new
                        {
                            role = "tool",
                            tool_call_id = tc.Id,
                            content = error ?? result ?? "(empty)"
                        });
                    }

                    TurnComplete?.Invoke();
                }
                else
                {
                    // No tool calls → final answer
                    return content ?? "";
                }
            }

            // Max rounds reached — ask for summary (Claude Code pattern)
            apiMessages.Add(new { role = "user", content = "(Max rounds reached. Summarize what was done.)" });
            var (_, summary, _) = await CallApiStreamingAsync(apiMessages, skipTools: true);
            return summary ?? "";
        }

        /// <summary>
        /// Calls the API with streaming. Returns (finished, textContent, toolCalls).
        /// </summary>
        private async Task<(bool finished, string? text, List<AiToolCall> toolCalls)> CallApiStreamingAsync(
            List<object> messages, bool skipTools = false)
        {
            var bodyObj = new Dictionary<string, object>
            {
                ["model"] = Model,
                ["messages"] = messages,
                ["stream"] = true,
                ["max_tokens"] = 4096
            };
            if (!skipTools && _tools.Count > 0)
            {
                bodyObj["tools"] = _tools.Select(t => t.ToApiDefinition()).ToList();
                bodyObj["tool_choice"] = "auto";
            }

            var body = JsonSerializer.Serialize(bodyObj);
            var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", "Bearer " + ApiKey);

            var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _cts.Token);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync();
                var errMsg = errBody;
                try
                {
                    using var errDoc = JsonDocument.Parse(errBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var e) &&
                        e.TryGetProperty("message", out var em))
                        errMsg = em.GetString() ?? errBody;
                }
                catch { }
                Error?.Invoke($"API {resp.StatusCode}: {errMsg[..Math.Min(errMsg.Length, 300)]}");
                return (true, null, new List<AiToolCall>());
            }

            var textContent = new StringBuilder();
            var toolCalls = new Dictionary<int, AiToolCall>();
            var finished = false;

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var buffer = "";

            while (!reader.EndOfStream)
            {
                _cts.Token.ThrowIfCancellationRequested();
                buffer += await reader.ReadLineAsync() + "\n";

                while (true)
                {
                    var newlineIdx = buffer.IndexOf('\n');
                    if (newlineIdx < 0) break;
                    var line = buffer[..newlineIdx].Trim();
                    buffer = buffer[(newlineIdx + 1)..];

                    if (!line.StartsWith("data: ")) continue;
                    var data = line[6..];
                    if (data == "[DONE]") { finished = true; break; }

                    try
                    {
                        using var chunk = JsonDocument.Parse(data);
                        var choices = chunk.RootElement.TryGetProperty("choices", out var c) ? c : default;
                        if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) continue;

                        var delta = choices[0].TryGetProperty("delta", out var d) ? d : default;
                        if (delta.ValueKind != JsonValueKind.Object) continue;

                        // Text content
                        if (delta.TryGetProperty("content", out var ct) && ct.ValueKind == JsonValueKind.String)
                        {
                            var txt = ct.GetString();
                            if (!string.IsNullOrEmpty(txt))
                            {
                                textContent.Append(txt);
                                TextDelta?.Invoke(txt);
                            }
                        }

                        // Tool calls (streaming, accumulated across chunks)
                        if (delta.TryGetProperty("tool_calls", out var tcs))
                        {
                            foreach (var tc in tcs.EnumerateArray())
                            {
                                var idx = tc.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                                if (!toolCalls.TryGetValue(idx, out var call))
                                {
                                    call = new AiToolCall();
                                    toolCalls[idx] = call;
                                }

                                if (tc.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                                    call.Id = id.GetString()!;
                                if (tc.TryGetProperty("function", out var fn))
                                {
                                    if (fn.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                                        call.Name = nm.GetString()!;
                                    if (fn.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
                                        call.Arguments += args.GetString();
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (finished) break;
            }

            return (finished, textContent.Length > 0 ? textContent.ToString() : null, toolCalls.Values.ToList());
        }

        /// <summary>Quick non-streaming call (for inline AI).</summary>
        public static async Task<string?> QuickGenerateAsync(
            string apiUrl, string apiKey, string model,
            string systemPrompt, string userPrompt,
            CancellationToken cancellationToken = default)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var body = JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 300,
                temperature = 0.2
            });

            var req = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", "Bearer " + apiKey);

            var resp = await http.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var result = doc.RootElement
                .TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
                ? choices[0].TryGetProperty("message", out var msg)
                    ? msg.TryGetProperty("content", out var msgContent) ? msgContent.GetString() : null
                    : null
                : null;

            return result?.Trim();
        }
    }
}
