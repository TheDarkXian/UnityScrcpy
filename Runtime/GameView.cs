#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine.UI;
using Sirenix.Utilities.Editor;       // Extension 方法
using Sirenix.Utilities;
using Unity.VisualScripting;
using System.Text.RegularExpressions;
public partial class GameView : OdinEditorWindow
{
    protected override void OnEnable()
    {
        base.OnEnable();
        wantsMouseMove = true;
        wantsMouseEnterLeaveWindow = true;


    }
    private Texture2D tempTexture2D; // 临时 Texture2D 用于加载数据
    private byte[] flippedDataBuffer; // 预分配的翻转缓冲区，避免每帧分配
    private RenderTexture cameraRenderTexture; // 用于捕获主相机画面的临时 RenderTexture

    // 缓存的纹理尺寸（在主线程中设置，读取线程中使用）
    private int cachedTextureWidth;
    private int cachedTextureHeight;
    private int cachedRowSize; // 每行字节数
    private Process ffmpegProcess;
    private Process scrcpyServerProcess;
    private Process scrcpyClientProcess;
    private byte[] frameBuffer;
    private Thread readThread;
    private Thread errorThread;
    private bool isRunning = false;
    private volatile bool hasReceivedFirstFrame = false; // 是否已接收到第一帧数据（volatile 确保多线程访问安全）

    /// <summary>
    /// 是否正在运行且有画面
    /// </summary>
    public bool IsRunningWithFrame => isRunning && hasReceivedFirstFrame;
    private int frameCount = 0;
    private int totalBytesRead = 0;
    private DateTime startTime;
    private string phoneIp;
    private string phonePort;
    private bool debugMode;
    private readonly StringBuilder debugLog = new StringBuilder();
    private readonly object logLock = new object();
    [HideInInspector]
    public AdbHelper adbHelper;
    private static readonly Queue<Action> _queue = new Queue<Action>();
    private static GUIContent deviceSimulatorIcon;
    private static bool deviceSimulatorIconChecked;
    private Color backgroundColor;
    private Color deviceFrameColor;
    private Color screenBackgroundColor;
    int width = 1080;
    int height = 2340;
    private bool enableFPS = false; // 是否显示帧率

    // 性能优化：缓存 Canvas 数组，避免每帧查找
    private Canvas[] cachedCanvases;
    private RenderMode[] cachedOriginalRenderModes;
    private Camera[] cachedOriginalCameras;
    private DateTime lastCanvasCacheTime = DateTime.MinValue;
    private const double CANVAS_CACHE_INTERVAL = 1.0; // 每秒更新一次 Canvas 缓存

    // 性能优化：限制 Repaint 频率
    private DateTime lastRepaintTime = DateTime.MinValue;
    private const double REPAINT_INTERVAL = 0.033; // 约 30 FPS (33ms)

    // 流健康检测
    private DateTime lastFrameReceivedTime = DateTime.MinValue;
    private const double STREAM_TIMEOUT = 5.0; // 5秒没有新帧认为流可能停止

    // 性能优化：限制队列处理数量，避免单帧卡顿
    private const int MAX_QUEUE_ITEMS_PER_FRAME = 5;

    // 性能优化：缓存布局计算结果
    private float cachedViewWidth = -1;
    private float cachedViewHeight = -1;
    private Rect cachedDeviceRect;
    private Rect cachedScreenRect;
    private Rect cachedDrawRect;

