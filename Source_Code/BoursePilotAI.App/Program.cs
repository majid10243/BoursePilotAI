using System;
using System.IO;
using System.Windows;

namespace BoursePilotAI;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            File.WriteAllText(path, DateTime.Now + "\n" + ex);
        }
    }
}
