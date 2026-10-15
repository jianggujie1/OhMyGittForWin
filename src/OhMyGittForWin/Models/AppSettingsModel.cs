namespace OhMyGittForWin.Models;

/// <summary>
/// 全局应用程序设置模型
/// </summary>
public class AppSettingsModel
{
    /// <summary>
    /// 是否启用 GPU 硬件加速 (Direct3D 渲染)。
    /// 默认 true。关闭后强制使用纯 CPU 软件渲染 (SoftwareOnly)，适合虚拟机/远程桌面或显卡驱动异常环境。
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
}