    // 帧率显示相关
    private float currentFPS = 0f;
    private int frameCounter = 0;
    private DateTime lastFPSCalculationTime = DateTime.Now;
    private const double FPS_UPDATE_INTERVAL = 0.5; // 每0.5秒更新一次帧率显示
    [MenuItem("Window/Custom/Game View")]
    private static void OpenWindow()
    {
        var window = GetWindow<GameView>("Game View");
        window.Show();
        //寻找一个FakeGameViewWindow实例
        if (window.data == null)
        {

            var fakeGameViewWindow = FindObjectsOfType<FakeGameViewWindow>();
            if (fakeGameViewWindow.Length > 0)
            {
                window.data = fakeGameViewWindow[0].fakeGameViewData;
            }

        }
        if (window.data == null)
        {
            //寻找一个数据 
            //从rescours下面找这个数据文件，如果存在则加载
            var data = Resources.Load<FakeGameViewData>("DefaultSetting");
            if (data != null)
            {
                window.data = data;
            }
        }
    }
    [HideInInspector]
    public FakeGameViewData data;
    public void SetData(FakeGameViewData fakeGameViewData)
    {
        this.data = fakeGameViewData;
        phoneIp = data.phoneIp;
        phonePort = data.phonePort;
        width = data.resolution.x;
        height = data.resolution.y;
        debugMode = this.data.enableDebug;
        backgroundColor = this.data.backgroundColor;
        deviceFrameColor = this.data.deviceFrameColor;
        screenBackgroundColor = this.data.screenBackgroundColor;
        enableFPS = this.data.enableFPS;

        // 初始化 AdbHelper
        adbHelper = new AdbHelper(data.abdPath, debugMode, (msg) =>
        {
            lock (logLock)
            {
                debugLog.AppendLine(msg);
            }
        });
    }

