using System;
using System.Threading;
using System.Windows.Forms;

namespace FastRepeat;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // Enforce a single instance across all sessions
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }
}
