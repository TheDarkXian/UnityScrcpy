#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// ADB 命令助手类，提供所有 ADB 相关功能
/// </summary>
public class AdbHelper
{
    private string adbPath;
    private bool debugMode;
    private Action<string> onLog;

    /// <summary>
    /// 应用信息
    /// </summary>
    [System.Serializable]
    public class AppInfo
    {
        public string packageName;
        public string appName;

        public AppInfo(string packageName, string appName = "")
        {
            this.packageName = packageName;
            this.appName = string.IsNullOrEmpty(appName) ? packageName : appName;
        }
    }

    public AdbHelper(string adbPath, bool debugMode = false, Action<string> onLog = null)
    {
        this.adbPath = adbPath;
        this.debugMode = debugMode;
        this.onLog = onLog;
    }

    #region 基础 ADB 命令

    /// <summary>
    /// 执行 ADB 命令
    /// </summary>
    public bool ExecuteCommand(string arguments)
    {
        if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
        {
            LogError("ADB 路径无效，请在配置中正确设置。");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var proc = Process.Start(psi))
            {
                var output = proc.StandardOutput.ReadToEnd();
                var error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (debugMode)
                {
                    if (!string.IsNullOrWhiteSpace(output))
                        Log($"[ADB Output] {output}");
                    if (!string.IsNullOrWhiteSpace(error))
                        Log($"[ADB Error] {error}");
                }

                if (proc.ExitCode != 0)
                {
                    LogError($"❌ ADB 命令失败: adb {arguments} (exit {proc.ExitCode})");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"❌ ADB 命令异常: adb {arguments}\n{ex}");
            return false;
        }
    }

