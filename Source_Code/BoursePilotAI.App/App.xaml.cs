using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using BoursePilotAI.Models;

namespace BoursePilotAI
{
    public partial class App : Application
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public App()
        {
            try
            {
                TsetmcOptions = LoadTsetmcOptions();
                TsetmcOptions.Validate();
            }
            catch (Exception ex)
            {
                WriteLog(ex);
                TsetmcOptions = new TsetmcOptions();
            }

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

        /// <summary>
        /// TSETMC endpoint configuration loaded from appsettings.json at startup.
        /// </summary>
        public static TsetmcOptions TsetmcOptions { get; private set; } = new TsetmcOptions();

        private static TsetmcOptions LoadTsetmcOptions()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                return new TsetmcOptions();

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty(TsetmcOptions.SectionName, out var section))
                return new TsetmcOptions();

            var options = section.Deserialize<TsetmcOptions>(JsonOptions) ?? new TsetmcOptions();
            return options;
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
