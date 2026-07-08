using System;
using System.Collections.Generic;
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
            Export(settings, null);
        }

        public static void Export(FigunitySettingsAsset settings, string frameConfigPathOverride)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            FigunitySettings.EnsureUnityFolder(settings.importFolder);

            var configPath = string.IsNullOrWhiteSpace(frameConfigPathOverride) ? settings.frameConfigPath : frameConfigPathOverride;
            var stdout = RunNodeWorker(
                settings,
                "figunity-export.mjs",
                new Dictionary<string, string>
                {
                    { "FIGUNITY_OUT_DIR", settings.importFolder },
                    { "FIGUNITY_FRAMES", configPath },
                    { "FIGUNITY_RASTER_SCALE", Mathf.Clamp(settings.rasterScale, 1, 4).ToString() }
                },
                TimeoutMilliseconds,
                "FIGUNITY export");

            UnityEngine.Debug.Log("FIGUNITY export completed:\n" + stdout);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FigunityTextureImporter.ConfigureElementTextures(settings.importFolder);
        }

        internal static string RunNodeWorker(FigunitySettingsAsset settings, string workerFileName, IReadOnlyDictionary<string, string> environmentVariables, int timeoutMilliseconds, string operationName)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var workerPath = Path.Combine(ResolvePackageRoot(), "Tools", workerFileName);
            if (!File.Exists(workerPath))
            {
                throw new FileNotFoundException("FIGUNITY worker is missing.", workerPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveNodePath(),
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
            if (!string.IsNullOrWhiteSpace(settings.expectedFileName))
            {
                startInfo.EnvironmentVariables["FIGUNITY_FILE_NAME"] = settings.expectedFileName;
            }

            if (environmentVariables != null)
            {
                foreach (var pair in environmentVariables)
                {
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value ?? string.Empty;
                }
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

                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // The worker may exit between timeout and Kill.
                    }

                    throw new TimeoutException(operationName + " timed out while communicating with Figma.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(operationName + " failed.\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
                }

                return stdout.ToString();
            }
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
