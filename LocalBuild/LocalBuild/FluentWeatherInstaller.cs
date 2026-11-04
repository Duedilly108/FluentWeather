// FluentWeatherInstaller.cs
//
// Single-file installer for the FluentWeather local build.
// Make-Installer.cmd compiles this file with the C# compiler that ships with Windows,
// and embeds the setup files as resources named "payload/<file name>".
//
// When it runs, it asks what to do:
//   Install       extracts the files to a new temporary folder, runs Setup, waits for it,
//                 and deletes the temporary folder.
//   Extract only  writes the files to a new folder on the Desktop and opens it, so the
//                 files can be read before anything runs.
//   Cancel        does nothing.
//
// The installer contains only the setup scripts, the patch and the C# sources listed in
// Make-Installer.cmd. It contains no certificate, no settings and no saved locations.
// Each PC that runs Setup creates its own signing certificate.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("FluentWeather local build installer")]
[assembly: AssemblyDescription("Extracts and runs the FluentWeather local build setup")]
[assembly: AssemblyProduct("FluentWeather local build")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace FluentWeatherLocal
{
    internal static class Installer
    {
        private const string Title = "FluentWeather local build";
        private const string Prefix = "payload/";
        private const string SetupScript = "Setup-FluentWeatherLocal.ps1";

        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();

            List<string> names = GetPayloadNames();
            if (!names.Contains(SetupScript))
            {
                MessageBox.Show("This installer is incomplete. " + SetupScript + " is missing.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            string text =
                "This installs the FluentWeather weather app with saved locations and tray icons.\n\n" +
                "Setup builds the app on this PC from the developer's public source code " +
                "(github.com/Gabboxl/FluentWeather). It signs the app with a certificate that " +
                "Setup creates on this PC only.\n\n" +
                "If Git for Windows or Visual Studio 2026 Build Tools are missing, Setup downloads " +
                "and installs them. They need about 15 GB of disk space, and the first install can " +
                "take 30 minutes or more. Setup asks for administrator approval.\n\n" +
                "Yes: install now.\n" +
                "No: only extract the files to a new folder on your Desktop, so you can read them first.\n" +
                "Cancel: do nothing.";

            DialogResult choice = MessageBox.Show(text, Title, MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);

            try
            {
                if (choice == DialogResult.No)
                    return ExtractOnly(names);
                if (choice == DialogResult.Yes)
                    return Install(names);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("The installer stopped with an error:\n\n" + ex.Message,
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static List<string> GetPayloadNames()
        {
            var list = new List<string>();
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!resource.StartsWith(Prefix, StringComparison.Ordinal))
                    continue;

                string name = resource.Substring(Prefix.Length);
                // Accept plain file names only. This blocks paths such as "..\x" or "C:\x".
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

        private static int ExtractOnly(List<string> names)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string folder = Path.Combine(desktop, "FluentWeatherLocal");
            for (int i = 2; Directory.Exists(folder) || File.Exists(folder); i++)
                folder = Path.Combine(desktop, "FluentWeatherLocal-" + i);

            Extract(names, folder);
            Process.Start("explorer.exe", "\"" + folder + "\"");
            MessageBox.Show("The files are in:\n" + folder + "\n\nRead README.txt first. To install, double-click Setup.cmd in that folder.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static int Install(List<string> names)
        {
            string folder = Path.Combine(Path.GetTempPath(), "FluentWeatherLocal-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Extract(names, folder);

            try
            {
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    @"WindowsPowerShell\v1.0\powershell.exe");
                var info = new ProcessStartInfo(powershell,
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(folder, SetupScript) + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;          // this first copy only asks for administrator approval
                info.WorkingDirectory = folder;

                // Setup starts an administrator copy of itself in its own window, and waits for it.
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        MessageBox.Show("Setup did not finish (exit code " + process.ExitCode + "). " +
                            "If administrator approval was declined, nothing was changed.",
                            Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return process.ExitCode;
                    }
                }
                return 0;
            }
            finally
            {
                TryDelete(folder);
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
    }
}
