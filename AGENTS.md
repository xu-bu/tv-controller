# tv-controller

WinUI 3 桌面遥控器，控制 TCL 55G60E 电视（192.168.3.126）。当前只有两个按钮：开机（从「小程序待机」唤醒）和关机（进入「小程序待机」）。

## 技术栈

- WinUI 3 via `Microsoft.WindowsAppSDK` 2.7.0（开源 UI 仓库：microsoft/microsoft-ui-xaml）
- .NET 10，TFM `net10.0-windows10.0.26100.0`
- 非打包（`WindowsPackageType=None`）+ WindowsAppSDK 自包含（`WindowsAppSDKSelfContained=true`），无需安装 Windows App Runtime
- 单窗口，无 MVVM 框架，逻辑集中在 `TvPower.cs`

## 构建与运行

```powershell
dotnet build -c Debug -p:Platform=x64
dotnet run -c Debug -p:Platform=x64
```

输出：`bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TvController.exe`

`-p:Platform=x64` 必须带，否则 WinUI 目标会报平台不支持。首次 restore 约 9 分钟（下载 WinAppSDK 包），之后增量构建约 10 秒。

发布单文件 exe（约 238 MB，内置 WinAppSDK 和 .NET 依赖）：

```powershell
dotnet publish -c Release -p:Platform=x64 -p:EnableMsixTooling=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$env:TEMP\tvc-publish"
```

`EnableMsixTooling=true` 不能省，否则报 "PublishSingleFile requires EnableMsixTooling for embedded resources.pri"。

注意：publish 输出目录**不要放在项目目录内**（如 `-o publish-single`）。项目默认项会把上一次的产物当内容打进新 bundle，exe 会逐次膨胀（238→476→715 MB）。csproj 里已用 `DefaultItemExcludes` 排除 `publish-single\**` 和 `dist\**` 兜底。

产物放在 `dist\TvController.exe`，桌面快捷方式「电视遥控.lnk」指向它。改动代码后重新发布并把 exe 复制过去即可。首次启动会解压原生库到临时目录，比后续启动慢几秒。

## 图标

- `Assets\app.ico`（多尺寸 16–256，PNG 帧）。由 `Assets\make-icon.ps1` 生成，改设计后重跑即可。
- exe 图标：csproj 的 `ApplicationIcon`。
- 标题栏图标：运行时把 EmbeddedResource 里的 ico 解压到 `%LOCALAPPDATA%\TvController\app.ico`，再 `AppWindow.SetIcon`。

## 界面 / 窗口尺寸

- 窗口尺寸在代码里按 DIP 设置（`MainWindow.ResizeToDip(440, 600)`），内部用 `GetDpiForWindow` 乘缩放系数。`AppWindow.Resize` 收的是物理像素，若直接传 DIP 值，在 150% 缩放下内容会溢出、按钮被裁掉（曾出现按钮完全不可见）。
- 布局用 `Grid` 行高 `Auto/Auto/Auto/Auto/*`：按钮行必须 Auto，不能给固定高度行留 `*`，否则会被压缩裁剪。

## 电视 / ADB 环境（2026-11-18 实测）

- 电视：TCL 55G60E，Android TV，无 GMS，armv7。
- ADB：`192.168.3.126:5555`；adb 路径 `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`（`TvPower.ResolveAdb()` 会回退到 PATH）。
- 待机模式必须选「小程序待机」（智能音箱模式）：Wi-Fi 和 ADB 保持在线，屏幕约 2 分钟后变黑。判定标志：`dumpsys window` 里出现 `SoundBoxDefaultActivity`（`com.tcl.walleve/...`）。
- 电源键 `input keyevent 26`：
  - 待机画面下按一次 → 回桌面（唤醒），约 8–12 秒生效。
  - 桌面下按一次 → 弹出 `com.tcl.shutdowndialog` 电源菜单，默认高亮「熄屏」，约 13 秒倒计时后自动执行 → 进入小程序待机。
- 绝对不要用 `keyevent 223`（SLEEP，深度休眠），会掉 Wi-Fi/ADB，且本机不支持 WOL，只能红外遥控唤醒。
- 深度待机 / 真关机无法经 Wi-Fi 唤醒，只能用红外遥控。

## 按钮语义

- 开机：`connect` → `get-state` 校验 → 若在待机画面则发 `26`，等 12 秒复核；仍在待机则重发一次；若电源菜单残留则发 `4` 关闭。
- 关机（两步，必须显式确认）：`connect` → 校验 → 未待机则发 `26` 打开电源菜单 → 轮询等 `com.tcl.shutdowndialog` 窗口出现（最多约 6 秒）→ 发 `19`（左移到最左的「熄屏」，防止焦点在「关机」上）→ 发 `66` 确认 → 轮询 `SoundBoxDefaultActivity` 最多 12 秒确认。菜单未出现就返回失败，不乱按键。
- 电源菜单有四个选项：熄屏 / 关机 / 延时关机 / 艺生活，默认高亮「熄屏」，底部有 13 秒倒计时自动进入智能音箱模式。不要依赖倒计时：焦点可能被改变，且选中「关机」会深度关机、ADB 掉线。
- 关机 = 小程序待机，不是断电。目的是保住 ADB，便于下次远程开机。

## 网络环境（影响电视联网）

- 电视全局 HTTP 代理：`192.168.3.125:17890`，由本机 `node lan-proxy.js` 转发到 Clash Verge 的 `127.0.0.1:7890`。
- 脚本位置：`C:\Users\64170\Documents\Qoder\2026-11-16\f37d96e9\tv-wake\lan-proxy.js`（另见同目录 `wake-tv.bat`、`yt\` 下的 APK 和截图）。
- 代理进程不在时，ADB 仍正常，但 SmartTube 等应用没网。Clash Verge 自身 `allow-lan: false`，所以需要这个转发进程。
- 自启：计划任务 `LanProxyForTV`（登录后延迟 20 秒，隐藏窗口，调用同目录 `start-lan-proxy.vbs`，失败自动重试 3 次）。转发器挂了 B 站也会一起断，因为全局代理对所有 App 生效。
- Clash 必须保持 `rule` 模式，切到全局模式会让 B 站等国内流量也走节点。
- 电视代理里写死 `192.168.3.125`，本机 IP 变动后自启也没用，建议路由器做 DHCP 保留。

## 投屏现状（未完成）

- SmartTube 32.71（`org.smarttube.stable`），已登录 Google 账号，走代理可正常看视频。
- 官方 YouTube TV（`com.google.android.youtube.tv`）已装但无 GMS 无法联网，不可用。
- 「设置 → 遥控 → 添加设备」生成电视配对码失败，弹窗 `fromNullable result is null`（无 GMS 限制）。
- DIAL（同页「在 YouTube 投射列表中显示」）：开关打开后**必须重启应用/服务**才生效（服务启动时才读开关）。重启后已验证：TCP 8090 正常，`http://192.168.3.126:8090/dd.xml` 返回设备名 `TCL 55G60E-9D92 (SmartTube)`。
- 手机 YouTube 投屏列表此前搜不到电视；桌面重启 SmartTube 后尚未复测。下一步：手机重试；仍不行则关手机 Clash 30 秒复测（ClashMetaForAndroid 的 TUN 可能拦局域网；官方不给订阅加规则的 UI，只能自带完整 YAML 导入）。

## 代码约定

- 保持极简，不引入 MVVM / DI / 日志框架。
- 界面文案中文；注释只在非显而易见处写。
- 设备地址、ADB 路径等常量在 `TvPower.cs`。
- 所有 ADB 调用有超时保护，UI 线程不阻塞（按钮在异步执行期间禁用）。
