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

        public static void Export()
        {
            Export(FigunitySettings.LoadOrCreate());
        }

        public static void Export(FigunitySettingsAsset settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            FigunitySettings.EnsureUnityFolder(settings.importFolder);

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

            startInfo.EnvironmentVariables["FIGMA_WS_PORT"] = string.IsNullOrWhiteSpace(settings.figmaWsPort) ? "9225" : settings.figmaWsPort.Trim();
            startInfo.EnvironmentVariables["FIGUNITY_OUT_DIR"] = settings.importFolder;
            startInfo.EnvironmentVariables["FIGUNITY_FRAMES"] = settings.frameConfigPath;
            startInfo.EnvironmentVariables["FIGUNITY_RASTER_SCALE"] = Mathf.Clamp(settings.rasterScale, 1, 4).ToString();
            if (!string.IsNullOrWhiteSpace(settings.expectedFileName))
            {
                startInfo.EnvironmentVariables["FIGUNITY_FILE_NAME"] = settings.expectedFileName;
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

                UnityEngine.Debug.Log("FIGUNITY export completed:\n" + stdout);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FigunityTextureImporter.ConfigureElementTextures(settings.importFolder);
        }

        private static string ResolvePackageRoot()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
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
    }
}
