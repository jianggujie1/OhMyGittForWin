using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public class AiService : IAiService
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        UseProxy = true,
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
    })
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    private static string SettingsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OhMyGittForWin", "ai_settings.json");

    public AiSettingsModel LoadSettings()
    {
        try
        {
            string path = SettingsFilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AiSettingsModel>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Fallback to defaults
        }

        return new AiSettingsModel();
    }

    public void SaveSettings(AiSettingsModel settings)
    {
        try
        {
            string path = SettingsFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存 AI 配置失败: {ex.Message}", ex);
        }
    }

    public async Task<string> GenerateCommitMessageAsync(string diffContent, AiSettingsModel? settings = null)
    {
        settings ??= LoadSettings();

        if (string.IsNullOrWhiteSpace(diffContent))
        {
            throw new ArgumentException("待提交的代码差异内容为空，无法生成提交信息。");
        }

        bool isLocal = IsLocalEndpoint(settings.ApiBaseUrl);
        if (!isLocal && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("未配置 API Key。请点击设置按钮配置您的 AI API 密钥。");
        }

        string lang = settings.Language == "English" ? "English" : "中文";
        string styleDesc = settings.PromptStyle switch
        {
            "简洁单行" => "生成单行简洁明了的提交信息（例如：feat: 支持 AI 自动生成提交信息），不超过 50 个字符。",
            "详细要点" => "首行生成精炼概要（<=72字符），并在空行后使用无序列表（- 说明）列出详细的改动要点。",
            _ => "严格遵循 Conventional Commits 规范格式（<type>(<scope>): <subject>），首行精炼概要（不超过 72 字符），并在空行后使用列表列出主要变动点。"
        };

        string systemPrompt = $@"你是一个专业的 Git 提交信息（Commit Message）编写专家。
请根据用户提供的 Git 代码变更差异（diff），生成最恰当、专业、精炼的 Git 提交信息。

规范要求：
1. 风格规范：{styleDesc}
2. 常用类型包括：
   - feat: 新功能或新特性
   - fix: 修复 Bug 或问题
   - refactor: 代码重构（不改变现有功能的结构变动）
   - perf: 性能优化
   - style: 代码格式调整
   - docs: 文档变更
   - test: 测试相关
   - chore: 构建过程或依赖变动
3. 语言要求：必须使用 {lang}。
4. 输出要求：只输出提交信息本身，严禁输出任何 Markdown 代码块标记（如 ``` 或 ```markdown），严禁输出任何多余的废话、前缀或寒暄说明，确保输出可以直接作为 git commit -m 的信息。
5. 概括完整性：请结合文件总览与各个模块的代码差异，全面概括本次提交的核心变动，不要遗漏重要模块（如新增的功能、UI界面、服务层、配置等）。";

        string userPrompt = $"以下是待提交的 Git 代码变更详情：\n\n{diffContent}\n\n请根据以上变更生成最合适的 Git Commit Message：";

        int maxTokens = settings.MaxTokens > 0 ? settings.MaxTokens : 3072;
        return await SendChatCompletionAsync(systemPrompt, userPrompt, settings, maxTokens);
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(AiSettingsModel settings)
    {
        try
        {
            bool isLocal = IsLocalEndpoint(settings.ApiBaseUrl);
            if (!isLocal && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return (false, "请先填写 API Key！");
            }

            string testResult = await SendChatCompletionAsync(
                "You are an assistant.",
                "Ping. Reply with 'OK'.",
                settings,
                maxTokens: 500);

            return (true, $"连接成功！模型响应: {testResult.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, $"连接失败: {ex.Message}");
        }
    }

    private static async Task<string> SendChatCompletionAsync(
        string systemPrompt, 
        string userPrompt, 
        AiSettingsModel settings, 
        int maxTokens = 2048)
    {
        string baseUrl = settings.ApiBaseUrl.Trim().TrimEnd('/');
        string endpoint = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) 
            ? baseUrl 
            : $"{baseUrl}/chat/completions";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
        }

        var payload = new
        {
            model = settings.ModelName.Trim(),
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = maxTokens
        };

        string jsonPayload = JsonSerializer.Serialize(payload);
        requestMessage.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        int timeoutSec = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 180;
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cts.Token);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new HttpRequestException($"网络请求超时（已等待 {timeoutSec} 秒未响应）。若直连海外大模型（如 NVIDIA / OpenAI）较慢或排队，建议开启系统代理加速，或在设置中延长超时时间 / 换用国内高速模型（DeepSeek、硅基流动）。", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"网络请求失败: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"网络请求失败: {ex.Message}", ex);
        }

        string responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            string errorMessage = TryExtractErrorMessage(responseContent, (int)response.StatusCode, endpoint);
            throw new HttpRequestException($"API 请求失败 (HTTP {(int)response.StatusCode}): {errorMessage}");
        }

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                string finishReason = string.Empty;
                if (firstChoice.TryGetProperty("finish_reason", out var finishReasonElem) &&
                    finishReasonElem.ValueKind == JsonValueKind.String)
                {
                    finishReason = finishReasonElem.GetString() ?? string.Empty;
                }

                if (firstChoice.TryGetProperty("message", out var msg))
                {
                    string content = string.Empty;
                    if (msg.TryGetProperty("content", out var contentElem) &&
                        contentElem.ValueKind == JsonValueKind.String)
                    {
                        content = contentElem.GetString() ?? string.Empty;
                    }

                    string cleaned = CleanUpCommitMessage(content);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        return cleaned;
                    }

                    // Content is empty - check why
                    if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "AI 模型思考或输出超出了 Token 预算上限（finish_reason: length），导致正文未能生成。\n\n" +
                            "解决建议：\n" +
                            "1. 当前使用的模型（如 GLM、DeepSeek-R1 等推理模型）在分析大量代码差异时消耗了过多思考 Token；\n" +
                            "2. 请点击设置按钮（⚙️）将【Token 上限】调大（如 4096），或换用通用对话模型（如 deepseek-chat、gpt-4o-mini、llama-3.1 等）；\n" +
                            "3. 亦可分批暂存代码，避免单次提交包含过多改动。");
                    }

                    // Check if reasoning_content exists without content
                    if (msg.TryGetProperty("reasoning_content", out var rcElem) &&
                        rcElem.ValueKind == JsonValueKind.String)
                    {
                        string rc = rcElem.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(rc))
                        {
                            throw new InvalidOperationException(
                                "AI 模型仅返回了思考过程（reasoning_content），未能输出最终提交正文。\n\n" +
                                "建议更换为标准的通用对话/指令模型（如 deepseek-chat、gpt-4o-mini）。");
                        }
                    }
                }
            }

            throw new InvalidOperationException("API 响应中未包含有效的提交信息正文。");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"解析 API 响应 JSON 失败: {ex.Message}", ex);
        }
    }

    private static string CleanUpCommitMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string text = raw.Trim();

        // Strip <think>...</think> reasoning blocks if model inlined thoughts into content
        if (text.Contains("<think>"))
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<think>[\s\S]*?</think>", string.Empty).Trim();
            if (text.StartsWith("<think>"))
            {
                // Unclosed think block (cut off by token limit)
                return string.Empty;
            }
        }

        // Strip markdown code fences if LLM produced them
        if (text.StartsWith("```"))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline != -1)
            {
                text = text.Substring(firstNewline + 1);
            }
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }
            text = text.Trim();
        }

        // Strip single backticks around full message if present
        if (text.StartsWith("`") && text.EndsWith("`") && text.Length > 2)
        {
            text = text.Substring(1, text.Length - 2).Trim();
        }

        return text;
    }

    private static string TryExtractErrorMessage(string responseContent, int statusCode = 0, string endpoint = "")
    {
        if (statusCode == 404 && endpoint.Contains("nvidia.com", StringComparison.OrdinalIgnoreCase))
        {
            return "未找到模型 (HTTP 404)。NVIDIA NIM 服务要求【模型名称 (Model)】必须包含厂商前缀且存在于 NVIDIA Catalog，例如：nvidia/llama-3.1-nemotron-70b-instruct 或 meta/llama-3.2-11b-vision-instruct。请检查模型名称。";
        }

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errorElem))
            {
                if (errorElem.TryGetProperty("message", out var msgElem))
                {
                    return msgElem.GetString() ?? responseContent;
                }
            }
            if (root.TryGetProperty("detail", out var detailElem))
            {
                return detailElem.GetString() ?? responseContent;
            }
            if (root.TryGetProperty("title", out var titleElem))
            {
                string title = titleElem.GetString() ?? "";
                string detail = root.TryGetProperty("detail", out var d) ? d.GetString() ?? "" : "";
                return string.IsNullOrEmpty(detail) ? title : $"{title}: {detail}";
            }
        }
        catch
        {
            // Use raw content if not JSON
        }

        if (responseContent.Length > 200)
        {
            return responseContent.Substring(0, 200) + "...";
        }
        return string.IsNullOrWhiteSpace(responseContent) ? $"HTTP {statusCode}" : responseContent;
    }

    private static bool IsLocalEndpoint(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
