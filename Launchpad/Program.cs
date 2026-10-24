using System.Windows.Forms;

namespace Launchpad;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        CrashLog.Clear();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            CrashLog.Write($"UI thread exception: {args.Exception}");
            MessageBox.Show(
                $"Launchpad error:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Stand Launchpad — Unhandled Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            CrashLog.Write($"Fatal exception: {args.ExceptionObject}");
            MessageBox.Show(
                $"Fatal error:\n\n{args.ExceptionObject}",
                "Stand Launchpad — Fatal Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.Run(new MainForm());
    }
}
