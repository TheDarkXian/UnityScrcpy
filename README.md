# UnityScrcpy

Unity Editor 工具，用于在 Unity 编辑器中显示和控制 Android 设备屏幕。通过 ADB、Scrcpy 和 FFmpeg 实现设备屏幕的实时显示和交互控制。

## 功能特性

### 🎥 屏幕显示

- 实时显示 Android 设备屏幕画面
- 支持通过 Scrcpy 或 FFmpeg 接收视频流
- 自动适配设备分辨率
- 可显示帧率信息

### 🖱️ 交互控制

- **触摸操作**：点击、滑动、长按
- **按键控制**：返回、Home、电源、音量、方向键等
- **文本输入**：支持输入文本和清空输入框
- **应用管理**：启动、停止、卸载应用
- **系统功能**：通知栏、快速设置、截图等

### 📱 设备管理

- 通过 ADB 连接 Android 设备（支持无线连接）
- 自动获取设备分辨率
- 查看已安装应用列表
- 应用搜索和选择

## 目录结构

```
UnityScrcpy/
├── Runtime/                    # 运行时脚本
│   ├── GameView.cs            # 主窗口类
│   ├── GameView.Connection.cs # 设备连接管理
│   ├── GameView.Control.cs    # 输入控制处理
│   ├── GameView.Streaming.cs  # 视频流处理
│   ├── AdbHelper.cs           # ADB 命令封装类
│   ├── FakeGameViewData.cs    # 配置数据 ScriptableObject
│   ├── FakeGameViewWindow.cs  # 窗口组件
│   └── Resources/             # 资源文件
├── Editor/                    # 编辑器相关
├── scrcpy/                    # Scrcpy 工具文件
└── ffmpeg/                    # FFmpeg 工具文件
```

## 依赖要求

### 必需工具

1. **ADB (Android Debug Bridge)**

   - 用于连接和控制 Android 设备
2. **Scrcpy** 
   - 用于屏幕镜像
   - v3.3.3版本
3. **FFmpeg** 
   - 用于视频流解码
   - tag v1.0.5版本
   - https://github.com/keijiro/FFmpegOut/releases

### Unity 依赖

- **Odin Inspector** (必需)
  - 用于 Inspector 界面美化
  - 需要从 Asset Store 安装

## 快速开始

### 1. 安装依赖

确保已安装 **Odin Inspector**：

- 从 Unity Asset Store 搜索并导入 "Odin Inspector"

### 2.下载release版本

​	1.https://github.com/TheDarkXian/UnityScrcpy/releases

​	2.解压后放到unity工程目录Assets/Script文件夹下。

### 3. 配置设备连接

1. 在 Unity 中创建配置资源：

   - `Assets` → `Create` → `Fake` → `FakeGameViewData`
   - 或使用 `Resources/默认设置.asset`

2. 配置工具路径：

   - 设置 **FFmpeg 路径**
   - 设置 **Scrcpy 路径**
   - 设置 **ADB 路径**

3. 配置连接信息：
   - **手机 IP**：设备 IP 地址（如 `192.168.1.100`）
   - **手机 IP 端口**：ADB 端口（默认 `5555`）
   - **分辨率**：设备分辨率（连接后会自动获取）

### 4. 连接设备

#### 方式一：通过 Unity 窗口

1. 打开窗口：`UGToolkit` → `Scrcpy` 
2. 在配置资源的 Inspector 中点击 **"开始画面传输"** 按钮
3. 系统会自动连接设备并开始显示画面(第一次推荐开启Debug模式，方便排查问题，控制台出现 ReadFrames 线程开始 字样后，需要使得手机屏幕发生大幅度重绘才能显示画面，可以在FakeGameViewWindow面板下面的系统功能处反复使用通知栏和收起通知栏功能来搞。)

### 5. 使用控制功能

连接成功后，在配置资源的 Inspector 中会显示控制面板：

- **设备控制**：基础按键、电源、音量、方向键、媒体控制、系统功能
- **文本输入**：输入文本并发送到设备
- **应用管理**：查看应用列表、启动/停止/卸载应用

