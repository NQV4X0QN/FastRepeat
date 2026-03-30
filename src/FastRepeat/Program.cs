using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FastRepeat;

static class Program
{
    private static Mutex? _mutex;

    /// <summary>
    /// The canonical install directory: %LOCALAPPDATA%\FastRepeat
    /// </summary>
    public static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FastRepeat");

    /// <summary>
    /// The canonical EXE path inside the install directory.
    /// </summary>
    public static readonly string InstalledExePath = Path.Combine(InstallDir, "FastRepeat.exe");

    private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FastRepeat";
    private const string StartupRegKey   = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // ── Handle --uninstall flag ───────────────────────────────────────
        if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            RunUninstall();
            return;
        }

        // ── Self-install: copy to %LOCALAPPDATA%\FastRepeat if needed ─────
        var currentExe = Application.ExecutablePath;
        if (!IsRunningFromInstallDir(currentExe))
        {
            try
            {
                Directory.CreateDirectory(InstallDir);
                File.Copy(currentExe, InstalledExePath, overwrite: true);
                CreateStartMenuShortcut();
                RegisterUninstaller();

                // Relaunch from the installed location
                Process.Start(new ProcessStartInfo(InstalledExePath)
                {
                    UseShellExecute = true
                });
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not install to {InstallDir}:\n{ex.Message}\n\nRunning from current location.",
                    "Fast Repeat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Single instance check ─────────────────────────────────────────
        _mutex = new Mutex(true, @"Global\FastRepeat_v1", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show(
                "Fast Repeat is already running.\nCheck the system tray.",
                "Fast Repeat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.Run(new TrayApp());
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Returns true if the running EXE is already inside the install directory.
    /// </summary>
    private static bool IsRunningFromInstallDir(string exePath)
    {
        try
        {
            var exeDir  = Path.GetFullPath(Path.GetDirectoryName(exePath)!);
            var install = Path.GetFullPath(InstallDir);
            return string.Equals(exeDir, install, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>
    /// Creates a Start Menu shortcut so the app appears in search and the Start Menu.
    /// </summary>
    private static void CreateStartMenuShortcut()
    {
        try
        {
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs");
            var lnkPath = Path.Combine(startMenu, "Fast Repeat.lnk");

            if (File.Exists(lnkPath)) return;

            var vbs = Path.Combine(Path.GetTempPath(), "FastRepeat_shortcut.vbs");
            File.WriteAllText(vbs, $"""
                Set ws = CreateObject("WScript.Shell")
                Set lnk = ws.CreateShortcut("{lnkPath}")
                lnk.TargetPath = "{InstalledExePath}"
                lnk.WorkingDirectory = "{InstallDir}"
                lnk.Description = "System-wide key and mouse button repeater"
                lnk.Save
                """);

            var psi = new ProcessStartInfo("wscript.exe", $"\"{vbs}\"")
            {
                WindowStyle    = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            };
            Process.Start(psi)?.WaitForExit(5000);

            try { File.Delete(vbs); } catch { }
        }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Registers the app in Add/Remove Programs (Apps &amp; Features) so users
    /// can uninstall it from Windows Settings.
    /// </summary>
    public static void RegisterUninstaller()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegKey);
            if (key == null) return;

            key.SetValue("DisplayName",     "Fast Repeat");
            key.SetValue("DisplayVersion",  UpdateManager.CurrentVersion);
            key.SetValue("Publisher",        "NQV4X0QN");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayIcon",     InstalledExePath);
            key.SetValue("UninstallString", $"\"{InstalledExePath}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{InstalledExePath}\" --uninstall");
            key.SetValue("NoModify",  1, RegistryValueKind.DWord);
            key.SetValue("NoRepair",  1, RegistryValueKind.DWord);

            // Estimated size in KB (get actual file size)
            try
            {
                var sizeKb = (int)(new FileInfo(InstalledExePath).Length / 1024);
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
            }
            catch { }
        }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Performs a full uninstall: asks the user, removes registry entries,
    /// shortcuts, and launches a batch trampoline to delete the EXE and folder.
    /// </summary>
    private static void RunUninstall()
    {
        var result = MessageBox.Show(
            "Are you sure you want to uninstall Fast Repeat?\n\n" +
            "This will remove the application and its Start Menu shortcut.",
            "Uninstall Fast Repeat",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        // Ask about settings
        var removeSettings = MessageBox.Show(
            "Do you also want to remove your saved settings and key bindings?",
            "Remove Settings?",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        // Remove startup registry entry
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(StartupRegKey, writable: true);
            runKey?.DeleteValue("FastRepeat", throwOnMissingValue: false);
        }
        catch { }

        // Remove uninstall registry entry
        try
        {
            try { Registry.CurrentUser.DeleteSubKey(UninstallRegKey); } catch { }
        }
        catch { }

        // Remove Start Menu shortcut
        try
        {
            var lnkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "Fast Repeat.lnk");
            if (File.Exists(lnkPath)) File.Delete(lnkPath);
        }
        catch { }

        // Remove settings if requested
        if (removeSettings)
        {
            try
            {
                var settingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FastRepeat");
                if (Directory.Exists(settingsDir))
                    Directory.Delete(settingsDir, recursive: true);
            }
            catch { }
        }

        // Batch trampoline to delete the EXE and folder after process exits
        var bat = Path.Combine(Path.GetTempPath(), "FastRepeat_uninstall.bat");
        File.WriteAllText(bat, $"""
            @echo off
            ping -n 3 127.0.0.1 > nul
            del /f /q "{InstalledExePath}" 2>nul
            rmdir /q "{InstallDir}" 2>nul
            del "%~f0"
            """);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
        {
            WindowStyle    = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = true
        });

        MessageBox.Show(
            "Fast Repeat has been uninstalled.",
            "Uninstall Complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
