using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace OhMyGittForWin;

public partial class App : Application
{
    private bool _isHandlingException;

    protected override void OnStartup(StartupEventArgs e)
    {
        var settingsService = new Services.SettingsService();
        var appSettings = settingsService.LoadSettings();

        // 检查命令行兜底参数 (例如: --disable-gpu 或 --software-rendering 或 --enable-gpu)
        bool? commandLineGpuOverride = null;
        if (e.Args != null)
        {
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--disable-gpu", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-no-gpu", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--software-rendering", StringComparison.OrdinalIgnoreCase))
                {
                    commandLineGpuOverride = false;
                }
                else if (arg.Equals("--enable-gpu", StringComparison.OrdinalIgnoreCase))
                {
                    commandLineGpuOverride = true;
                }
            }
        }

        bool enableGpu = commandLineGpuOverride ?? appSettings.EnableGpuAcceleration;
        Services.SettingsService.ApplyGlobalRenderMode(enableGpu);

        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        if (!_isHandlingException)
        {
            _isHandlingException = true;
            MessageBox.Show($"程序发生异常:\n{e.Exception.Message}\n\n详情已写入 crash.log", "OhMyGit 异常提示", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
            if (!_isHandlingException)
            {
                _isHandlingException = true;
                MessageBox.Show($"致命异常:\n{ex.Message}\n\n详情已写入 crash.log", "OhMyGit 致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.WriteAllText(logPath, DateTime.Now + "\n" + ex.ToString());

            string scratchLog = @"C:\Users\jiang\.gemini\antigravity\brain\f2341b56-b3e4-49b0-b559-359f34413741\scratch\crash.log";
            File.WriteAllText(scratchLog, DateTime.Now + "\n" + ex.ToString());
        }
        catch { }
    }
}
