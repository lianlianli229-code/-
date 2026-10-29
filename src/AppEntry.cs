using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using StickyTickets.Data;
using StickyTickets.Interop;
using StickyTickets.UI;

[assembly: AssemblyTitle("任务便签")]
[assembly: AssemblyProduct("任务便签")]
[assembly: AssemblyDescription("任务便签 — 汉堡店挂单式桌面任务便签")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace StickyTickets
{
    public static class AppEntry
    {
#if CLEAN_RELEASE
        private const string LocalSingleInstanceMutexName = @"Local\TaskStickyNotes_CleanRelease_Mutex_v1";
        private const string AppDataFolderName = "TaskStickyNotes";
#else
        private const string LocalSingleInstanceMutexName = @"Local\StickyTickets_SingleInstance_Mutex_v2";
        private const string AppDataFolderName = "StickyTickets";
#endif

        [STAThread]
        public static int Main(string[] args)
        {
            Win32Interop.HardenProcessDllSearchPath();

            if (args != null && args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return Tests.SelfTestRunner.RunAllTests();
            }

            bool createdNew;
            using (Mutex singleInstanceMutex = new Mutex(true, LocalSingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("任务便签已在运行中。", "任务便签", MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }

                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppDataFolderName);
                string dbPath = Path.Combine(appDataDir, "sticky_tickets.db");

                using (TicketRepository repo = new TicketRepository(dbPath))
                {
                    Application app = new Application();
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    TicketRailWindow rail = new TicketRailWindow(repo);
                    app.Run(rail);
                }
                return 0;
            }
        }
    }
}
