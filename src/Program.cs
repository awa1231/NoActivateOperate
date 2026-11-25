using System;
using System.Threading;
using System.Windows.Forms;

namespace NonActivatingOperate
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\NonActivatingOperate.Main", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Non Activating Operate is already running. Check the notification area.", "Non Activating Operate",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Native.TryEnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Log.Error("UI thread exception", e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Log.Error("unhandled exception", e.ExceptionObject as Exception);
                };

                Log.Write("---- starting (pid " + System.Diagnostics.Process.GetCurrentProcess().Id + ") ----");
                using (TrayContext context = new TrayContext())
                {
                    Application.Run(context);
                }
                Log.Write("---- stopped ----");
            }
        }
    }
}