using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OhMyGittForWin.Models;
using OhMyGittForWin.Services;

namespace OhMyGittForWin.Views;

public partial class AiSettingsDialog : Window
{
    private readonly IAiService _aiService;
    private readonly AiSettingsModel _settings;
    private bool _isInitializing = true;

    public AiSettingsDialog(AiSettingsModel settings, IAiService aiService)
    {
        InitializeComponent();
        _settings = settings;
        _aiService = aiService;

        LoadFormData();
        _isInitializing = false;
    }

    private void LoadFormData()
    {
        TxtApiKey.Text = _settings.ApiKey ?? string.Empty;
        TxtApiBaseUrl.Text = _settings.ApiBaseUrl ?? "https://api.deepseek.com/v1";
        TxtModelName.Text = _settings.ModelName ?? "deepseek-chat";

        switch (_settings.Provider)
        {
            case "NVIDIA":
                RbNvidia.IsChecked = true;
                break;
            case "OpenAI":
                RbOpenAI.IsChecked = true;
                break;
            case "SiliconFlow":
                RbSiliconFlow.IsChecked = true;
                break;
            case "Ollama":
                RbOllama.IsChecked = true;
                break;
            case "Custom":
                RbCustom.IsChecked = true;
                break;
            default:
                RbDeepSeek.IsChecked = true;
                break;
        }

        // Set PromptStyle
        foreach (ComboBoxItem item in CmbPromptStyle.Items)
        {
            if (item.Content?.ToString()?.StartsWith(_settings.PromptStyle ?? "") == true)
            {
                CmbPromptStyle.SelectedItem = item;
                break;
            }
        }

        // Set Language
        foreach (ComboBoxItem item in CmbLanguage.Items)
        {
            if (item.Content?.ToString() == _settings.Language)
            {
                CmbLanguage.SelectedItem = item;
                break;
            }
        }

        // Set Timeout
        int currentTimeout = _settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 180;
        foreach (ComboBoxItem item in CmbTimeout.Items)
        {
            if (item.Tag?.ToString() == currentTimeout.ToString())
            {
                CmbTimeout.SelectedItem = item;
                break;
            }
        }

        // Set MaxTokens
        int currentMaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 2048;
        foreach (ComboBoxItem item in CmbMaxTokens.Items)
        {
            if (item.Tag?.ToString() == currentMaxTokens.ToString())
            {
                CmbMaxTokens.SelectedItem = item;
                break;
            }
        }
    }

    private void ProviderPreset_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (RbDeepSeek.IsChecked == true)
        {
            TxtApiBaseUrl.Text = "https://api.deepseek.com/v1";
            TxtModelName.Text = "deepseek-chat";
        }
        else if (RbNvidia.IsChecked == true)
        {
            TxtApiBaseUrl.Text = "https://integrate.api.nvidia.com/v1";
            TxtModelName.Text = "nvidia/llama-3.1-nemotron-70b-instruct";
        }
        else if (RbOpenAI.IsChecked == true)
        {
            TxtApiBaseUrl.Text = "https://api.openai.com/v1";
            TxtModelName.Text = "gpt-4o-mini";
        }
        else if (RbSiliconFlow.IsChecked == true)
        {
            TxtApiBaseUrl.Text = "https://api.siliconflow.cn/v1";
            TxtModelName.Text = "deepseek-ai/DeepSeek-V3";
        }
        else if (RbOllama.IsChecked == true)
        {
            TxtApiBaseUrl.Text = "http://localhost:11434/v1";
            TxtModelName.Text = "qwen2.5:7b";
        }
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var tempSettings = GetSettingsFromForm();

        BtnTest.IsEnabled = false;
        TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        TxtTestResult.Text = "⏳ 正在测试连接...";

        try
        {
            var (success, message) = await _aiService.TestConnectionAsync(tempSettings);
            if (success)
            {
                TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                TxtTestResult.Text = "✓ " + message;
            }
            else
            {
                TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                TxtTestResult.Text = "✕ " + message;
            }
        }
        catch (Exception ex)
        {
            TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtTestResult.Text = "✕ " + ex.Message;
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var updated = GetSettingsFromForm();
        _settings.Provider = updated.Provider;
        _settings.ApiBaseUrl = updated.ApiBaseUrl;
        _settings.ApiKey = updated.ApiKey;
        _settings.ModelName = updated.ModelName;
        _settings.PromptStyle = updated.PromptStyle;
        _settings.Language = updated.Language;
        _settings.TimeoutSeconds = updated.TimeoutSeconds;
        _settings.MaxTokens = updated.MaxTokens;

        try
        {
            _aiService.SaveSettings(_settings);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private AiSettingsModel GetSettingsFromForm()
    {
        string provider = "DeepSeek";
        if (RbNvidia.IsChecked == true) provider = "NVIDIA";
        else if (RbOpenAI.IsChecked == true) provider = "OpenAI";
        else if (RbSiliconFlow.IsChecked == true) provider = "SiliconFlow";
        else if (RbOllama.IsChecked == true) provider = "Ollama";
        else if (RbCustom.IsChecked == true) provider = "Custom";

        string promptStyle = "Conventional Commits";
        if (CmbPromptStyle.SelectedItem is ComboBoxItem psi && psi.Content != null)
        {
            string txt = psi.Content.ToString()!;
            if (txt.Contains("简洁")) promptStyle = "简洁单行";
            else if (txt.Contains("详细")) promptStyle = "详细要点";
        }

        string language = "中文";
        if (CmbLanguage.SelectedItem is ComboBoxItem li && li.Content != null)
        {
            language = li.Content.ToString()!;
        }

        int timeoutSeconds = 180;
        if (CmbTimeout.SelectedItem is ComboBoxItem ti && ti.Tag != null && int.TryParse(ti.Tag.ToString(), out int parsedTimeout))
        {
            timeoutSeconds = parsedTimeout;
        }

        int maxTokens = 2048;
        if (CmbMaxTokens.SelectedItem is ComboBoxItem mti && mti.Tag != null && int.TryParse(mti.Tag.ToString(), out int parsedMaxTokens))
        {
            maxTokens = parsedMaxTokens;
        }

        string apiKey = TxtApiKey.Text.Trim();
        if (apiKey.StartsWith("-") && apiKey.Length > 20)
        {
            // Auto repair missing 'nvapi'
            apiKey = "nvapi" + apiKey;
            TxtApiKey.Text = apiKey;
        }

        return new AiSettingsModel
        {
            Provider = provider,
            ApiKey = apiKey,
            ApiBaseUrl = TxtApiBaseUrl.Text.Trim(),
            ModelName = TxtModelName.Text.Trim(),
            PromptStyle = promptStyle,
            Language = language,
            TimeoutSeconds = timeoutSeconds,
            MaxTokens = maxTokens
        };
    }
}
