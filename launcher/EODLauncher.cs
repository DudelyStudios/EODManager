using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

internal static class Program
{
    private const string AppVersion = "0.0.0-PLACEHOLDER";
    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    private static int Main()
    {
        try
        {
            ClearPyInstallerEnvironment();
            string home = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            Environment.SetEnvironmentVariable("EODMANAGER_HOME", home);
            Environment.SetEnvironmentVariable("PYINSTALLER_RESET_ENVIRONMENT", "1");

            string payloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EODManager",
                "runtime");
            Directory.CreateDirectory(payloadDir);
            string payload = Path.Combine(payloadDir, "EODRuntime.exe");
            string stamp = Path.Combine(payloadDir, "version.txt");
            ExtractPayload(payload, stamp);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = payload;
            psi.WorkingDirectory = home;
            psi.UseShellExecute = false;
            foreach (string key in CollectPyInstallerKeys(psi.EnvironmentVariables.Keys))
            {
                psi.EnvironmentVariables.Remove(key);
            }
            psi.EnvironmentVariables["EODMANAGER_HOME"] = home;
            psi.EnvironmentVariables["PYINSTALLER_RESET_ENVIRONMENT"] = "1";
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.Message, "EOD Manager", MbOk | MbIconError);
            return 1;
        }
    }

    private static void ExtractPayload(string payload, string stamp)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream src = OpenPayload(asm))
        {
            bool sameVersion = File.Exists(stamp)
                && File.ReadAllText(stamp).Trim() == AppVersion
                && File.Exists(payload)
                && new FileInfo(payload).Length == src.Length;
            if (sameVersion)
            {
                return;
            }

            Exception last = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    using (FileStream dst = File.Create(payload))
                    {
                        src.Position = 0;
                        src.CopyTo(dst);
                    }
                    File.WriteAllText(stamp, AppVersion);
                    return;
                }
                catch (IOException ex)
                {
                    last = ex;
                    Thread.Sleep(500);
                }
            }
            throw new IOException("Could not install the EOD runtime. Close every EODManager window and start it again.", last);
        }
    }

    private static Stream OpenPayload(Assembly asm)
    {
        string[] names = asm.GetManifestResourceNames();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (name.EndsWith("EODInner.exe", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Stream stream = asm.GetManifestResourceStream(name);
                if (stream != null)
                {
                    return stream;
                }
            }
        }
        throw new InvalidOperationException("EOD Manager payload is missing from the launcher.");
    }

    private static void ClearPyInstallerEnvironment()
    {
        foreach (string key in CollectPyInstallerKeys(Environment.GetEnvironmentVariables().Keys))
        {
            Environment.SetEnvironmentVariable(key, null);
        }
        Environment.SetEnvironmentVariable("PYINSTALLER_RESET_ENVIRONMENT", "1");
    }

    private static string[] CollectPyInstallerKeys(ICollection keys)
    {
        ArrayList names = new ArrayList();
        foreach (object item in keys)
        {
            string name = item as string;
            if (IsPyInstallerKey(name))
            {
                names.Add(name);
            }
        }
        return (string[])names.ToArray(typeof(string));
    }

    private static bool IsPyInstallerKey(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        return name.StartsWith("_MEI", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("_PYI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "TCL_LIBRARY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "TK_LIBRARY", StringComparison.OrdinalIgnoreCase);
    }
}
