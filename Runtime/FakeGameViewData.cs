#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEditor;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
[CreateAssetMenu(fileName = "FakeGameViewData", menuName = "Fake/FakeGameViewData")]
public class FakeGameViewData : ScriptableObject
{
    [LabelText("名称")]
    [InlineButton("RenameAsset")]
    public string itemName;
    public void RenameAsset()
    {
        string path = AssetDatabase.GetAssetPath(this);
        string newName = itemName;
        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogError("isnull");
            return;
        }
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            newName = newName.Replace(c.ToString(), "");
        }
        AssetDatabase.RenameAsset(path, newName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #region  工具配置
    [FoldoutGroup("工具配置")]
    [LabelText("FFmpeg 路径")]
    [InlineButton("BrowseFFmpeg", "选择")]
    public string ffmpegPath = @"D:\Tools\Minitools\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe";
    [FoldoutGroup("工具配置")]
    [LabelText("Scrcpy 路径")]
    [InlineButton("BrowseScrcpy", "选择")]
    public string scrcpyPath = @"D:\Tools\Minitools\scrcpy-win64\scrcpy.exe";
    [FoldoutGroup("工具配置")]
    [LabelText("ADB 路径")]
    [InlineButton("BrowseAdb", "选择")]
    public string abdPath = @"D:\Tools\Minitools\scrcpy-win64\scrcpy.exe";

    // 选择 FFmpeg 可执行文件
    void BrowseFFmpeg()
    {
        var path = EditorUtility.OpenFilePanel("选择 FFmpeg 可执行文件", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
        if (!string.IsNullOrEmpty(path))
        {
            ffmpegPath = path;
            EditorUtility.SetDirty(this);
        }
    }

    // 选择 scrcpy 可执行文件
    void BrowseScrcpy()
    {
        var path = EditorUtility.OpenFilePanel("选择 scrcpy 可执行文件", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
        if (!string.IsNullOrEmpty(path))
        {
            scrcpyPath = path;
            EditorUtility.SetDirty(this);
        }
    }

    // 选择 adb 可执行文件
    void BrowseAdb()
    {
        var path = EditorUtility.OpenFilePanel("选择 adb 可执行文件", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
        if (!string.IsNullOrEmpty(path))
        {
            abdPath = path;
            EditorUtility.SetDirty(this);
        }
    }


    #endregion

    #region 连接设置
    [FoldoutGroup("连接设置")]
    [LabelText("手机IP")]
    public string phoneIp = "192.168.1.100";
    [FoldoutGroup("连接设置")]
    [LabelText("手机IP端口")]
    public string phonePort = "5555";
    [FoldoutGroup("连接设置")]
    [FoldoutGroup("连接设置")]
    [LabelText("视频服务器端口")]
    public string videoPort = "1234";
    [FoldoutGroup("连接设置")]
    [LabelText("视频流地址")]
    public string videoStreamUrl = "tcp://localhost:1234";
    [FoldoutGroup("连接设置")]
    [LabelText("视频流格式")]
    [Tooltip("例如: h264, rawvideo, mpegts 等。如果为空则自动检测")]
    public inputFormatEnum inputFormat = inputFormatEnum.h264;
    [FoldoutGroup("连接设置")]
    [LabelText("显示帧率")]
    public bool enableFPS = false;
    #endregion
    [LabelText("分辨率(与手机一致)")]
    public Vector2Int resolution = new Vector2Int(1080, 2340);
    [LabelText("启用Debug")]
    public bool enableDebug;
    [LabelText("启用Scrcpy窗口")]
    public bool enableScrcpyWindow = false;

    public enum inputFormatEnum
    {
        h264,
        rawvideo,
        mpegts,
    }

    #region GameView参数
    [FoldoutGroup("GameView参数")]
    public Color backgroundColor = new Color(0.18f, 0.18f, 0.18f);
    [FoldoutGroup("GameView参数")]
    public Color deviceFrameColor = Color.black;
    [FoldoutGroup("GameView参数")]
    public Color screenBackgroundColor = Color.black;
    #endregion
    /*
    [Button("激活配置",ButtonSizes.Large)]
    public void SetData(){
  var gameView=EditorWindow.GetWindow<GameView>();
        gameView.SetData(this);
    }
    [Button("连接手机",ButtonSizes.Large)]
    public void ConnectPhone(){
        var gameView=EditorWindow.GetWindow<GameView>();
        gameView.SetData(this);
        gameView.ConnectPhone();
    }
    [Button("开启Scrcpy",ButtonSizes.Large)]
    public void StartScrcpy(){
        var gameView=EditorWindow.GetWindow<GameView>();
        gameView.SetData(this);
        gameView.StartScrcpy();
    }
    [Button("开始视频服务器",ButtonSizes.Large)]
    public void StartVideoServer(){
        var gameView=EditorWindow.GetWindow<GameView>();
        gameView.SetData(this);
        gameView.StartVideoServer();
    }
    */

    [Button("开始画面传输", ButtonSizes.Large)]
    public void StartFFmpeg()
    {

        var gameView = EditorWindow.GetWindow<GameView>();
        gameView.SetData(this);
        gameView.StartFFmpegVideoStreamming();

    }
    [Button("关闭流", ButtonSizes.Large)]
    public void ExitFFmpeg()
    {

        var gameView = EditorWindow.GetWindow<GameView>();
        gameView.Quit();

    }

    #region 设备控制按钮
    [FoldoutGroup("设备控制")]
    [BoxGroup("设备控制/基础控制")]
    [HorizontalGroup("设备控制/基础控制/导航")]
    [Button("返回", ButtonSizes.Medium)]
    public void Back() => SendKeyEvent(4);
    [HorizontalGroup("设备控制/基础控制/导航")]
    [Button("Home", ButtonSizes.Medium)]
    public void Home() => SendKeyEvent(3);
    [HorizontalGroup("设备控制/基础控制/导航")]
    [Button("多任务", ButtonSizes.Medium)]
    public void RecentApps() => SendKeyEvent(187);

    [BoxGroup("设备控制/电源控制")]
    [HorizontalGroup("设备控制/电源控制/按钮")]
    [Button("电源", ButtonSizes.Medium)]
    public void Power() => SendKeyEvent(26);
    [HorizontalGroup("设备控制/电源控制/按钮")]
    [Button("唤醒", ButtonSizes.Medium)]
    public void WakeScreen() => SendKeyEvent(224);
    [HorizontalGroup("设备控制/电源控制/按钮")]
    [Button("休眠", ButtonSizes.Medium)]
    public void SleepScreen() => SendKeyEvent(223);

    [BoxGroup("设备控制/音量控制")]
    [HorizontalGroup("设备控制/音量控制/按钮")]
    [Button("音量+", ButtonSizes.Medium)]
    public void VolumeUp() => SendKeyEvent(24);
    [HorizontalGroup("设备控制/音量控制/按钮")]
    [Button("音量-", ButtonSizes.Medium)]
    public void VolumeDown() => SendKeyEvent(25);

    [BoxGroup("设备控制/方向键")]
    [HorizontalGroup("设备控制/方向键/行1")]
    [Button("↑", ButtonSizes.Medium)]
    public void DPadUp() => SendKeyEvent(19);
    [HorizontalGroup("设备控制/方向键/行2")]
    [Button("←", ButtonSizes.Medium)]
    public void DPadLeft() => SendKeyEvent(21);
    [HorizontalGroup("设备控制/方向键/行2")]
    [Button("确定", ButtonSizes.Medium)]
    public void DPadCenter() => SendKeyEvent(23);
    [HorizontalGroup("设备控制/方向键/行2")]
    [Button("→", ButtonSizes.Medium)]
    public void DPadRight() => SendKeyEvent(22);
    [HorizontalGroup("设备控制/方向键/行3")]
    [Button("↓", ButtonSizes.Medium)]
    public void DPadDown() => SendKeyEvent(20);

    [BoxGroup("设备控制/媒体控制")]
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("播放/暂停", ButtonSizes.Medium)]
    public void PlayPause() => SendKeyEvent(85);
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("停止", ButtonSizes.Medium)]
    public void StopMedia() => SendKeyEvent(86);
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("上一曲", ButtonSizes.Medium)]
    public void PrevTrack() => SendKeyEvent(88);
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("下一曲", ButtonSizes.Medium)]
    public void NextTrack() => SendKeyEvent(87);
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("快退", ButtonSizes.Medium)]
    public void Rewind() => SendKeyEvent(89);
    [HorizontalGroup("设备控制/媒体控制/按钮")]
    [Button("快进", ButtonSizes.Medium)]
    public void FastForward() => SendKeyEvent(90);

    [BoxGroup("设备控制/系统功能")]
    [HorizontalGroup("设备控制/系统功能/按钮")]
    [Button("通知栏", ButtonSizes.Medium)]
    public void ExpandNotifications() => SendAdbCommand("shell cmd statusbar expand-notifications");
    [HorizontalGroup("设备控制/系统功能/按钮")]
    [Button("收起通知", ButtonSizes.Medium)]
    public void CollapseNotifications() => SendAdbCommand("shell cmd statusbar collapse");
    [HorizontalGroup("设备控制/系统功能/按钮")]
    [Button("快速设置", ButtonSizes.Medium)]
    public void ExpandQuickSettings() => SendAdbCommand("shell cmd statusbar expand-settings");
    [HorizontalGroup("设备控制/系统功能/按钮")]
    [Button("截图", ButtonSizes.Medium)]
    public void Screenshot() => SendKeyEvent(120);
    #endregion

    #region 文本输入和应用管理
    [FoldoutGroup("文本输入和应用管理")]
    [BoxGroup("文本输入和应用管理/文本输入")]
    [LabelText("输入文本")]
    [HorizontalGroup("文本输入和应用管理/文本输入/输入")]
    public string inputText = "";

    [HorizontalGroup("文本输入和应用管理/文本输入/输入")]
    [Button("发送", ButtonSizes.Medium)]
    public void SendInputText()
    {
        if (!string.IsNullOrEmpty(inputText))
        {
            var adb = GetAdbHelper();
            adb?.InputText(inputText);
        }
    }

    [HorizontalGroup("文本输入和应用管理/文本输入/输入")]
    [Button("清空", ButtonSizes.Medium)]
    [GUIColor(0.9f, 0.7f, 0.7f)]
    public void ClearInputText()
    {
        var adb = GetAdbHelper();
        adb?.ClearInputField();
    }

    [BoxGroup("文本输入和应用管理/应用管理")]
    [LabelText("应用包名")]
    [HorizontalGroup("文本输入和应用管理/应用管理/包名")]
    public string appPackageName = "com.example.app";

    [BoxGroup("文本输入和应用管理/应用列表")]
    [LabelText("搜索应用")]
    [HorizontalGroup("文本输入和应用管理/应用列表/搜索")]
    public string appSearchFilter = "";

    [HorizontalGroup("文本输入和应用管理/应用列表/搜索")]
    [Button("刷新列表", ButtonSizes.Medium)]
    public void RefreshAppList()
    {
        var adb = GetAdbHelper();
        if (adb != null)
        {
            var apps = adb.GetInstalledApps();
            installedApps = apps.Select(a => new GameView.AppInfo(a.packageName, a.appName)).ToList();
        }
    }

    [BoxGroup("文本输入和应用管理/应用列表")]
    [LabelText("已安装应用")]
    [ListDrawerSettings(ShowIndexLabels = false, DraggableItems = false, HideAddButton = true, HideRemoveButton = true)]
    [ShowIf("@installedApps != null && installedApps.Count > 0")]
    public List<GameView.AppInfo> installedApps = new List<GameView.AppInfo>();

    [BoxGroup("文本输入和应用管理/应用列表")]
    [ShowIf("@installedApps != null && installedApps.Count > 0")]
    [PropertySpace(5)]
    [InfoBox("点击'选择'按钮可自动填充包名", InfoMessageType.Info)]
    [TableList(ShowIndexLabels = false, AlwaysExpanded = true, NumberOfItemsPerPage = 10)]
    [Searchable]
    [ShowInInspector]
    private List<AppListItem> filteredAppList
    {
        get
        {
            if (installedApps == null || installedApps.Count == 0)
                return new List<AppListItem>();

            var filtered = installedApps;
            if (!string.IsNullOrEmpty(appSearchFilter))
            {
                string filter = appSearchFilter.ToLower();
                filtered = installedApps.Where(app =>
                    app.appName.ToLower().Contains(filter) ||
                    app.packageName.ToLower().Contains(filter)
                ).ToList();
            }

            return filtered.Select((app, index) => new AppListItem
            {
                index = index,
                appName = app.appName,
                packageName = app.packageName,
                data = this
            }).ToList();
        }
    }

    [System.Serializable]
    public class AppListItem
    {
        [HideInInspector]
        public int index;

        [TableColumnWidth(250)]
        [LabelText("应用名称")]
        [Sirenix.OdinInspector.ReadOnly]
        public string appName;

        [TableColumnWidth(350)]
        [LabelText("包名")]
        [Sirenix.OdinInspector.ReadOnly]
        public string packageName;

        [TableColumnWidth(80)]
        [Button("选择")]
        private void SelectApp()
        {
            if (data != null && !string.IsNullOrEmpty(packageName))
            {
                data.appPackageName = packageName;
                Debug.Log($"已选择应用: {appName} ({packageName})");
            }
        }

        [HideInInspector]
        public FakeGameViewData data;
    }

    [HorizontalGroup("文本输入和应用管理/应用管理/操作")]
    [Button("打开应用", ButtonSizes.Medium)]
    public void LaunchApp()
    {
        if (!string.IsNullOrEmpty(appPackageName))
        {
            var adb = GetAdbHelper();
            adb?.LaunchApp(appPackageName);
        }
    }

    [HorizontalGroup("文本输入和应用管理/应用管理/操作")]
    [Button("退出应用", ButtonSizes.Medium)]
    [GUIColor(1f, 0.8f, 0.8f)]
    public void ForceStopApp()
    {
        if (!string.IsNullOrEmpty(appPackageName))
        {
            var adb = GetAdbHelper();
            adb?.ForceStopApp(appPackageName);
        }
    }

    [HorizontalGroup("文本输入和应用管理/应用管理/操作")]
    [Button("卸载应用", ButtonSizes.Medium)]
    [GUIColor(1f, 0.6f, 0.6f)]
    public void UninstallApp()
    {
        if (!string.IsNullOrEmpty(appPackageName))
        {
            var adb = GetAdbHelper();
            adb?.UninstallApp(appPackageName);
        }
    }
    #endregion

    /// <summary>
    /// 获取 AdbHelper 实例
    /// </summary>
    private AdbHelper GetAdbHelper()
    {
        var gameView = EditorWindow.GetWindow<GameView>();
        if (gameView != null && gameView.data == this && gameView.adbHelper != null)
        {
            return gameView.adbHelper;
        }
        else
        {
            // 如果没有 GameView 窗口，创建临时 AdbHelper
            return new AdbHelper(abdPath, enableDebug);
        }
    }

    /// <summary>
    /// 发送按键事件到设备
    /// </summary>
    private void SendKeyEvent(int keyCode)
    {
        var adb = GetAdbHelper();
        adb?.KeyEvent(keyCode);
    }

    /// <summary>
    /// 发送 ADB 命令到设备
    /// </summary>
    private void SendAdbCommand(string command)
    {
        var adb = GetAdbHelper();
        adb?.ExecuteCommand(command);
    }


}
#endif
