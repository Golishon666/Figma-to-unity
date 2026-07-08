using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityMcpExporter
    {
        public const string ImportFolder = "Assets/FIGUNITY/Imports";
        public const string FrameConfigPath = "Assets/FIGUNITY/figunity.frames.json";
        private const int TimeoutMilliseconds = 420000;
        private const string PortPrefsKey = "Figunity.FigmaWsPort";
        private const string ExpectedFilePrefsKey = "Figunity.ExpectedFileName";

        public static string FigmaPort
        {
            get => EditorPrefs.GetString(PortPrefsKey, "9225");
            set => EditorPrefs.SetString(PortPrefsKey, string.IsNullOrWhiteSpace(value) ? "9225" : value.Trim());
        }

        public static string ExpectedFileName
        {
            get => EditorPrefs.GetString(ExpectedFilePrefsKey, string.Empty);
            set => EditorPrefs.SetString(ExpectedFilePrefsKey, value ?? string.Empty);
        }

        public static void Export()
        {
            EnsureFolder("Assets", "FIGUNITY");
            EnsureFolder("Assets/FIGUNITY", "Imports");

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var workerPath = Path.Combine(ResolvePackageRoot(), "Tools", "figunity-export.mjs");
            if (!File.Exists(workerPath))
            {
                throw new FileNotFoundException("FIGUNITY worker is missing.", workerPath);
            }

            var nodePath = ResolveNodePath();
            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = Quote(workerPath),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            startInfo.EnvironmentVariables["FIGMA_WS_PORT"] = FigmaPort;
            startInfo.EnvironmentVariables["FIGUNITY_OUT_DIR"] = ImportFolder;
            startInfo.EnvironmentVariables["FIGUNITY_FRAMES"] = FrameConfigPath;
            if (!string.IsNullOrWhiteSpace(ExpectedFileName))
            {
                startInfo.EnvironmentVariables["FIGUNITY_FILE_NAME"] = ExpectedFileName;
            }

            using (var process = new Process { StartInfo = startInfo })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null) stdout.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null) stderr.AppendLine(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // The worker may exit between timeout and Kill.
                    }

                    throw new TimeoutException("FIGUNITY timed out while exporting from Figma.");
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("FIGUNITY export failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
                }

                Debug.Log("FIGUNITY export completed:\n" + stdout);
            }

            AssetDatabase.Refresh();
        }

        private static string ResolvePackageRoot()
        {
            var package = PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            if (package != null && !string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                return package.resolvedPath;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var localPackage = Path.Combine(projectRoot, "Packages", "com.golishon666.figunity");
            if (Directory.Exists(localPackage))
            {
                return localPackage;
            }

            return projectRoot;
        }

        private static string ResolveNodePath()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var nodePath = Path.Combine(programFiles, "nodejs", "node.exe");
            return File.Exists(nodePath) ? nodePath : "node";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
