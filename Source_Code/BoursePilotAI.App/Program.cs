using System;
using System.IO;
using System.Windows;

namespace BoursePilotAI;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        try
        {
            File.AppendAllText(log, DateTime.Now + " Program started\n");
            var app = new App();
            File.AppendAllText(log, "App created\n");
            app.InitializeComponent();
            File.AppendAllText(log, "App initialized\n");
            app.Run();
            File.AppendAllText(log, "App closed normally\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(log, DateTime.Now + " ERROR\n" + ex + "\n");
        }
    }
}
