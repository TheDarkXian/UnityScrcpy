#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;

public partial class GameView
{
    public void StartFFmpegVideoStreamming()
    {
        ConnectPhone();
        StartScrcpy();
        StartVideoServer();
        Thread.Sleep(500);
        // 检查 FFmpeg 文件
        if (!checkFFmpegExist())
        {
            debugLog.AppendLine("❌ FFmpeg 文件不存在");
            LogDebug();
            return;
        }
        CreateTexture();
        // 每帧 RGBA 大小
        frameBuffer = new byte[width * height * 4];
        if (debugMode)
        {
            debugLog.AppendLine($"✓ 帧缓冲区大小: {frameBuffer.Length} 字节 ({frameBuffer.Length / 1024.0 / 1024.0:F2} MB)");
        }
        startTime = DateTime.Now;
        #region  构建 FFmpeg 参数
        StringBuilder argsBuilder = new StringBuilder();

        // 添加协议白名单
        if (data.videoStreamUrl.StartsWith("tcp://") || data.videoStreamUrl.StartsWith("http://") || data.videoStreamUrl.StartsWith("https://"))
        {
            argsBuilder.Append("-protocol_whitelist file,udp,rtp,tcp,http,https ");
        }

        // 低延迟标志
        argsBuilder.Append("-flags low_delay ");

        // 增加分析时间和探测大小（帮助 FFmpeg 正确解析 H.264 流）
        // 使用更大的值来确保能解析出流信息，但不要太大避免延迟
        argsBuilder.Append("-analyzeduration 20000000 -probesize 20000000 ");

        // 减少缓冲，低延迟，不等待完整帧，允许不完整帧
        argsBuilder.Append("-fflags nobuffer+genpts+discardcorrupt+ignidx ");

        // 对于 H.264 裸流，可能需要额外的解码选项
        argsBuilder.Append("-err_detect ignore_err ");

        // 进一步减少延迟：设置线程数为1，减少解码延迟
        argsBuilder.Append("-threads 1 ");

        // 设置最大延迟（微秒），减少缓冲
        argsBuilder.Append("-max_delay 0 ");

        // 指定输入格式
        if (!string.IsNullOrEmpty(data.inputFormat.ToString()))
        {
            argsBuilder.AppendFormat("-f {0} ", data.inputFormat.ToString());
        }

        // TCP 监听模式
        string finalUrl = data.videoStreamUrl;
        // 输入源
        argsBuilder.AppendFormat("-i \"{0}\" ", finalUrl);
        // 明确映射视频流（如果流存在）
        argsBuilder.Append("-map 0:v:0 ");
        // 视频滤镜（包含尺寸设置，确保输出尺寸，使用快速算法）
        argsBuilder.AppendFormat("-vf scale={0}:{1}:force_original_aspect_ratio=disable:flags=fast_bilinear,setpts=0 ", width, height);
        // 输出格式（rawvideo 必须明确指定尺寸）
        // 使用 -video_size 或 -s 参数
        // 使用 -fps_mode passthrough 来避免帧同步问题，直接输出所有帧（替代已弃用的 -vsync）
        argsBuilder.AppendFormat("-f rawvideo -pix_fmt rgba -video_size {0}x{1} -fps_mode passthrough -", width, height);
        string args = argsBuilder.ToString();
        #endregion
        if (debugMode)
        {
            debugLog.AppendLine($"FFmpeg 参数: {args}");
        }
        // 启动 FFmpeg
        ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = data.ffmpegPath;
        ffmpegProcess.StartInfo.Arguments = args;
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.RedirectStandardOutput = true;
        ffmpegProcess.StartInfo.RedirectStandardError = true;
        ffmpegProcess.StartInfo.CreateNoWindow = true;

        try
        {
            ffmpegProcess.Start();
            if (debugMode)
            {
                debugLog.AppendLine($"✓ FFmpeg 进程已启动 (PID: {ffmpegProcess.Id})");
                LogDebug();
            }
            if (data.enableDebug)
            {
                // 启动错误输出读取线程
                errorThread = new Thread(ReadErrorOutput);
                errorThread.IsBackground = true;
                errorThread.Start();
                debugLog.AppendLine("✓ 错误输出读取线程已启动");
            }
            // 等待一下让 FFmpeg 初始化并连接到流
            Thread.Sleep(500);
            // 检查进程是否还在运行
            if (ffmpegProcess.HasExited)
            {
                debugLog.AppendLine($"❌ FFmpeg 进程在启动后立即退出");
                debugLog.AppendLine($"退出代码: {ffmpegProcess.ExitCode}");
                LogDebug();
                return;
            }
        }
        catch (Exception ex)
        {
            debugLog.AppendLine($"❌ 启动 FFmpeg 失败: {ex.Message}");
            debugLog.AppendLine($"堆栈: {ex.StackTrace}");
            LogDebug();
            return;
        }

        isRunning = true;
        hasReceivedFirstFrame = false; // 重置标志，等待第一帧数据
        frameCount = 0; // 重置帧计数
        // 异步读取帧
        readThread = new Thread(ReadFrames);
        readThread.IsBackground = true;
        readThread.Start();
        if (debugMode)
        {
            debugLog.AppendLine("✓ 读取线程已启动");
            debugLog.AppendLine("=== 初始化完成 ===");
            LogDebug();
        }

    }

