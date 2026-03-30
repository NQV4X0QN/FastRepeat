using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

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

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // ── Self-install: copy to %LOCALAPPDATA%\FastRepeat if needed ──────
        var currentExe = Application.ExecutablePath;
        if (!IsRunningFromInstallDir(currentExe))
        {
            try
            {
                Directory.CreateDirectory(InstallDir);
                File.Copy(currentExe, InstalledExePath, overwrite: true);
                CreateStartMenuShortcut();

                // Relaunch from the installed location
                Process.Start(new ProcessStartInfo(InstalledExePath)
                {
                    UseShellExecute = true
                });
                return; // exit the current (non-installed) instance
            }
            catch (Exception ex)
            {
                // If install fails, just continue running from the current location
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
    /// Uses a VBScript shim since .NET doesn't have native shortcut creation.
    /// </summary>
    private static void CreateStartMenuShortcut()
    {
        try
        {
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs");
            var lnkPath = Path.Combine(startMenu, "Fast Repeat.lnk");

            // Skip if shortcut already exists and points to the right place
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
}
