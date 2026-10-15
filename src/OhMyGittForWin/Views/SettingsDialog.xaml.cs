using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using OhMyGittForWin.Models;
using OhMyGittForWin.Services;

namespace OhMyGittForWin.Views;

public partial class SettingsDialog : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IAiService _aiService;
    private readonly AppSettingsModel _appSettings;
    private readonly AiSettingsModel _aiSettings;
    private bool _isInitializing = true;

    public SettingsDialog(int initialTabIndex = 0, ISettingsService? settingsService = null, IAiService? aiService = null)
    {
        InitializeComponent();

        _settingsService = settingsService ?? new SettingsService();
        _aiService = aiService ?? new AiService();

        _appSettings = _settingsService.LoadSettings();
        _aiSettings = _aiService.LoadSettings();

        LoadGeneralData();
        LoadAiData();

        SwitchTab(initialTabIndex);
        _isInitializing = false;
    }

    private void SwitchTab(int index)
    {
        if (index == 0)
        {
            TabGeneral.IsChecked = true;
            PanelGeneral.Visibility = Visibility.Visible;
            PanelAi.Visibility = Visibility.Collapsed;
            AiTestPanel.Visibility = Visibility.Collapsed;
            TxtGeneralHint.Visibility = Visibility.Visible;
        }
        else
        {
            TabAi.IsChecked = true;
            PanelGeneral.Visibility = Visibility.Collapsed;
            PanelAi.Visibility = Visibility.Visible;
            AiTestPanel.Visibility = Visibility.Visible;
            TxtGeneralHint.Visibility = Visibility.Collapsed;
        }
    }

    private void TabGeneral_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(0);
    }

    private void TabAi_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(1);
    }

    #region General & GPU Rendering

    private void LoadGeneralData()
    {
        // 优先查看当前进程 RenderOptions 的实际运行模式
        bool isCurrentlyHardware = RenderOptions.ProcessRenderMode != RenderMode.SoftwareOnly;
        ChkEnableGpu.IsChecked = _appSettings.EnableGpuAcceleration;

        UpdateGpuLabels(ChkEnableGpu.IsChecked == true);

        int tier = (RenderCapability.Tier >> 16);
        TxtGpuTier.Text = tier switch
        {
            2 => "Tier 2 (完全硬件加速 - 支持 Direct3D 9Ex 硬件着色)",
            1 => "Tier 1 (部分硬件加速)",
            _ => "Tier 0 (软件光栅化 - 显卡无 Direct3D 硬件加速)"
        };
    }

    private void ChkEnableGpu_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ChkEnableGpu.IsChecked == true;
        UpdateGpuLabels(enable);
    }

    private void UpdateGpuLabels(bool isHardwareEnabled)
    {
        if (isHardwareEnabled)
        {
            TxtCurrentRenderMode.Text = "⚡ Direct3D 硬件加速渲染 (已配置开启)";
            TxtCurrentRenderMode.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // #34D399
        }
        else
        {
            TxtCurrentRenderMode.Text = "🖥️ 纯 CPU 软件渲染模式 (SoftwareOnly 已配置关闭)";
            TxtCurrentRenderMode.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B
        }
    }

    private void BtnOpenAppData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = SettingsService.SettingsDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开目录失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCleanCrashLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            if (File.Exists(logPath))
            {
                var fileInfo = new FileInfo(logPath);
                long length = fileInfo.Length;
                File.Delete(logPath);
                MessageBox.Show($"已成功清理崩溃日志文件 (大小: {length} 字节)。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("当前未发现 crash.log 崩溃日志，程序运行状态良好！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清理日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region AI Configuration

    private void LoadAiData()
    {
        TxtApiKey.Text = _aiSettings.ApiKey ?? string.Empty;
        TxtApiBaseUrl.Text = _aiSettings.ApiBaseUrl ?? "https://api.deepseek.com/v1";
        TxtModelName.Text = _aiSettings.ModelName ?? "deepseek-chat";

        switch (_aiSettings.Provider)
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

        foreach (ComboBoxItem item in CmbPromptStyle.Items)
        {
            if (item.Content?.ToString()?.StartsWith(_aiSettings.PromptStyle ?? "") == true)
            {
                CmbPromptStyle.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in CmbLanguage.Items)
        {
            if (item.Content?.ToString() == _aiSettings.Language)
            {
                CmbLanguage.SelectedItem = item;
                break;
            }
        }

        int currentTimeout = _aiSettings.TimeoutSeconds > 0 ? _aiSettings.TimeoutSeconds : 180;
        foreach (ComboBoxItem item in CmbTimeout.Items)
        {
            if (item.Tag?.ToString() == currentTimeout.ToString())
            {
                CmbTimeout.SelectedItem = item;
                break;
            }
        }

        int currentMaxTokens = _aiSettings.MaxTokens > 0 ? _aiSettings.MaxTokens : 2048;
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
        var tempSettings = GetAiSettingsFromForm();

        BtnTest.IsEnabled = false;
        TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        TxtTestResult.Text = "⏳ 正在测试连接...";

        try
        {
            var (success, message) = await _aiService.TestConnectionAsync(tempSettings);
            if (success)
            {
                TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtTestResult.Text = "✓ " + message;
            }
            else
            {
                TxtTestResult.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
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

    private AiSettingsModel GetAiSettingsFromForm()
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

    #endregion

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 1. Save App/GPU Settings
            bool enableGpu = ChkEnableGpu.IsChecked == true;
            _appSettings.EnableGpuAcceleration = enableGpu;
            _settingsService.SaveSettings(_appSettings);
            _settingsService.ApplyRenderMode(enableGpu);

            // 2. Save AI Settings
            var updatedAi = GetAiSettingsFromForm();
            _aiSettings.Provider = updatedAi.Provider;
            _aiSettings.ApiBaseUrl = updatedAi.ApiBaseUrl;
            _aiSettings.ApiKey = updatedAi.ApiKey;
            _aiSettings.ModelName = updatedAi.ModelName;
            _aiSettings.PromptStyle = updatedAi.PromptStyle;
            _aiSettings.Language = updatedAi.Language;
            _aiSettings.TimeoutSeconds = updatedAi.TimeoutSeconds;
            _aiSettings.MaxTokens = updatedAi.MaxTokens;
            _aiService.SaveSettings(_aiSettings);

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
}
