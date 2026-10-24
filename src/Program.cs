using System;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;

[assembly: AssemblyTitle("Elden Ring Seamless Co-op Launcher")]
[assembly: AssemblyDescription("Installe, configure et lance Seamless Co-op pour Elden Ring")]
[assembly: AssemblyProduct("ER Seamless Launcher")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace ERSeamless
{
    public static class Program
    {
        public const string Version = "1.0";

        [STAThread]
        public static int Main(string[] args)
        {
            // mode élevé ponctuel : donne l'accès en écriture au dossier du jeu puis quitte
            if (args.Length == 3 && args[0] == "--grant") return Fs.Grant(args[1], args[2]);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log.Write("crash: " + e.Exception);
                MessageBox.Show(e.Exception.Message, "Seamless Co-op Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            return app.Run(new MainUi(null).Window);
        }
    }
}