    /// <summary>
    /// 执行 ADB 命令并返回输出
    /// </summary>
    public string ExecuteCommandWithOutput(string arguments)
    {
        if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
        {
            LogError("ADB 路径无效，请在配置中正确设置。");
            return string.Empty;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var proc = Process.Start(psi))
            {
                var output = proc.StandardOutput.ReadToEnd();
                var error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (debugMode)
                {
                    if (!string.IsNullOrWhiteSpace(output))
                        Log($"[ADB Output] {output}");
                    if (!string.IsNullOrWhiteSpace(error))
                        Log($"[ADB Error] {error}");
                }

                if (proc.ExitCode != 0)
                {
                    if (debugMode)
                    {
                        LogError($"❌ ADB 命令失败: adb {arguments} (exit {proc.ExitCode})");
                    }
                    return string.Empty;
                }

                return output;
            }
        }
        catch (Exception ex)
        {
            if (debugMode)
            {
                LogError($"❌ ADB 命令异常: adb {arguments}\n{ex}");
            }
            return string.Empty;
        }
    }

    #endregion

    #region 触摸操作

    /// <summary>
    /// 点击屏幕坐标
    /// </summary>
    public void Click(int x, int y)
    {
        ExecuteCommand($"shell input tap {x} {y}");
    }

    /// <summary>
    /// 滑动
    /// </summary>
    public void Swipe(int x1, int y1, int x2, int y2, int durationMs = 200)
    {
        ExecuteCommand($"shell input swipe {x1} {y1} {x2} {y2} {durationMs}");
    }

    /// <summary>
    /// 长按
    /// </summary>
    public void LongPress(int x, int y, int durationMs = 500)
    {
        ExecuteCommand($"shell input swipe {x} {y} {x} {y} {durationMs}");
    }

    #endregion

    #region 按键操作

    /// <summary>
    /// 发送按键事件
    /// </summary>
    public void KeyEvent(int keyCode)
    {
        ExecuteCommand($"shell input keyevent {keyCode}");
    }

    public void Back() => KeyEvent(4);
    public void Home() => KeyEvent(3);
    public void RecentApps() => KeyEvent(187);
    public void Power() => KeyEvent(26);
    public void WakeScreen() => KeyEvent(224);
    public void SleepScreen() => KeyEvent(223);
    public void VolumeUp() => KeyEvent(24);
    public void VolumeDown() => KeyEvent(25);
    public void DPadUp() => KeyEvent(19);
    public void DPadDown() => KeyEvent(20);
    public void DPadLeft() => KeyEvent(21);
    public void DPadRight() => KeyEvent(22);
    public void DPadCenter() => KeyEvent(23);
    public void PlayPause() => KeyEvent(85);
    public void StopMedia() => KeyEvent(86);
    public void NextTrack() => KeyEvent(87);
    public void PrevTrack() => KeyEvent(88);
    public void FastForward() => KeyEvent(90);
    public void Rewind() => KeyEvent(89);
    public void Screenshot() => KeyEvent(120);
    public void PasteClipboard() => KeyEvent(279);

    #endregion

    #region 文本输入

    /// <summary>
    /// 输入文本（会转义空格）
    /// </summary>
    public void InputText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var escaped = text.Replace(" ", "%s");
        ExecuteCommand($"shell input text \"{escaped}\"");
    }

    /// <summary>
    /// 清空输入框（发送多个删除键）
    /// </summary>
    public void ClearInputField()
    {
        // 发送多个删除键来清空输入框（通常 50 次足够清空大部分输入框）
        for (int i = 0; i < 50; i++)
        {
            KeyEvent(67); // KEYCODE_DEL = 67
        }
    }

    #endregion

    #region 应用管理

    /// <summary>
    /// 启动应用（通过包名）
    /// </summary>
    public void LaunchApp(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return;
        ExecuteCommand($"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
    }

    /// <summary>
    /// 强制停止应用（退出当前应用）
    /// </summary>
    public void ForceStopApp(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return;
        ExecuteCommand($"shell am force-stop {packageName}");
    }

    /// <summary>
    /// 卸载应用（通过包名）
    /// </summary>
    public void UninstallApp(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return;
        ExecuteCommand($"uninstall {packageName}");
    }

    /// <summary>
    /// 获取设备上安装的应用列表（仅第三方应用）
    /// </summary>
    public List<AppInfo> GetInstalledApps()
    {
        List<AppInfo> apps = new List<AppInfo>();

        try
        {
            // 获取所有第三方应用的包名
            string output = ExecuteCommandWithOutput("shell pm list packages -3");

            if (string.IsNullOrEmpty(output))
                return apps;

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                // 格式: package:com.example.app
                if (line.StartsWith("package:"))
                {
                    string packageName = line.Substring(8).Trim();
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        // 尝试获取应用名称
                        string appName = GetAppName(packageName);
                        apps.Add(new AppInfo(packageName, appName));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"获取应用列表失败: {ex.Message}");
        }

        return apps.OrderBy(a => a.appName).ToList();
    }

    /// <summary>
    /// 获取应用名称
    /// </summary>
    private string GetAppName(string packageName)
    {
        try
        {
            // 使用 dumpsys 获取应用信息
            string output = ExecuteCommandWithOutput($"shell dumpsys package {packageName}");

            if (string.IsNullOrEmpty(output))
                return packageName;

            // 查找应用名称
            // 格式通常是: applicationLabel='应用名称' 或 applicationLabel=应用名称
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("applicationLabel="))
                {
                    int startIndex = line.IndexOf("applicationLabel=");
                    if (startIndex >= 0)
                    {
                        string labelPart = line.Substring(startIndex + "applicationLabel=".Length);
                        // 移除引号
                        labelPart = labelPart.Trim().Trim('\'', '"');
                        if (!string.IsNullOrEmpty(labelPart))
                        {
                            return labelPart;
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略错误，返回包名
        }

        return packageName;
    }

    #endregion

    #region 系统功能

    /// <summary>
    /// 展开通知栏
    /// </summary>
    public void ExpandNotifications()
    {
        ExecuteCommand("shell cmd statusbar expand-notifications");
    }

    /// <summary>
    /// 收起通知栏
    /// </summary>
    public void CollapseNotifications()
    {
        ExecuteCommand("shell cmd statusbar collapse");
    }

    /// <summary>
    /// 展开快速设置
    /// </summary>
    public void ExpandQuickSettings()
    {
        ExecuteCommand("shell cmd statusbar expand-settings");
    }

    #endregion

    #region 设备连接

    /// <summary>
    /// 连接设备
    /// </summary>
    public bool ConnectDevice(string ip, string port)
    {
        string targetDevice = $"{ip}:{port}";
        string devicesOutput = ExecuteCommandWithOutput("devices");

        if (!string.IsNullOrEmpty(devicesOutput))
        {
            // 解析设备列表，检查目标设备是否已连接
            string[] lines = devicesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                // 跳过标题行 "List of devices attached"
                if (line.Contains("List of devices") || string.IsNullOrWhiteSpace(line))
                    continue;

                // ADB devices 输出格式: "设备ID    状态"
                string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    string deviceId = parts[0];
                    // 检查设备ID是否匹配（可能是完整IP:端口，或者部分匹配）
                    if (deviceId == targetDevice || deviceId.Contains(ip))
                    {
                        string status = parts.Length > 1 ? parts[1] : "unknown";
                        if (status == "device")
                        {
                            // 设备已存在，跳过连接
                            return true;
                        }
                    }
                }
            }
        }

        // 使用adb命令连接上手机
        return ExecuteCommand($"connect {ip}:{port}");
    }

    #endregion

    #region 日志

    private void Log(string message)
    {
        if (onLog != null)
        {
            onLog(message);
        }
        else if (debugMode)
        {
            Debug.Log(message);
        }
    }

    private void LogError(string message)
    {
        if (onLog != null)
        {
            onLog(message);
        }
        else
        {
            Debug.LogError(message);
        }
    }

    #endregion
}
#endif

