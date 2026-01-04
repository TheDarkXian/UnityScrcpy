#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;
using System.Linq;

public partial class GameView
{
    /*

🎯 ADB 控制手机的完整指令合集（最全版）
✅ 一、触摸类（Tap / Swipe / Long press）
1. 单击 Tap
adb shell input tap x y

2. 滑动 Swipe
adb shell input swipe x1 y1 x2 y2 duration

3. 长按（两个方法）

方式 1：滑动 0 距离（官方推荐）

adb shell input swipe x y x y 500
方式 2（部分设备也支持）

adb shell input touchscreen swipe x y x y 500

✅ 二、多点触控（Multi-touch）

安卓原生 input 不直接支持多点触摸，但可使用 sendevent 或 getevent 回放触点。

如果你需要，我可以给你 完整的多点触控脚本（可同时多指操作）。

✅ 三、输入文本（Text）
1. 输入文字：
adb shell input text "hello123"
注意：空格要写成 %s 或 _：
adb shell input text "hello_world"
2. 粘贴剪贴板内容（10+ Android 必须用这个）
adb shell input keyevent 279
✅ 四、键盘按键 KeyEvent（最全列表）
下面是安卓所有标准键码（KeyEvent），你可以全用：
按键	KeyEvent 值
Home	3
Back	4
电源键 Power	26
菜单键	1
音量+	24
音量-	25
相机键	27
Enter	66
空格	62
删除键（删除字符）	67
Tab	61
Esc	111
最近任务	187
截图（部分设备）	120
唤醒屏幕	224
完整文字输入相关：
按键	事件
A ~ Z	29 ~ 54
0 ~ 9	7 ~ 16
例如输入 A：
adb shell input keyevent 29
✅ 五、屏幕控制（开 / 关屏幕）
开屏：
adb shell input keyevent 224
关屏：
adb shell input keyevent 223
电源键（切换）：
adb shell input keyevent 26
✅ 六、导航类控制（返回、主页、多任务）
返回：
adb shell input keyevent 4
主页：
adb shell input keyevent 3
最近任务：
adb shell input keyevent 187
✅ 七、方向键（D-PAD）
控制焦点：
adb shell input keyevent 19   # 上
adb shell input keyevent 20   # 下
adb shell input keyevent 21   # 左
adb shell input keyevent 22   # 右
adb shell input keyevent 23   # 确认(OK)
✅ 八、媒体按键（播放器控制）
操作	代码
播放/暂停	85
停止	86
下一曲	87
上一曲	88
快进	90
快退	89
例如暂停播放：
adb shell input keyevent 85
✅ 九、特殊控制
截图（部分厂商支持）
adb shell input keyevent 120
关闭通知栏
adb shell cmd statusbar collapse
打开通知栏
adb shell cmd statusbar expand-notifications
打开快速设置
adb shell cmd statusbar expand-settings
    */

    public void Click(int x, int y)
    {
        if (adbHelper != null)
            adbHelper.Click(x, y);
    }

    /// <summary>
    /// 使用 0-1 归一化坐标点击（自动换算为设备分辨率）
    /// </summary>
    public void Click01(Vector2 normalized)
    {
        var pos = ToDevicePixel(normalized);
        Click(pos.x, pos.y);
    }

    /// <summary>
    /// 模拟滑动，传入设备像素坐标与持续时间（毫秒）
    /// </summary>
    public void Swipe(int x1, int y1, int x2, int y2, int durationMs = 200)
    {
        if (adbHelper != null)
            adbHelper.Swipe(x1, y1, x2, y2, durationMs);
    }

    /// <summary>
    /// 长按：用 0 距离滑动模拟，默认 500ms
    /// </summary>
    public void LongPress(int x, int y, int durationMs = 500)
    {
        if (adbHelper != null)
            adbHelper.LongPress(x, y, durationMs);
    }

    public void LongPress01(Vector2 normalized, int durationMs = 500)
    {
        var p = ToDevicePixel(normalized);
        LongPress(p.x, p.y, durationMs);
    }

    /// <summary>
    /// 使用 0-1 归一化坐标滑动
    /// </summary>
    public void Swipe01(Vector2 from01, Vector2 to01, int durationMs = 200)
    {
        var a = ToDevicePixel(from01);
        var b = ToDevicePixel(to01);
        Swipe(a.x, a.y, b.x, b.y, durationMs);
    }

    /// <summary>
    /// 输入文本（会转义空格）
    /// </summary>
    public void InputText(string text)
    {
        if (adbHelper != null)
            adbHelper.InputText(text);
    }

    /// <summary>
    /// 清空输入框（发送多个删除键）
    /// </summary>
    public void ClearInputField()
    {
        if (adbHelper != null)
            adbHelper.ClearInputField();
    }

