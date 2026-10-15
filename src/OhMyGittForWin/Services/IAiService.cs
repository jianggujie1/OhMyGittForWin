using System.Threading.Tasks;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public interface IAiService
{
    AiSettingsModel LoadSettings();
    void SaveSettings(AiSettingsModel settings);
    Task<string> GenerateCommitMessageAsync(string diffContent, AiSettingsModel? settings = null);
    Task<(bool Success, string Message)> TestConnectionAsync(AiSettingsModel settings);
}
