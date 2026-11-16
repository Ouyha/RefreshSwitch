# RefreshSwitch — 一键切换显示器刷新率

Windows 小工具：双击运行，在 120Hz / 60Hz 之间切换，屏幕上弹一张卡片告诉你切到了哪个刷新率，3 秒后自动消失。

编译产物只有一个 17KB 的 exe，不常驻、不装服务、不写自启项，跑完就退出。

---

## 快速使用

| 方式 | 说明 |
| --- | --- |
| 双击桌面「刷新率切换」图标 | 在 120Hz ⇄ 60Hz 之间来回切 |
| 双击 `RefreshSwitch.exe` | 同上 |
| `RefreshSwitch.exe --rate 60` | 直接切到指定刷新率，不做循环判断 |

命令行带参数适合做两个独立快捷方式（一个固定 60Hz，一个固定 120Hz）。

### 提示卡片的三种状态

| 卡片 | 颜色 | 含义 | 停留 |
| --- | --- | --- | --- |
| 已切换到 120 Hz | 绿点 | 切换成功 | 3 秒 |
| 已经是 120 Hz | 黄点 | 当前就在这一档，没有动作 | 3 秒 |
| 切换失败 | 红点 | 附具体原因（模式不支持 / 驱动返回码 / 未真正生效） | 5 秒 |

卡片不会抢焦点、不进 Alt+Tab，点一下可以立即关闭。

### 本机验证记录（2026-11-15，2560×1600，高 DPI 缩放）

| 场景 | 实测结果 |
| --- | --- |
| 双击 exe，当前 60Hz | 绿卡「已切换到 120 Hz」，约 1.0 秒出现，卡片关闭后进程自动退出 |
| `--rate 120`，当前已在 120Hz | 黄卡「已经是 120 Hz」，不做任何切换 |
| `--rate 999`，档位不存在 | 红卡「当前分辨率下没有 999 Hz，可选：60, 120」 |

---

## 目录结构

```
refresh-switch/
├─ RefreshSwitch.exe      编译产物，直接可用
├─ src/RefreshSwitch.cs   全部源码（约 360 行，单文件）
├─ src/app.ico            编译时由 build.ps1 生成的图标
├─ build.ps1              重新编译
├─ Install-Shortcut.ps1   重建桌面快捷方式
└─ README.md
```

---

## 修改与重新编译

1. 打开 `src/RefreshSwitch.cs`，文件底部 `Program` 类里改档位：

```csharp
const int HzA = 120;
const int HzB = 60;
```

2. 重新编译（**先确认 exe 没在运行**，被占用会删不掉旧文件）：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` 用的是系统自带 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，引用从 GAC 解析（PresentationFramework / PresentationCore / WindowsBase / System.Xaml / WinForms / Drawing），**不需要装 SDK、不需要 NuGet**。同时会用 System.Drawing 现画一个 256×256 的 ICO 嵌进去。

编译参数：`/target:winexe /platform:x64 /optimize+`。改完源码不必动其它配置。

---

## 实现要点

**切换刷新率**（`Display.TrySet`）

1. `EnumDisplayDevices` 取第一个连接桌面的显示器（`\\.\DISPLAY1`）。
2. `EnumDisplaySettings(ENUM_CURRENT_SETTINGS)` 读当前模式。
3. 只改 `dmDisplayFrequency`，分辨率 / 色深沿用当前值，`dmFields` 置 `DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY`。
4. `ChangeDisplaySettingsEx` 带 `CDS_UPDATEREGISTRY`（重启后保持），失败则去掉该标志重试一次。
5. 等 80ms 回读实际模式，偏差超过 ±2Hz 判为失败——驱动“假装成功”是真实存在的情况。

成功路径不做全量模式枚举，所以只在必要时（失败、要给提示）才枚举，省掉上百毫秒。

**提示卡片**（`Hud.Build`）

- WPF 无边框 + `AllowsTransparency`，`Border` 圆角 14、`DropShadowEffect` 真阴影（外面留 26px margin 给阴影绘制）。
- `SizeToContent` 自适应文字长度，位置取工作区高度的 62%。
- `ShowActivated=false` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`：不抢焦点、不进 Alt+Tab。
- 停留期间用 `DispatcherTimer` 关窗，最后 320ms 做淡出动画。

**防连击**：命名互斥体 `Local\RefreshSwitch.Switching`，双击两次不会切两次（第二次直接退出）。

---

## 已知限制

- **没有全局快捷键**。全局热键必须有一个进程常驻监听按键（`RegisterHotKey`），本工具是“跑完即退”的模型，所以不提供。真想要按键：右键桌面快捷方式 → 属性 → “快捷键”里设一个，那是资源管理器负责的，不需要常驻进程。
- 顺带记一笔：这台机器上 `Ctrl+Alt+R` 已被别的软件占用，注册会返回 `1409 (ERROR_HOTKEY_ALREADY_REGISTERED)`，要设请换组合。
- **只处理第一个显示器**。多屏场景需要扩展 `Display.GetDevices()` 的选择逻辑。
- **启动约 1 秒**。其中约 0.6 秒是 .NET Framework + WPF 的冷启动，屏幕在此之前（约 0.6 秒）就已经切过去了，卡片是事后确认。想做到 0.1 秒级别只能写原生 C++。
- 依赖 .NET Framework 4.x（Win10/11 自带，无需额外安装）。
- 按 DPI 缩放使用的是系统 DPI（`SetProcessDPIAware`），多显示器不同缩放比例时卡片大小可能与所在屏不完全匹配。

---

## 排错

**弹红卡「当前分辨率下没有 X Hz，可选：60, 120」**
该分辨率下确实没有这个刷新率。这条消息本身也是查可用档位的手段：随便传个不存在的值，例如 `RefreshSwitch.exe --rate 999`，红卡就会列出当前分辨率下所有可用的刷新率。

**弹红卡「驱动没有真正切换」**
`ChangeDisplaySettingsEx` 返回了成功，但回读仍是旧值。通常是驱动/显卡控制面板接管了刷新率，去 AMD / NVIDIA 控制面板里把“自定义刷新率”关掉再试。

**切完屏幕黑一下或花屏**
属正常现象（模式切换会重新初始化显示输出）。若持续不恢复，等 15 秒系统会自动回滚，或重启进入安全模式恢复。

**桌面图标丢了**
运行 `Install-Shortcut.ps1` 重建。

**编译报「文件被占用」**
`RefreshSwitch.exe` 还在运行（卡片没关或进程残留），先 `Stop-Process -Name RefreshSwitch` 再编译。

---

## 历史记录（避免后续维护踩回头路）

这个工具前后换过三版实现，只保留最后一版：

1. **PowerShell + Win32 调 `ChangeDisplaySettingsEx`**：功能可用，但每次启动要付 PowerShell 本身约 1.5-2 秒的启动开销，且弹窗效果受控制台限制。
2. **PowerShell + WinForms 自绘卡片**：绕开了控制台字体问题，但圆角只能靠 `Region` 裁、没有阴影。
3. **C# 编译成 exe + WPF 卡片**（当前版本）：启动最快、样式最好，也是唯一没有多余依赖的形态。

中途曾实现过「托盘常驻 + 可自定义全局快捷键」的版本，因为需要后台进程被砍掉，相关代码已删除（机器上也没有留下自启项）。如果将来要做，思路是：`RegisterHotKey` + 隐藏消息窗口 + `HwndSource` 钩子，配合 `NotifyIcon` 托盘图标。