    void ReadFrames()
    {
        if (debugMode)
        {
            debugLog.AppendLine("=== ReadFrames 线程开始 ===");
            LogDebug();
        }

        var stream = ffmpegProcess.StandardOutput.BaseStream;
        int consecutiveZeros = 0;
        DateTime lastFrameTime = DateTime.Now;
        DateTime lastLogTime = DateTime.Now;
        int zeroReadCount = 0;

        while (isRunning)
        {
            try
            {
                // 检查进程是否还在运行
                if (ffmpegProcess.HasExited)
                {
                    debugLog.AppendLine($"❌ FFmpeg 进程已退出");
                    debugLog.AppendLine($"退出代码: {ffmpegProcess.ExitCode}");
                    debugLog.AppendLine($"运行时间: {(DateTime.Now - startTime).TotalSeconds:F2} 秒");
                    debugLog.AppendLine($"总接收帧数: {frameCount}");
                    debugLog.AppendLine($"总接收字节: {totalBytesRead / 1024.0 / 1024.0:F2} MB");
                    LogDebug();
                    break;
                }

                int bytesRead = 0;
                DateTime readStartTime = DateTime.Now;
                int maxReadAttempts = 1000; // 最大读取尝试次数，避免无限循环
                int readAttempts = 0;

                // 使用更大的读取块来提高性能
                byte[] readBuffer = new byte[65536]; // 64KB 读取缓冲区

                while (bytesRead < frameBuffer.Length && readAttempts < maxReadAttempts)
                {
                    int toRead = Math.Min(readBuffer.Length, frameBuffer.Length - bytesRead);
                    int n = stream.Read(readBuffer, 0, toRead);

                    if (n == 0)
                    {
                        consecutiveZeros++;
                        zeroReadCount++;
                        readAttempts++;

                        if (consecutiveZeros > 10)
                        {
                            if ((DateTime.Now - lastLogTime).TotalSeconds > 5) // 每5秒输出一次警告
                            {
                                if (debugMode)
                                {
                                    debugLog.AppendLine($"⚠ 连续 {consecutiveZeros} 次读取到 0 字节");
                                    debugLog.AppendLine($"总零读取次数: {zeroReadCount}");
                                    debugLog.AppendLine($"已接收帧数: {frameCount}");
                                    debugLog.AppendLine($"进程状态: {(ffmpegProcess.HasExited ? "已退出" : "运行中")}");
                                    LogDebug();
                                }

                                lastLogTime = DateTime.Now;
                            }
                            Thread.Sleep(1); // 减少等待时间，提高响应速度
                        }

                        if (debugMode && readAttempts >= maxReadAttempts)
                        {
                            debugLog.AppendLine($"⚠ 达到最大读取尝试次数，可能流已结束");
                            LogDebug();
                        }
                        break; // 流结束
                    }

                    consecutiveZeros = 0;
                    readAttempts = 0;

                    // 复制到帧缓冲区
                    Buffer.BlockCopy(readBuffer, 0, frameBuffer, bytesRead, n);
                    bytesRead += n;
                    totalBytesRead += n;
                }

                if (debugMode && bytesRead > 0 && bytesRead < frameBuffer.Length)
                {
                    debugLog.AppendLine($"⚠ 读取的数据不完整: {bytesRead}/{frameBuffer.Length} 字节 ({bytesRead * 100.0 / frameBuffer.Length:F1}%)");
                    LogDebug();
                }

                if (bytesRead == frameBuffer.Length)
                {
                    frameCount++;
                    // 标记已接收到第一帧数据
                    if (!hasReceivedFirstFrame)
                    {
                        hasReceivedFirstFrame = true;
                        if (debugMode)
                        {
                            debugLog.AppendLine("=== 第一帧数据接收完成，开始显示视频流 ===");
                            debugLog.AppendLine($"第一帧数据大小: {frameBuffer.Length / 1024.0 / 1024.0:F2} MB");
                            LogDebug();
                        }
                    }
                    var frameTime = DateTime.Now;
                    var timeSinceLastFrame = (frameTime - lastFrameTime).TotalMilliseconds;
                    lastFrameTime = frameTime;

                    // 调试 frameBuffer 内容（前几帧和每100帧）
                    bool shouldDebugBuffer = frameCount <= 5 || frameCount % 100 == 0;
                    if (debugMode && shouldDebugBuffer)
                    {
                        // 检查前几个字节和后几个字节
                        int sampleSize = Math.Min(32, frameBuffer.Length);
                        debugLog.AppendLine($"=== FrameBuffer 调试 (帧 #{frameCount}) ===");
                        debugLog.AppendLine($"缓冲区大小: {frameBuffer.Length} 字节");
                        debugLog.AppendLine($"前 {sampleSize} 字节: {BitConverter.ToString(frameBuffer, 0, sampleSize)}");
                        debugLog.AppendLine($"后 {sampleSize} 字节: {BitConverter.ToString(frameBuffer, frameBuffer.Length - sampleSize, sampleSize)}");

                        // 计算一些统计信息
                        int nonZeroCount = 0;
                        int maxValue = 0;
                        int minValue = 255;
                        for (int i = 0; i < Math.Min(1000, frameBuffer.Length); i++)
                        {
                            byte val = frameBuffer[i];
                            if (val != 0)
                                nonZeroCount++;
                            if (val > maxValue)
                                maxValue = val;
                            if (val < minValue)
                                minValue = val;
                        }
                        debugLog.AppendLine($"前1000字节统计: 非零={nonZeroCount}, 最大值={maxValue}, 最小值={minValue}");

                        // 检查是否有明显的图像数据模式（RGBA 应该有一些规律）
                        // 检查前4个像素（16字节）是否合理
                        bool hasValidPattern = true;
                        for (int i = 0; i < 16 && i < frameBuffer.Length; i += 4)
                        {
                            byte r = frameBuffer[i];
                            byte g = frameBuffer[i + 1];
                            byte b = frameBuffer[i + 2];
                            byte a = frameBuffer[i + 3];

                            // 如果所有值都是0或255，可能是无效数据
                            if ((r == 0 && g == 0 && b == 0 && a == 0) ||
                                (r == 255 && g == 255 && b == 255 && a == 255))
                            {
                                // 可能是纯色，继续检查
                            }
                        }
                        debugLog.AppendLine($"数据模式检查: {(hasValidPattern ? "正常" : "异常")}");
                    }

                    // 每30帧或每2秒输出一次统计
                    if (debugMode && (frameCount % 30 == 0 || (frameTime - lastLogTime).TotalSeconds >= 2))
                    {
                        var elapsed = (frameTime - startTime).TotalSeconds;
                        var fps = frameCount / elapsed;
                        debugLog.AppendLine($"=== 统计信息 ===");
                        debugLog.AppendLine($"帧数: {frameCount}");
                        // 修复整数溢出问题
                        double totalMB = (double)totalBytesRead / 1024.0 / 1024.0;
                        debugLog.AppendLine($"总字节: {totalMB:F2} MB");
                        debugLog.AppendLine($"帧间隔: {timeSinceLastFrame:F2} ms");
                        debugLog.AppendLine($"平均 FPS: {fps:F2}");
                        debugLog.AppendLine($"运行时间: {elapsed:F2} 秒");
                        debugLog.AppendLine($"最后读取耗时: {(DateTime.Now - readStartTime).TotalMilliseconds:F2} ms");
                        LogDebug();
                        lastLogTime = frameTime;
                    }

                    if (debugMode && shouldDebugBuffer)
                    {
                        LogDebug();
                    }

                    // 把数据传给主线程渲染
                    if (_queue != null)
                    {
                        // 性能优化：如果队列中已有太多待处理任务，跳过本次更新，避免积压
                        int queueCount = 0;
                        lock (_queue)
                        {
                            queueCount = _queue.Count;
                        }

                        // 如果队列积压超过限制，跳过本次更新（使用最新数据）
                        if (queueCount < MAX_QUEUE_ITEMS_PER_FRAME * 3)
                        {
                            // 快速复制到翻转缓冲区（在读取线程中完成，减少主线程负担）
                            // 使用缓存的尺寸，避免在主线程外访问 Unity 对象
                            for (int y = 0; y < cachedTextureHeight; y++)
                            {
                                int srcRow = y * cachedRowSize;
                                int dstRow = (cachedTextureHeight - 1 - y) * cachedRowSize;
                                Buffer.BlockCopy(frameBuffer, srcRow, flippedDataBuffer, dstRow, cachedRowSize);
                            }

                            // 将已翻转的数据传递给主线程（避免主线程再次处理）
                            byte[] dataToUpload = flippedDataBuffer; // 直接引用，因为已经翻转完成

                            _queue.Enqueue(() =>
                            {
                                try
                                {
                                    if (tempTexture2D != null)
                                    {
                                        // 直接加载已翻转的数据（翻转已在读取线程完成）
                                        tempTexture2D.LoadRawTextureData(dataToUpload);
                                        tempTexture2D.Apply(false); // false = 异步上传，减少延迟
                                    }
                                    else
                                    {
                                        if (debugMode && frameCount % 30 == 0)
                                        {
                                            debugLog.AppendLine($"⚠ 纹理或数据大小不匹配");
                                            debugLog.AppendLine($"TempTexture2D: null");
                                            debugLog.AppendLine($"数据: {frameBuffer.Length} 字节");
                                            LogDebug();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (debugMode)
                                    {
                                        debugLog.AppendLine($"❌ 更新 RenderTexture 失败: {ex.Message}");
                                        debugLog.AppendLine($"堆栈: {ex.StackTrace}");
                                        LogDebug();
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        if (frameCount % 30 == 0) // 每30帧输出一次警告
                        {
                            debugLog.AppendLine("⚠ UnityMainThreadDispatcher 实例不存在，无法更新纹理");
                            LogDebug();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                debugLog.AppendLine($"❌ ReadFrames 异常: {ex.Message}");
                debugLog.AppendLine($"堆栈: {ex.StackTrace}");
                debugLog.AppendLine($"已接收帧数: {frameCount}");
                debugLog.AppendLine($"总接收字节: {totalBytesRead / 1024.0 / 1024.0:F2} MB");
                LogDebug();
                Thread.Sleep(100);
            }
        }

        debugLog.AppendLine("=== ReadFrames 线程结束 ===");
        debugLog.AppendLine($"总接收帧数: {frameCount}");
        debugLog.AppendLine($"总接收字节: {totalBytesRead / 1024.0 / 1024.0:F2} MB");
        debugLog.AppendLine($"运行时间: {(DateTime.Now - startTime).TotalSeconds:F2} 秒");
        LogDebug();
    }

    void ReadErrorOutput()
    {
        debugLog.AppendLine("=== 错误输出读取线程开始 ===");
        LogDebug();

        var errorStream = ffmpegProcess.StandardError;
        StringBuilder errorBuffer = new StringBuilder();
        string line;
        int lineCount = 0;

        try
        {
            while ((line = errorStream.ReadLine()) != null && isRunning)
            {
                lineCount++;
                errorBuffer.AppendLine($"[FFmpeg] {line}");

                // 每10行或遇到错误关键词时输出一次
                if (lineCount % 10 == 0 ||
                    line.Contains("Error") ||
                    line.Contains("error") ||
                    line.Contains("failed") ||
                    line.Contains("Invalid"))
                {
                    debugLog.Append(errorBuffer.ToString());
                    LogDebug();
                    errorBuffer.Clear();
                }
            }

            // 输出剩余内容
            if (errorBuffer.Length > 0)
            {
                debugLog.Append(errorBuffer.ToString());
                LogDebug();
            }
        }
        catch (Exception ex)
        {
            debugLog.AppendLine($"❌ 读取错误输出异常: {ex.Message}");
            debugLog.AppendLine($"堆栈: {ex.StackTrace}");
            LogDebug();
        }

        debugLog.AppendLine($"=== 错误输出读取线程结束 (共 {lineCount} 行) ===");
        LogDebug();
    }
}
#endif

