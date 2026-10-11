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