    public void Back()
    {
        if (adbHelper != null)
            adbHelper.Back();
    }
    public void Home()
    {
        if (adbHelper != null)
            adbHelper.Home();
    }
    public void RecentApps()
    {
        if (adbHelper != null)
            adbHelper.RecentApps();
    }
    public void Power()
    {
        if (adbHelper != null)
            adbHelper.Power();
    }
    public void WakeScreen()
    {
        if (adbHelper != null)
            adbHelper.WakeScreen();
    }
    public void SleepScreen()
    {
        if (adbHelper != null)
            adbHelper.SleepScreen();
    }
    public void VolumeUp()
    {
        if (adbHelper != null)
            adbHelper.VolumeUp();
    }
    public void VolumeDown()
    {
        if (adbHelper != null)
            adbHelper.VolumeDown();
    }
    public void DPadUp()
    {
        if (adbHelper != null)
            adbHelper.DPadUp();
    }
    public void DPadDown()
    {
        if (adbHelper != null)
            adbHelper.DPadDown();
    }
    public void DPadLeft()
    {
        if (adbHelper != null)
            adbHelper.DPadLeft();
    }
    public void DPadRight()
    {
        if (adbHelper != null)
            adbHelper.DPadRight();
    }
    public void DPadCenter()
    {
        if (adbHelper != null)
            adbHelper.DPadCenter();
    }
    public void PlayPause()
    {
        if (adbHelper != null)
            adbHelper.PlayPause();
    }
    public void StopMedia()
    {
        if (adbHelper != null)
            adbHelper.StopMedia();
    }
    public void NextTrack()
    {
        if (adbHelper != null)
            adbHelper.NextTrack();
    }
    public void PrevTrack()
    {
        if (adbHelper != null)
            adbHelper.PrevTrack();
    }
    public void FastForward()
    {
        if (adbHelper != null)
            adbHelper.FastForward();
    }
    public void Rewind()
    {
        if (adbHelper != null)
            adbHelper.Rewind();
    }
    public void ExpandNotifications()
    {
        if (adbHelper != null)
            adbHelper.ExpandNotifications();
    }
    public void CollapseNotifications()
    {
        if (adbHelper != null)
            adbHelper.CollapseNotifications();
    }
    public void ExpandQuickSettings()
    {
        if (adbHelper != null)
            adbHelper.ExpandQuickSettings();
    }
    public void Screenshot()
    {
        if (adbHelper != null)
            adbHelper.Screenshot();
    }

    public void KeyEvent(int keyCode)
    {
        if (adbHelper != null)
            adbHelper.KeyEvent(keyCode);
    }

    /// <summary>
    /// 剪贴板粘贴（Android 10+）
    /// </summary>
    public void PasteClipboard() => KeyEvent(279);

    /// <summary>
    /// 启动应用（通过包名）
    /// </summary>
    public void LaunchApp(string packageName)
    {
        if (adbHelper != null)
            adbHelper.LaunchApp(packageName);
    }

    /// <summary>
    /// 强制停止应用（退出当前应用）
    /// </summary>
    public void ForceStopApp(string packageName)
    {
        if (adbHelper != null)
            adbHelper.ForceStopApp(packageName);
    }

    /// <summary>
    /// 卸载应用（通过包名）
    /// </summary>
    public void UninstallApp(string packageName)
    {
        if (adbHelper != null)
            adbHelper.UninstallApp(packageName);
    }

    /// <summary>
    /// 应用信息（使用 AdbHelper 中的定义）
    /// </summary>
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

    /// <summary>
    /// 获取设备上安装的应用列表（仅第三方应用）
    /// </summary>
    public List<AppInfo> GetInstalledApps()
    {
        if (adbHelper == null)
            return new List<AppInfo>();

        var apps = adbHelper.GetInstalledApps();
        return apps.Select(a => new AppInfo(a.packageName, a.appName)).ToList();
    }

    /// <summary>
    /// 将 0-1 坐标转为设备分辨率像素，自动 clamp
    /// </summary>
    private Vector2Int ToDevicePixel(Vector2 normalized)
    {
        float nx = Mathf.Clamp01(normalized.x);
        float ny = Mathf.Clamp01(normalized.y);

        // 优先使用当前帧的真实分辨率，避免配置分辨率与实际视频分辨率不一致造成偏差
        int targetWidth = tempTexture2D != null ? tempTexture2D.width : width;
        int targetHeight = tempTexture2D != null ? tempTexture2D.height : height;

        return new Vector2Int(
            Mathf.RoundToInt(nx * (targetWidth - 1)),
            Mathf.RoundToInt(ny * (targetHeight - 1))
        );
    }

    /// <summary>
    /// 将相对于 drawRect 的坐标转换为设备分辨率像素坐标
    /// </summary>
    private Vector2Int ToDevicePixelFromDrawRect(Vector2 localPos)
    {
        if (drawRect.width <= 0 || drawRect.height <= 0)
            return Vector2Int.zero;

        // 归一化到 0-1
        float normalizedX = Mathf.Clamp01(localPos.x / drawRect.width);
        float normalizedY = Mathf.Clamp01(localPos.y / drawRect.height);

        // 使用实际视频帧分辨率进行映射，减少偏差
        int targetWidth = tempTexture2D != null ? tempTexture2D.width : width;
        int targetHeight = tempTexture2D != null ? tempTexture2D.height : height;

        return new Vector2Int(
            Mathf.RoundToInt(normalizedX * (targetWidth - 1)),
            Mathf.RoundToInt(normalizedY * (targetHeight - 1))
        );
    }