## 核心类说明

### AdbHelper

独立的 ADB 命令封装类，提供所有 ADB 相关功能：

- 基础命令执行
- 触摸操作（点击、滑动、长按）
- 按键操作
- 文本输入
- 应用管理
- 设备连接

### GameView

主窗口类，继承自 `OdinEditorWindow`：

- 管理设备连接和视频流
- 处理鼠标输入并转换为设备坐标
- 显示设备屏幕画面

### FakeGameViewData

配置数据类，使用 `ScriptableObject`：

- 存储工具路径配置
- 存储连接设置
- 提供控制按钮界面

## 使用示例

### 基本使用流程

```csharp
// 1. 获取窗口实例
var gameView = EditorWindow.GetWindow<GameView>();

// 2. 设置配置数据
gameView.SetData(fakeGameViewData);

// 3. 连接设备
gameView.ConnectPhone();

// 4. 开始视频流
gameView.StartFFmpegVideoStreamming();
```

### 使用 AdbHelper 直接控制

```csharp
// 创建 AdbHelper 实例
var adb = new AdbHelper(adbPath, debugMode: true);

// 连接设备
adb.ConnectDevice("192.168.1.100", "5555");

// 点击屏幕
adb.Click(540, 960);

// 发送按键
adb.Back();
adb.Home();

// 输入文本
adb.InputText("Hello World");

// 启动应用
adb.LaunchApp("com.example.app");
```

## 坐标系统

### 坐标转换

- Unity GUI 坐标（左上角为原点）→ 设备像素坐标
- 自动处理 `drawRect` 的缩放和偏移
- 自动适配设备实际分辨率

### 坐标映射流程

1. 鼠标位置相对于 `drawRect` 的局部坐标
2. 归一化到 0-1 范围
3. 映射到设备分辨率（width × height）

## 注意事项

1. **分辨率自动获取**：连接设备后会自动通过 `adb shell wm size` 获取实际分辨率
2. **输入框焦点**：在 Inspector 中输入时，GameView 不会处理鼠标事件，避免干扰
3. **视频流格式**：支持 h264、rawvideo、mpegts 等格式
4. **仅 Editor 模式**：所有功能仅在 Unity Editor 中可用（使用 `#if UNITY_EDITOR`）
5. **依赖ffmpeg**：开始画面串流后必须要使得手机屏幕画面有大幅度变化才会成功链接视频流

## 常见问题

### Q: 点击位置有偏差？

A: 确保设备分辨率配置正确，连接后会自动获取实际分辨率。如果仍有偏差，检查视频流分辨率是否与设备分辨率一致。

### Q: 无法连接设备？

A:

1. 检查设备 IP 和端口是否正确
2. 确保设备已启用 USB 调试和无线调试
3. 检查 ADB 路径是否正确
4. 尝试手动执行 `adb connect IP:PORT` 测试连接

### Q: 视频流无法显示？

A:

1. 检查 FFmpeg 或 Scrcpy 路径是否正确
2. 检查视频流地址和端口配置
3. 查看 Console 中的错误信息
4. 确保设备已成功连接
5. 如果都已经正常连接，尝试使得手机的画面发生大幅度变化，比如使用面板中的展开收起状态栏按钮

### Q: 控制按钮不显示？

A: 控制按钮只在有画面时显示（`isRunning && hasReceivedFirstFrame`），确保视频流已成功启动。

## 许可证

本项目包含的工具：

- **Scrcpy**: [Apache License 2.0](https://github.com/Genymobile/scrcpy)
- **FFmpeg**: [LGPL/GPL](https://ffmpeg.org/legal.html)
- **ADB**: [Apache License 2.0](https://android.googlesource.com/platform/packages/modules/adb/)

## 更新日志

### 最新版本

- ✅ 自动获取设备分辨率
- ✅ 独立的 AdbHelper 类
- ✅ 应用列表查看和搜索
- ✅ 文本输入和清空功能
- ✅ 完整的设备控制功能

## 贡献

欢迎提交 Issue 和 Pull Request！

## 联系方式

如有问题或建议，请通过 Issue 反馈。
