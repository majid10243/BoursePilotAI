using System;
using System.IO;
using System.Windows;

namespace BoursePilotAI
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (sender, e) =>
            {
                WriteLog(e.Exception);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    WriteLog(ex);
            };
        }

        private static void WriteLog(Exception ex)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BoursePilotAI");

                Directory.CreateDirectory(path);
                File.AppendAllText(
                    Path.Combine(path, "error.log"),
                    $"{DateTime.Now}\n{ex}\n----------------\n");
            }
            catch
            {
            }
        }
    }
}
