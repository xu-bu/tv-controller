using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace TvController;

public sealed partial class MainWindow : Window
{
    private readonly TvPower _power;
    private readonly StringBuilder _log = new();

    public MainWindow()
    {
        InitializeComponent();
        _power = new TvPower(AppendLog);
        Title = "电视遥控";
        SystemBackdrop = new MicaBackdrop();
        ApplyWindowIcon();
        ResizeToDip(440, 750);
    }

    private void ApplyWindowIcon()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TvController");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "app.ico");
            using var src = typeof(App).Assembly.GetManifestResourceStream("TvController.Assets.app.ico");
            if (src is null)
            {
                return;
            }

            using (var dst = File.Create(path))
            {
                src.CopyTo(dst);
            }

            AppWindow.SetIcon(path);
        }
        catch
        {
            // 图标只是装饰，写文件失败不应阻止启动
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void ResizeToDip(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0)
        {
            scale = 1.0;
        }

        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }

    private async void WakeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.WakeAndOpenBilibiliAsync());
    }

    private async void SleepButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.SleepAsync());
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.BackAsync());
    }

    private async void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.HomeAsync());
    }

    private async void VolumeUpButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.VolumeUpAsync());
    }

    private async void VolumeDownButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.VolumeDownAsync());
    }

    private async void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _power.ToggleMuteAsync());
    }

    private async Task RunAsync(Func<Task<string>> action)
    {
        SetBusy(true, "执行中…");
        try
        {
            StatusText.Text = await action();
        }
        catch (Exception ex)
        {
            StatusText.Text = "失败：" + ex.Message;
            AppendLog("ERROR " + ex.Message);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? status)
    {
        WakeButton.IsEnabled = !busy;
        SleepButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy;
        HomeButton.IsEnabled = !busy;
        VolumeUpButton.IsEnabled = !busy;
        VolumeDownButton.IsEnabled = !busy;
        MuteButton.IsEnabled = !busy;
        BusyRing.IsActive = busy;
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }

    private void AppendLog(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _log.AppendLine(line);
            LogText.Text = _log.ToString();
        });
    }
}