    private Vector2 pressPos;
    private double pressTime;
    private bool dragging;
    private bool longDragFired;
    private bool hasPress;
    private Vector2 normalizedPressPos;

    void HandleInput()
    {
        if (!isRunning)
        {
            return;
        }
        if (!hasReceivedFirstFrame)
        {
            return;
        }

        var e = Event.current;
        var rt = e.type; // Odin 下用普通 type，避免 rawType 被内部改写
        // 进入窗口/离开窗口时重置状态，避免长时间累积 duration
        if (rt == EventType.MouseEnterWindow)
        {
            dragging = false;
            longDragFired = false;
            hasPress = false;
            pressTime = EditorApplication.timeSinceStartup;
            return;
        }
        if (rt == EventType.MouseLeaveWindow)
        {
            dragging = false;
            longDragFired = false;
            hasPress = false;
            pressTime = EditorApplication.timeSinceStartup;
            return;
        }

        // Layout/Repaint 不处理输入
        if (rt == EventType.Layout || rt == EventType.Repaint)
            return;

        // 只在屏幕区域内处理鼠标事件，避免阻塞其它面板
        if (!drawRect.Contains(e.mousePosition))
            return;
        // Debug.Log($"{e.type} at {e.mousePosition}");

        // 只处理鼠标相关事件，其他直接返回
        if (rt != EventType.MouseDown &&
            rt != EventType.MouseDrag &&
            rt != EventType.MouseUp &&
            rt != EventType.ScrollWheel &&
            rt != EventType.MouseMove)
        {
            return;
        }

        switch (rt)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    e.Use();
                    // 将鼠标位置转换为相对于 drawRect 的坐标
                    pressPos = e.mousePosition;
                    pressPos.x -= drawRect.position.x;
                    pressPos.y -= drawRect.position.y;
                    pressTime = EditorApplication.timeSinceStartup;
                    dragging = false;
                    longDragFired = false;
                    hasPress = true;
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0 && hasPress)
                {
                    dragging = true;
                    // 将鼠标位置转换为相对于 drawRect 的坐标
                    var currentPos = e.mousePosition;
                    currentPos.x -= drawRect.position.x;
                    currentPos.y -= drawRect.position.y;
                    var dist = Vector2.Distance(currentPos, pressPos);
                    var dur = EditorApplication.timeSinceStartup - pressTime;

                    // 长滑动实时触发
                    if (!longDragFired && dist > 5f && dur > 0.5f)
                    {
                        var devicePressPos = ToDevicePixelFromDrawRect(pressPos);
                        var deviceCurrentPos = ToDevicePixelFromDrawRect(currentPos);
                        //Debug.Log($"LongDrag start from device({devicePressPos.x},{devicePressPos.y}) to device({deviceCurrentPos.x},{deviceCurrentPos.y}), {dur:F2}s");
                        longDragFired = true;
                    }
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && hasPress)
                {
                    if (GUIUtility.hotControl != 0)
                        GUIUtility.hotControl = 0;
                    e.Use();
                    // 将鼠标位置转换为相对于 drawRect 的坐标
                    var currentPos = e.mousePosition;
                    currentPos.x -= drawRect.position.x;
                    currentPos.y -= drawRect.position.y;
                    var dist = Vector2.Distance(currentPos, pressPos);
                    var dur = EditorApplication.timeSinceStartup - pressTime;

                    // 转换为设备坐标用于输出和操作
                    var devicePressPos = ToDevicePixelFromDrawRect(pressPos);
                    var deviceCurrentPos = ToDevicePixelFromDrawRect(currentPos);

                    if (!dragging && dist < 5f && dur < 0.2f)
                    {
                        // Debug.Log($"Click at device({devicePressPos.x},{devicePressPos.y}), dur {dur:F2}s");
                        Click(devicePressPos.x, devicePressPos.y);
                    }
                    else if (dist >= 5f && dur < 0.5f)
                    {
                        //Debug.Log($"Swipe from device({devicePressPos.x},{devicePressPos.y}) to device({deviceCurrentPos.x},{deviceCurrentPos.y}), dur {dur:F2}s");
                        Swipe(devicePressPos.x, devicePressPos.y, deviceCurrentPos.x, deviceCurrentPos.y);
                    }
                    else if (dist >= 5f && dur >= 0.5f)
                    {
                        //Debug.Log($"LongSwipe from device({devicePressPos.x},{devicePressPos.y}) to device({deviceCurrentPos.x},{deviceCurrentPos.y}), dur {dur:F2}s");
                        Swipe(devicePressPos.x, devicePressPos.y, deviceCurrentPos.x, deviceCurrentPos.y, (int)(dur * 1000));
                    }
                    else if (dist < 5f && dur >= 0.5f)
                    {
                        //Debug.Log($"LongPress at device({devicePressPos.x},{devicePressPos.y}), dur {dur:F2}s");
                        LongPress(devicePressPos.x, devicePressPos.y, (int)(dur * 1000));
                    }

                    hasPress = false;
                    dragging = false;
                    longDragFired = false;
                }
                break;
        }



    }

}
#endif

