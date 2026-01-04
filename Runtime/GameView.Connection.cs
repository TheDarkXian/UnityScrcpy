#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public partial class GameView
{
    bool checkFFmpegExist()
    {
        return File.Exists(data.ffmpegPath);
    }

    public void ConnectPhone()
    {
        if (data == null)
        {
            return;
        }
        if (string.IsNullOrEmpty(phoneIp) || string.IsNullOrEmpty(phonePort))
        {
            return;
        }
        ConnectPhone(phoneIp, phonePort);
    }

    void ConnectPhone(string ip, string port)
    {
        if (data == null)
        {
            Debug.LogError("请先激活一个配置（SetData）。");
            return;
        }

        if (string.IsNullOrEmpty(data.abdPath) || !File.Exists(data.abdPath))
        {
            debugLog.AppendLine("❌ ADB 路径无效，请在配置中正确设置。");
            LogDebug();
            return;
        }

        // 先查询adb列表里有没有这个手机，如果有就跳过，没有就连接
        string targetDevice = $"{ip}:{port}";
        string devicesOutput = ExecuteAdbCommandWithOutput("devices");

        if (!string.IsNullOrEmpty(devicesOutput))
        {
            // 解析设备列表，检查目标设备是否已连接
            string[] lines = devicesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool deviceExists = false;

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
                            deviceExists = true;
                            if (debugMode)
                            {
                                debugLog.AppendLine($"✓ 设备已连接: {targetDevice} (状态: {status})");
                                LogDebug();
                            }
                            break;
                        }
                    }
                }
            }

            if (deviceExists)
            {
                // 设备已存在，跳过连接
                UpdateResolutionFromDevice();
                return;
            }
        }

        //使用adb命令连接上手机
        if (adbHelper == null)
        {
            debugLog.AppendLine("❌ AdbHelper 未初始化");
            LogDebug();
            return;
        }

        bool isSuccess = adbHelper.ConnectDevice(ip, port);
        if (isSuccess)
        {
            debugLog.AppendLine($"✓ 连接成功: {ip}:{port}");
            // 成功后尝试获取设备分辨率
            UpdateResolutionFromDevice();
            LogDebug();
        }
        else
        {
            debugLog.AppendLine($"❌ 连接失败: {ip}:{port}");
            LogDebug();
        }
    }

    public void StartScrcpy()
    {
        if (data == null)
        {
            return;
        }
        if (string.IsNullOrEmpty(data.phoneIp) || string.IsNullOrEmpty(data.phonePort))
        {
            return;
        }
        StartScrcpy(data.phoneIp, data.phonePort, data.enableScrcpyWindow);

    }
    public void StartScrcpy(string ip, string port, bool window = false)
    {
        if (data == null)
        {
            Debug.LogError("请先激活一个配置（SetData）。");
            return;
        }

        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
        {
            debugLog.AppendLine("❌ IP 或端口不能为空");
            LogDebug();
            return;
        }

        if (string.IsNullOrEmpty(data.scrcpyPath) || !File.Exists(data.scrcpyPath))
        {
            debugLog.AppendLine("❌ scrcpy 路径无效，请在配置中正确设置。");
            LogDebug();
            return;
        }

        // 如果已有进程在运行，先停止它
        if (scrcpyClientProcess != null)
        {
            try
            {
                if (!scrcpyClientProcess.HasExited)
                {
                    scrcpyClientProcess.Kill();
                }
            }
            catch { }
            finally
            {
                scrcpyClientProcess.Dispose();
                scrcpyClientProcess = null;
            }
        }
        string windowCommand = "--no-window";
        if (window)
        {
            windowCommand = "";
        }
        // 构建 scrcpy 命令参数
        string arguments = $"-s {ip}:{port} {windowCommand}";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = data.scrcpyPath,
                Arguments = arguments,
            };

            // 如果要显示窗口，使用 ShellExecute 并允许创建窗口
            if (window)
            {
                psi.UseShellExecute = true;
                psi.CreateNoWindow = false;
                // 显示窗口时不能重定向输出，否则窗口可能不显示
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;
            }
            else
            {
                // 不显示窗口时，可以重定向输出用于调试
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = debugMode;
                psi.RedirectStandardError = debugMode;
            }

            scrcpyClientProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // 只有在不显示窗口且启用调试时，才设置输出重定向事件
            if (!window && debugMode)
            {
                scrcpyClientProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (debugLog)
                        {
                            debugLog.AppendLine($"[Scrcpy Client] {e.Data}");
                        }
                    }
                };
                scrcpyClientProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (debugLog)
                        {
                            debugLog.AppendLine($"[Scrcpy Client Error] {e.Data}");
                        }
                    }
                };
            }

            scrcpyClientProcess.Start();

            // 只有在不显示窗口且启用调试时，才开始异步读取输出
            if (!window && debugMode)
            {
                scrcpyClientProcess.BeginOutputReadLine();
                scrcpyClientProcess.BeginErrorReadLine();
            }

            if (debugMode)
            {
                debugLog.AppendLine($"✓ scrcpy 客户端已启动 (IP: {ip}, 端口: {port}, PID: {scrcpyClientProcess.Id}, 显示窗口: {window})");
            }
        }
        catch (Exception ex)
        {
            debugLog.AppendLine($"❌ 无法启动 scrcpy 客户端: {ex.Message}");
            debugLog.AppendLine($"堆栈: {ex.StackTrace}");
            scrcpyClientProcess = null;
        }

        LogDebug();
    }

    public void StartVideoServer()
    {
        if (data == null)
        {
            Debug.LogError("请先激活一个配置（SetData）。");
            return;
        }

        if (string.IsNullOrEmpty(data.abdPath) || !File.Exists(data.abdPath))
        {
            debugLog.AppendLine("❌ ADB 路径无效，请在配置中正确设置。");
            LogDebug();
            return;
        }

        if (string.IsNullOrEmpty(data.scrcpyPath) || !File.Exists(data.scrcpyPath))
        {
            debugLog.AppendLine("❌ scrcpy 路径无效，请在配置中正确设置。");
            LogDebug();
            return;
        }

        var scrcpyDirectory = Path.GetDirectoryName(data.scrcpyPath);
        if (string.IsNullOrEmpty(scrcpyDirectory) || !Directory.Exists(scrcpyDirectory))
        {
            debugLog.AppendLine("❌ 无法获取 scrcpy 所在目录。");
            LogDebug();
            return;
        }

        string localServerPath = Path.Combine(scrcpyDirectory, "scrcpy-server");
        if (!File.Exists(localServerPath))
        {
            localServerPath = Path.Combine(scrcpyDirectory, "scrcpy-server.jar");
        }

        if (!File.Exists(localServerPath))
        {
            debugLog.AppendLine("❌ 未在 scrcpy 目录中找到 scrcpy-server 文件。");
            LogDebug();
            return;
        }

        const string deviceServerPath = "/data/local/tmp/scrcpy-server-manual.jar";

        // 1. 将 scrcpy-server 推送到手机
        if (!ExecuteAdbCommand($"push \"{localServerPath}\" {deviceServerPath}"))
        {
            return;
        }

        // 2. 端口转发
        if (!ExecuteAdbCommand($"forward tcp:{data.videoPort} localabstract:scrcpy"))
        {
            return;
        }

        // 3. 启动 scrcpy server
        if (scrcpyServerProcess != null)
        {
            try
            {
                if (!scrcpyServerProcess.HasExited)
                {
                    scrcpyServerProcess.Kill();
                }
            }
            catch { }
            finally
            {
                scrcpyServerProcess.Dispose();
                scrcpyServerProcess = null;
            }
        }
        scrcpyServerProcess = StartAdbPersistentProcess(
            $"shell CLASSPATH={deviceServerPath} app_process / com.genymobile.scrcpy.Server 3.3.3 tunnel_forward=true audio=false control=false cleanup=false raw_stream=true video_bit_rate=0 max_size=1920");

        if (scrcpyServerProcess == null)
        {
            debugLog.AppendLine("❌ scrcpy server 启动失败。");
        }
        else if (debugMode)
        {
            debugLog.AppendLine($"✓ scrcpy server 已启动 (端口: {data.videoPort})");
        }

        LogDebug();
    }

    public bool ExecuteAdbCommand(string arguments)
    {
        if (adbHelper == null)
        {
            debugLog.AppendLine("❌ AdbHelper 未初始化");
            LogDebug();
            return false;
        }
        return adbHelper.ExecuteCommand(arguments);
    }

    public string ExecuteAdbCommandWithOutput(string arguments)
    {
        if (adbHelper == null)
        {
            debugLog.AppendLine("❌ AdbHelper 未初始化");
            LogDebug();
            return string.Empty;
        }
        return adbHelper.ExecuteCommandWithOutput(arguments);
    }

    Process StartAdbPersistentProcess(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = data.abdPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = debugMode,
                RedirectStandardError = debugMode,
                CreateNoWindow = true,
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (debugMode)
            {
                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (debugLog)
                        {
                            debugLog.AppendLine($"[ADB] {e.Data}");
                        }
                    }
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (debugLog)
                        {
                            debugLog.AppendLine($"[ADB] {e.Data}");
                        }
                    }
                };
            }

            proc.Start();
            if (debugMode)
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            return proc;
        }
        catch (Exception ex)
        {
            debugLog.AppendLine($"❌ 无法启动 adb 进程: {arguments}");
            debugLog.AppendLine(ex.ToString());
            LogDebug();
            return null;
        }
    }
}
#endif

