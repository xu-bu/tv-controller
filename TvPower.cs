using System.Diagnostics;

namespace TvController;

public sealed class TvPower
{
    private const string Device = "192.168.3.126:5555";

    private const string StandbyMarker = "SoundBoxDefaultActivity";
    private const string PowerDialogMarker = "shutdowndialog";
    private const string BilibiliPackage = "com.xiaodianshi.tv.yst";

    private static readonly string Adb = ResolveAdb();
    private readonly Action<string> _log;

    public TvPower(Action<string> log)
    {
        _log = log;
    }

    public async Task<string> WakeAsync()
    {
        Log("连接 " + Device);
        await Run($"connect {Device}", 8000);

        if (!await IsOnlineAsync())
        {
            throw new InvalidOperationException("无法连接电视。电视可能处于深度待机或已关机，需要用红外遥控器开机。");
        }

        var window = await WindowDumpAsync();
        if (!window.Contains(StandbyMarker))
        {
            Log("电视已经是唤醒状态");
            return "电视已经是唤醒状态";
        }

        Log("发送电源键");
        await Run($"-s {Device} shell input keyevent 26", 5000);
        await Task.Delay(12000);

        window = await WindowDumpAsync();
        if (window.Contains(StandbyMarker))
        {
            Log("仍在待机画面，再发一次电源键");
            await Run($"-s {Device} shell input keyevent 26", 5000);
            await Task.Delay(9000);
            window = await WindowDumpAsync();
        }

        if (window.Contains(PowerDialogMarker))
        {
            Log("检测到电源菜单，按返回关闭");
            await Run($"-s {Device} shell input keyevent 4", 5000);
            await Task.Delay(2000);
            window = await WindowDumpAsync();
        }

        return window.Contains(StandbyMarker)
            ? "开机指令已发送，但电视仍停留在待机画面"
            : "电视已唤醒";
    }

    public async Task<string> SleepAsync()
    {
        Log("连接 " + Device);
        await Run($"connect {Device}", 8000);

        if (!await IsOnlineAsync())
        {
            throw new InvalidOperationException("无法连接电视，无法执行待机。");
        }

        var window = await WindowDumpAsync();
        if (window.Contains(StandbyMarker))
        {
            Log("电视已经在待机状态");
            return "电视已处于待机状态";
        }

        Log("发送电源键，打开电源菜单");
        await Run($"-s {Device} shell input keyevent 26", 5000);

        // 电源菜单默认高亮「熄屏」，菜单一出现就立刻确认，跳过左右移动和多余等待，
        // 让菜单只在屏幕上一闪而过。直接拉起待机 Activity 会被系统拒绝（not exported）。
        var dialogShown = false;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            window = await WindowDumpAsync();
            if (window.Contains(PowerDialogMarker))
            {
                dialogShown = true;
                break;
            }
        }

        if (!dialogShown)
        {
            Log("未检测到电源菜单");
            return "未能打开电源菜单，请查看电视屏幕";
        }

        Log("确认「熄屏」");
        await Run($"-s {Device} shell input keyevent 66", 5000);

        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(400);
            window = await WindowDumpAsync();
            if (window.Contains(StandbyMarker))
            {
                Log("已进入小程序待机");
                return "电视已进入待机（网络保持在线）";
            }
        }

        if (window.Contains(PowerDialogMarker))
        {
            Log("电源菜单仍在，再确认一次");
            await Run($"-s {Device} shell input keyevent 66", 5000);
            await Task.Delay(1500);
            window = await WindowDumpAsync();
            if (window.Contains(StandbyMarker))
            {
                return "电视已进入待机（网络保持在线）";
            }
        }

        return "未能确认电视进入待机，请查看电视屏幕";
    }

    public async Task<string> WakeAndOpenBilibiliAsync()
    {
        Log("连接 " + Device);
        await Run($"connect {Device}", 8000);

        if (await IsOnlineAsync())
        {
            // 小程序待机下系统仍是 Awake、ADB 在线，直接拉起应用即可点亮屏幕，
            // 无需按电源键再死等十几秒（那段时间电视早回到主页空转）。
            return await LaunchBilibiliAsync();
        }

        Log("ADB 不在线，尝试电源键唤醒");
        var wakeResult = await WakeAsync();
        var launchResult = await LaunchBilibiliAsync();
        return wakeResult + "；" + launchResult;
    }

    private async Task<string> LaunchBilibiliAsync()
    {
        Log("启动哔哩哔哩 " + BilibiliPackage);
        await Run($"-s {Device} shell am start -n {BilibiliPackage}/.ui.main.MainActivity", 5000);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(300);
            var dump = await WindowDumpAsync();
            if (IsForeground(dump, BilibiliPackage))
            {
                return "已进入哔哩哔哩";
            }
        }

        return "启动指令已发送，请查看电视屏幕";
    }

    public async Task<string> VolumeUpAsync()
    {
        return await SendNavigationKeyAsync(24, "音量+");
    }

    public async Task<string> VolumeDownAsync()
    {
        return await SendNavigationKeyAsync(25, "音量-");
    }

    public async Task<string> ToggleMuteAsync()
    {
        return await SendNavigationKeyAsync(113, "静音切换");
    }

    public async Task<string> BackAsync()
    {
        return await SendNavigationKeyAsync(4, "返回键");
    }

    public async Task<string> HomeAsync()
    {
        return await SendNavigationKeyAsync(3, "主页键");
    }

    private async Task<string> SendNavigationKeyAsync(int keyCode, string name)
    {
        Log("连接 " + Device);
        await Run($"connect {Device}", 8000);

        if (!await IsOnlineAsync())
        {
            throw new InvalidOperationException("无法连接电视，无法发送" + name + "。");
        }

        Log("发送" + name);
        await Run($"-s {Device} shell input keyevent {keyCode}", 5000);
        return name + "已发送";
    }

    private async Task<bool> IsOnlineAsync()
    {
        var state = await Run($"-s {Device} get-state", 5000);
        return state.Trim() == "device";
    }

    private Task<string> WindowDumpAsync()
    {
        return Run($"-s {Device} shell dumpsys window", 15000);
    }

    private static bool IsForeground(string dump, string package)
    {
        foreach (var line in dump.Split('\n'))
        {
            if (line.Contains("mCurrentFocus") && line.Contains(package))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<string> Run(string args, int timeoutMs)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(Adb, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            var exit = process.WaitForExitAsync();

            if (await Task.WhenAny(exit, Task.Delay(timeoutMs)) != exit)
            {
                try { process.Kill(true); } catch { }
                Log("命令超时：" + args);
                return string.Empty;
            }

            await exit;
            return await output + await error;
        }
        catch (Exception ex)
        {
            Log($"命令失败：{args}（{ex.Message}）");
            return string.Empty;
        }
    }

    private static string ResolveAdb()
    {
        var known = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Android\Sdk\platform-tools\adb.exe");

        return File.Exists(known) ? known : "adb";
    }

    private void Log(string message)
    {
        _log($"{DateTime.Now:HH:mm:ss} {message}");
    }
}
