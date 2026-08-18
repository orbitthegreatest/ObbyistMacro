using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using ObbyistMacro.Core;
using ObbyistMacro.Macros;

namespace ObbyistMacro;

public partial class App : System.Windows.Application
{
    private static Mutex _singleInstance;
    private NotifyIcon _trayIcon;
    private MainWindow _mainWindow;
    public MacroEngine Engine { get; private set; }
    public AppSettings Settings { get; private set; }
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ObbyistMacro_crash.log"),
                    ex.ToString());
            }
            catch { }
            throw;
        }
    }

    private void StartupCore(StartupEventArgs e)
    {

        bool createdNew;
        _singleInstance = new Mutex(true, "ObbyistMacro_SingleInstance", out createdNew);
        if (!createdNew)
        {
            // Bring the already-running instance to the foreground and exit.
            try
            {
                var existing = System.Diagnostics.Process.GetProcessesByName("ObbyistMacro")
                    .FirstOrDefault(p => p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero);
                if (existing != null) Win32.SetForegroundWindow(existing.MainWindowHandle);
            }
            catch { }
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        Settings = SettingsService.Load();
        Engine = new MacroEngine(Settings);
        Engine.Notify += msg => _mainWindow?.ShowToast(msg);
        Engine.Start();

        _mainWindow = new MainWindow(Engine, Settings);
        MainWindow = _mainWindow;

        BuildTrayIcon();
        _mainWindow.Show();

        if (Settings.StartMinimized)
        {
            _mainWindow.HideToTray();
        }
    }

    private void BuildTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = ExtractAppIcon(),
            Text = "ObbyistMacro",
            Visible = true,
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open ObbyistMacro", null, (s, e) => _mainWindow.ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => _mainWindow.ShowFromTray();
        _trayIcon.BalloonTipTitle = "ObbyistMacro";
        _trayIcon.BalloonTipText = "ObbyistMacro is still running in the tray.";
    }

    public static System.Drawing.Icon ExtractAppIcon()
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObbyistMacro", "app.ico");
        if (File.Exists(local))
        {
            try { return new System.Drawing.Icon(local); }
            catch { }
        }
        try
        {
            using var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/ObbyiestMacro.ico"))?.Stream;
            if (stream != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(local));
                using var fs = File.Create(local);
                stream.CopyTo(fs);
                return new System.Drawing.Icon(local);
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    public void ShowTrayBalloon()
    {
        try
        {
            _trayIcon.ShowBalloonTip(2200, "ObbyistMacro", "ObbyistMacro is still running in the tray.", ToolTipIcon.Info);
        }
        catch { }
    }

    public void ExitApp()
    {
        try { _trayIcon?.Dispose(); } catch { }
        Engine?.Stop();
        SettingsService.Save(Settings);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _trayIcon?.Dispose(); } catch { }
        Engine?.Stop();
        SettingsService.Save(Settings);
        base.OnExit(e);
    }

    internal static class Win32
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}