// FluentWeatherDirect.cs
//
// The program behind FluentWeather-Setup.exe and FluentWeather-Uninstall.exe.
// Make-DirectInstaller.ps1 compiles this file twice with the C# compiler that ships with Windows:
//   without symbols       FluentWeather-Setup.exe      installs the app that was built on the sender's PC
//   /define:UNINSTALL     FluentWeather-Uninstall.exe  removes everything the setup added
// The files to install are embedded as resources named "payload/<file name>".
//
// Both programs extract their files to a new temporary folder, run the PowerShell script among
// them as the signed-in user, wait for it, and delete the temporary folder. The scripts ask for
// administrator approval only for the step that needs it (the signing certificate).
//
// Language level: C# 5, so the compiler built into Windows can build it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

#if UNINSTALL
[assembly: AssemblyTitle("FluentWeather uninstaller")]
[assembly: AssemblyDescription("Removes FluentWeather (Local), its tray icons and its signing certificate")]
#else
[assembly: AssemblyTitle("FluentWeather setup")]
[assembly: AssemblyDescription("Installs FluentWeather (Local) with its tray icons")]
#endif
[assembly: AssemblyProduct("FluentWeather (Local)")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace FluentWeatherLocal
{
    internal static class DirectSetup
    {
        private const string Title = "FluentWeather (Local)";
        private const string Prefix = "payload/";
#if UNINSTALL
        private const string Script = "Uninstall-FluentWeather.ps1";
#else
        private const string Script = "Install-FluentWeather.ps1";
#endif

        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();

            List<string> names = GetPayloadNames();
            if (!names.Contains(Script))
            {
                MessageBox.Show("This file is incomplete. " + Script + " is missing.", Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

#if UNINSTALL
            string question =
                "This removes FluentWeather (Local) from this PC: the app with its saved locations and " +
                "settings, the tray icons, the Start menu items and the signing certificate.\n\n" +
                "Windows asks for administrator approval once, to remove the certificate.\n\n" +
                "Remove FluentWeather now?";
#else
            string question =
                "This installs FluentWeather (Local), a weather app with saved locations and tray icons. " +
                "It is FluentWeather by Gabboxl with features added by duedilly.\n\n" +
                "Setup adds the app's signing certificate to this PC, so Windows accepts the app. " +
                "Windows asks for administrator approval once for that step.\n\n" +
                "README.txt next to this file explains every change. FluentWeather-Uninstall.exe, or " +
                "Settings > Apps, removes everything again.\n\n" +
                "Install now?";
#endif
            if (MessageBox.Show(question, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return 0;

            string folder = Path.Combine(Path.GetTempPath(), "FluentWeather-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            int code;
            try
            {
                Extract(names, folder);
                code = RunScript(folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stopped with an error:\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                code = 1;
            }
            finally
            {
                TryDelete(folder);
            }

#if UNINSTALL
            if (code == 0)
            {
                MessageBox.Show("FluentWeather (Local) is removed.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RemoveInstallFolderAfterExit();
            }
#else
            if (code == 0)
                MessageBox.Show("FluentWeather (Local) is installed and opens now.\n\n" +
                    "Start menu > FluentWeather has the app, the tray icons, the readme and the uninstaller.\n\n" +
                    "The tray icons are in the notification area. If you do not see them, click the ^ arrow " +
                    "next to the clock.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
#endif
            return code;
        }

        private static List<string> GetPayloadNames()
        {
            var list = new List<string>();
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!resource.StartsWith(Prefix, StringComparison.Ordinal))
                    continue;
                string name = resource.Substring(Prefix.Length);
                // Plain file names only. This blocks paths such as "..\x" or "C:\x".
                if (name.Length == 0 || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    continue;
                list.Add(name);
            }
            return list;
        }

        private static void Extract(List<string> names, string folder)
        {
            Directory.CreateDirectory(folder);
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string name in names)
            {
                using (Stream input = assembly.GetManifestResourceStream(Prefix + name))
                using (FileStream output = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write))
                {
                    input.CopyTo(output);
                }
            }
        }

        // Runs the script as the signed-in user, in a visible window, and waits for it.
        // The setup must not run as administrator: Windows installs an app for the user who installs it.
        private static int RunScript(string folder)
        {
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(powershell,
                "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(folder, Script) + "\"");
            info.UseShellExecute = false;
            info.WorkingDirectory = folder;
            using (Process process = Process.Start(info))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static void TryDelete(string folder)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                    return;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                System.Threading.Thread.Sleep(1000);
            }
        }

#if UNINSTALL
        // The installed copy of this program runs from the install folder, so it cannot delete that
        // folder itself. A hidden command waits 3 seconds, until this program has ended, and deletes it.
        private static void RemoveInstallFolderAfterExit()
        {
            string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\FluentWeather");
            string myDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(installDir))
                return;
            if (!string.Equals(Path.GetFullPath(myDir).TrimEnd('\\'), Path.GetFullPath(installDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return;

            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                "/c ping -n 4 127.0.0.1 >nul & rmdir /s /q \"" + installDir + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WorkingDirectory = Path.GetTempPath();
            Process.Start(info);
        }
#endif
    }
}
