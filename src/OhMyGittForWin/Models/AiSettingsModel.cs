namespace OhMyGittForWin.Models;

public class AiSettingsModel
{
    public string Provider { get; set; } = "DeepSeek";
    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "deepseek-chat";
    public string Language { get; set; } = "中文"; // "中文" or "English"
    public string PromptStyle { get; set; } = "Conventional Commits"; // "Conventional Commits" or "简洁单行" or "详细要点"
    public int TimeoutSeconds { get; set; } = 180; // Default 180s (3 minutes)
    public int MaxTokens { get; set; } = 3072; // Default 3072 tokens to support reasoning models & multi-file summaries
}
