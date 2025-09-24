using System;
using System.Diagnostics;
using System.IO;

public class DependencyChecker
{
    // Try to find a usable Python executable
    public static string GetPythonPath()
    {
        // 1️⃣ Check common installation folder for current user
        string userPythonBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Python"
        );

        if (Directory.Exists(userPythonBase))
        {
            foreach (var dir in Directory.GetDirectories(userPythonBase, "Python*"))
            {
                string exePath = Path.Combine(dir, "python.exe");
                if (CheckPythonInstalled(exePath))
                    return exePath;
            }
        }

        // 2️⃣ Fallback to python in PATH
        if (CheckPythonInstalled("python")) return "python";
        if (CheckPythonInstalled("python3")) return "python3";

        // 3️⃣ Not found
        return null;
    }

    // Check if a specific Python path works
    public static bool CheckPythonInstalled(string pythonPath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process proc = Process.Start(psi))
            {
                proc.WaitForExit(2000);
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    // Check if vex-tm-bridge is installed using a specific Python path
    public static bool CheckTmBridgeInstalled(string pythonPath)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "-c \"import vex_tm_bridge\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process proc = Process.Start(startInfo))
            {
                proc.WaitForExit(3000); // 3 seconds
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}