    /// <summary>
    /// 连接后尝试从设备获取实际分辨率，更新 width/height
    /// </summary>
    public void UpdateResolutionFromDevice()
    {
        if (adbHelper == null)
            return;

        // shell wm size 输出示例：Physical size: 1080x2340
        var output = adbHelper.ExecuteCommandWithOutput("shell wm size");
        if (string.IsNullOrEmpty(output))
            return;

        var match = Regex.Match(output, @"(\d+)\s*x\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int w) && int.TryParse(match.Groups[2].Value, out int h))
        {
            width = w;
            height = h;
            // 同步到 data，便于后续持久化或显示
            if (data != null)
            {
                data.resolution = new Vector2Int(width, height);
            }
            // 重新布局
            OnResize();
            Repaint();
            debugLog.AppendLine($"✓ 从设备获取分辨率: {width}x{height}");
            LogDebug();
        }
    }
    private Vector2 lastSize;
    void Update()
    {
        if (position.size != lastSize)
        {
            lastSize = position.size;
            OnResize();
        }
        // 处理队列，但限制每帧处理的数量，避免卡顿
        int processedCount = 0;
        bool hasNewFrame = false;
        lock (_queue)
        {
            while (_queue.Count > 0 && processedCount < MAX_QUEUE_ITEMS_PER_FRAME)
            {
                _queue.Dequeue().Invoke();
                processedCount++;
                hasNewFrame = true; // 标记有新的帧数据被处理
            }
        }

        // 如果有新帧被处理，更新最后接收时间
        if (hasNewFrame && isRunning && hasReceivedFirstFrame)
        {
            lastFrameReceivedTime = DateTime.Now;
        }


        // 计算帧率（仅串流时）
        if (enableFPS && isRunning)
        {
            var now = DateTime.Now;
            frameCounter++;

            double elapsed = (now - lastFPSCalculationTime).TotalSeconds;
            if (elapsed >= FPS_UPDATE_INTERVAL)
            {
                if (elapsed > 0)
                {
                    currentFPS = (float)(frameCounter / elapsed);
                }
                frameCounter = 0;
                lastFPSCalculationTime = now;
            }
        }
        else
        {
            // 未串流时清零
            currentFPS = 0f;
            frameCounter = 0;
        }

        // 流健康检测：检查是否长时间没有收到新帧
        var nowTime = DateTime.Now;
        if (isRunning && hasReceivedFirstFrame && lastFrameReceivedTime != DateTime.MinValue)
        {
            double timeSinceLastFrame = (nowTime - lastFrameReceivedTime).TotalSeconds;

            // 如果长时间没有新帧，强制触发 Repaint 保持画面更新
            // 这样即使流停止了，画面也不会完全卡住
            if (timeSinceLastFrame > STREAM_TIMEOUT)
            {
                // 长时间没有新帧，可能是流停止了
                // 但仍然强制 Repaint 以保持画面可见（即使没有新数据）
                if ((nowTime - lastRepaintTime).TotalSeconds >= REPAINT_INTERVAL)
                {
                    Repaint();
                    lastRepaintTime = nowTime;
                }

                // 每10秒输出一次警告（避免日志过多）
                if (timeSinceLastFrame > 10 && (int)timeSinceLastFrame % 10 == 0)
                {
                    if (debugMode)
                    {
                        debugLog.AppendLine($"⚠ 警告：已 {timeSinceLastFrame:F1} 秒未收到新帧，流可能已停止");
                        LogDebug();
                    }
                }
            }
        }

        // 限制 Repaint 频率，避免过度重绘
        if ((nowTime - lastRepaintTime).TotalSeconds >= REPAINT_INTERVAL)
        {
            Repaint();
            lastRepaintTime = nowTime;
        }
    }

    protected override void OnImGUI()
    {
        // 然后处理游戏视图的输入（只在绘制区域）
        HandleInput();
        // 先调用 base.OnImGUI() 让 Inspector 先处理事件
        base.OnImGUI();
    }
    Rect deviceRect;
    Rect screenRect;
    Rect drawRect;
    void OnResize()
    {

        float viewWidth = position.width;
        float viewHeight = position.height;

        // 性能优化：只在窗口大小改变时重新计算布局
        if (cachedViewWidth != viewWidth || cachedViewHeight != viewHeight)
        {
            cachedViewWidth = viewWidth;
            cachedViewHeight = viewHeight;

            // 1. 先在窗口中画出"手机外壳"（按固定分辨率等比缩放后再居中）
            float scaleToFit = Mathf.Min(viewWidth / width, viewHeight / height) * 0.9f; // 预留一点边距
            float deviceDrawWidth = width * scaleToFit;
            float deviceDrawHeight = height * scaleToFit;
            float deviceOffsetX = (viewWidth - deviceDrawWidth) * 0.5f;
            float deviceOffsetY = (viewHeight - deviceDrawHeight) * 0.5f;
            deviceRect = new Rect(deviceOffsetX, deviceOffsetY, deviceDrawWidth, deviceDrawHeight);
            cachedDeviceRect = deviceRect;

            // 2. 计算"屏幕区域"（外壳内再缩一圈，模拟边框 / 圆角 / 状态栏等）
            float paddingScaled = scaleToFit;
            screenRect = new Rect(
                deviceRect.x + paddingScaled,
                deviceRect.y + paddingScaled,
                Mathf.Max(1, deviceRect.width - paddingScaled * 2f),
                Mathf.Max(1, deviceRect.height - paddingScaled * 2f)
            );
            cachedScreenRect = screenRect;

            // 3. 在屏幕区域内，像 Game 视图一样按比例缩放并居中贴图
            float texWidth = width;
            float texHeight = height;
            float viewAspect = screenRect.width / screenRect.height;
            float texAspect = texWidth / texHeight;
            float drawWidth, drawHeight;
            if (texAspect > viewAspect)
            {
                // 贴图更"宽"，宽度占满屏幕，高度按比例
                drawWidth = screenRect.width;
                drawHeight = drawWidth / texAspect;
            }
            else
            {
                // 贴图更"高"，高度占满屏幕，宽度按比例
                drawHeight = screenRect.height;
                drawWidth = drawHeight * texAspect;
            }
            float offsetX = screenRect.x + (screenRect.width - drawWidth) * 0.5f;
            float offsetY = screenRect.y + (screenRect.height - drawHeight) * 0.5f;
            drawRect = new Rect(offsetX, offsetY, drawWidth, drawHeight);
            cachedDrawRect = drawRect;
        }
        else
        {
            // 使用缓存的布局
            deviceRect = cachedDeviceRect;
            screenRect = cachedScreenRect;
            drawRect = cachedDrawRect;
        }



    }
    protected override void OnGUI()
    {

        // 整个窗口背景（灰色），类似 Simulator
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), backgroundColor);
        // 设备外框（黑色矩形）
        EditorGUI.DrawRect(deviceRect, deviceFrameColor);
        EditorGUI.DrawRect(screenRect, screenBackgroundColor);
        // 只有在运行中且已接收到第一帧数据时才显示视频流，否则显示相机画面
        if (isRunning && hasReceivedFirstFrame && tempTexture2D != null)
        {
            GUI.DrawTexture(drawRect, tempTexture2D, ScaleMode.StretchToFill, false);
        }
        else
        {
            //绘制场景中主相机的画面（包含UI）
            if (width == 0 || height == 0)
            {
                return;
            }
            var camera = Camera.main;
            if (camera != null)
            {
                // 创建或更新临时 RenderTexture
                if (cameraRenderTexture == null || cameraRenderTexture.width != width || cameraRenderTexture.height != height)
                {
                    if (cameraRenderTexture != null)
                    {
                        cameraRenderTexture.Release();
                        DestroyImmediate(cameraRenderTexture);
                    }
                    cameraRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    cameraRenderTexture.Create();
                }

                // 性能优化：缓存 Canvas 数组，避免每帧查找（每秒更新一次）
                var now = DateTime.Now;
                if (cachedCanvases == null || (now - lastCanvasCacheTime).TotalSeconds >= CANVAS_CACHE_INTERVAL)
                {
                    cachedCanvases = FindObjectsOfType<Canvas>();
                    cachedOriginalRenderModes = new RenderMode[cachedCanvases.Length];
                    cachedOriginalCameras = new Camera[cachedCanvases.Length];
                    lastCanvasCacheTime = now;
                }

                // 保存相机原来的 targetTexture
                RenderTexture previousTarget = camera.targetTexture;
                RenderTexture previousActive = RenderTexture.active;

                // 设置所有Canvas为Screen Space - Camera模式（临时），以便它们能被渲染到RenderTexture
                if (cachedCanvases != null)
                {
                    for (int i = 0; i < cachedCanvases.Length; i++)
                    {
                        if (cachedCanvases[i] == null)
                            continue; // 跳过已销毁的 Canvas

                        cachedOriginalRenderModes[i] = cachedCanvases[i].renderMode;
                        cachedOriginalCameras[i] = cachedCanvases[i].worldCamera;

                        // 如果是Overlay模式，临时改为Camera模式以便渲染到RenderTexture
                        if (cachedCanvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            cachedCanvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                            cachedCanvases[i].worldCamera = camera;
                        }
                    }
                }

                // 让相机渲染到临时 RenderTexture
                camera.targetTexture = cameraRenderTexture;
                RenderTexture.active = cameraRenderTexture;

                // 清除RenderTexture
                GL.Clear(true, true, Color.black);

                // 渲染相机（这会包含所有设置为 Screen Space - Camera 模式的 Canvas）
                camera.Render();

                // 恢复Canvas的原始设置
                if (cachedCanvases != null)
                {
                    for (int i = 0; i < cachedCanvases.Length; i++)
                    {
                        if (cachedCanvases[i] == null)
                            continue; // 跳过已销毁的 Canvas
                        cachedCanvases[i].renderMode = cachedOriginalRenderModes[i];
                        cachedCanvases[i].worldCamera = cachedOriginalCameras[i];
                    }
                }

                // 恢复原来的设置
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                // 显示渲染结果
                GUI.DrawTexture(drawRect, cameraRenderTexture, ScaleMode.StretchToFill, false);
            }
        }
        // 在右下角显示帧率
        if (enableFPS && isRunning && currentFPS > 0)
        {
            DrawFPS(drawRect);
        }

    }
    private void DrawFPS(Rect drawRect)
    {
        // 在绘制区域的右下角显示帧率
        string fpsText = $"FPS: {currentFPS:F1}";

        // 创建样式
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        // 计算文本大小
        Vector2 textSize = style.CalcSize(new GUIContent(fpsText));

        // 在右下角绘制半透明背景
        float padding = 8f;
        float margin = 10f;
        Rect bgRect = new Rect(
            drawRect.xMax - textSize.x - padding * 2 - margin,
            drawRect.yMax - textSize.y - padding * 2 - margin,
            textSize.x + padding * 2,
            textSize.y + padding * 2
        );

        // 绘制半透明黑色背景
        Color bgColor = new Color(0, 0, 0, 0.7f);
        EditorGUI.DrawRect(bgRect, bgColor);

        // 绘制帧率文本
        GUI.Label(bgRect, fpsText, style);
    }
    void CreateTexture()
    {

        // 创建 RenderTexture
        try
        {
            // 预分配翻转缓冲区
            tempTexture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tempTexture2D.Apply();
            flippedDataBuffer = new byte[width * height * 4];

            // 缓存纹理尺寸（用于读取线程）
            cachedTextureWidth = width;
            cachedTextureHeight = height;
            cachedRowSize = cachedTextureWidth * 4; // RGBA = 4字节
            if (debugMode)
            {
                debugLog.AppendLine($"✓ 翻转缓冲区已预分配: {flippedDataBuffer.Length} 字节");
                debugLog.AppendLine($"✓ 纹理尺寸已缓存: {cachedTextureWidth}x{cachedTextureHeight}, 行大小: {cachedRowSize} 字节");
                LogDebug();
            }

        }
        catch (Exception ex)
        {
            debugLog.AppendLine($"❌ 创建纹理失败: {ex.Message}");
            debugLog.AppendLine($"堆栈: {ex.StackTrace}");
            LogDebug();
            return;
        }

    }

    void LogDebug()
    {
        if (!debugMode)
            return;
        if (debugLog.Length <= 0)
            return;

        Debug.Log(debugLog.ToString());
        debugLog.Clear();
    }



    public void Quit()
    {
        debugLog.AppendLine("=== 应用退出 ===");
        debugLog.AppendLine($"总接收帧数: {frameCount}");
        debugLog.AppendLine($"总接收字节: {totalBytesRead / 1024.0 / 1024.0:F2} MB");
        debugLog.AppendLine($"运行时间: {(DateTime.Now - startTime).TotalSeconds:F2} 秒");
        // 清理临时 Texture2D
        isRunning = false;
        hasReceivedFirstFrame = false; // 重置标志
        Repaint();
        if (tempTexture2D != null)
        {
            DestroyImmediate(tempTexture2D);
            tempTexture2D = null;
        }

        // 清理相机 RenderTexture
        if (cameraRenderTexture != null)
        {
            cameraRenderTexture.Release();
            DestroyImmediate(cameraRenderTexture);
            cameraRenderTexture = null;
        }
        if (frameCount > 0)
        {
            debugLog.AppendLine($"平均 FPS: {frameCount / (DateTime.Now - startTime).TotalSeconds:F2}");
        }
        LogDebug();


        if (readThread != null && readThread.IsAlive)
        {
            debugLog.AppendLine("等待读取线程结束...");
            LogDebug();
            readThread.Join(1000);
            if (readThread.IsAlive)
            {
                debugLog.AppendLine("⚠ 读取线程未在1秒内结束");
                LogDebug();
            }
        }

        if (ffmpegProcess != null && !ffmpegProcess.HasExited)
        {
            debugLog.AppendLine("终止 FFmpeg 进程");
            LogDebug();
            try
            {
                ffmpegProcess.Kill();
                ffmpegProcess.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                debugLog.AppendLine($"终止进程时出错: {ex.Message}");
                LogDebug();
            }
        }



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


    }

    protected override void OnDestroy()
    {
        Quit();
        base.OnDestroy();
    }










}
#endif