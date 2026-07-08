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
    public static class FigunityTestMenuWorkflow
    {
        private const int TimeoutMilliseconds = 420000;

        [MenuItem("Tools/FIGUNITY/Tests/Create Test Menus In Figma")]
        public static void CreateTestMenusInFigma()
        {
            RunNodeTool("figunity-create-test-menus.mjs", FigunitySettings.LoadOrCreate());
        }

        [MenuItem("Tools/FIGUNITY/Tests/Install Test Frame Config")]
        public static void InstallTestFrameConfig()
        {
            InstallTestFrameConfig(FigunitySettings.LoadOrCreate());
        }

        [MenuItem("Tools/FIGUNITY/Tests/Build Test Scene From Prefabs")]
        public static void BuildTestScene()
        {
            FigunityTestSceneBuilder.BuildScene(FigunitySettings.LoadOrCreate());
            ExitBatchmodeSuccessfully();
        }

        [MenuItem("Tools/FIGUNITY/Tests/Rebuild Prefabs And Build Test Scene")]
        public static void RebuildPrefabsAndBuildTestScene()
        {
            var settings = FigunitySettings.LoadOrCreate();
            FigunityPrefabWriter.RebuildPrefabsFromPayload(settings);
            settings = FigunitySettings.LoadOrCreate();
            FigunityTestSceneBuilder.BuildScene(settings);
            ExitBatchmodeSuccessfully();
        }

        [MenuItem("Tools/FIGUNITY/Tests/Create Export Rebuild And Build Test Scene")]
        public static void CreateExportRebuildAndBuildScene()
        {
            var settings = FigunitySettings.LoadOrCreate();
            CreateTestMenusInFigma();
            InstallTestFrameConfig(settings);
            settings = FigunitySettings.LoadOrCreate();
            FigunityMcpExporter.Export(settings);
            settings = FigunitySettings.LoadOrCreate();
            FigunityPrefabWriter.RebuildPrefabsFromPayload(settings);
            settings = FigunitySettings.LoadOrCreate();
            FigunityTestSceneBuilder.BuildScene(settings);
            ExitBatchmodeSuccessfully();
        }

        private static void InstallTestFrameConfig(FigunitySettingsAsset settings)
        {
            var packageConfig = Path.Combine(ResolvePackageRoot(), "Samples~", "Frame Config", "figunity-test-menus.frames.json");
            if (!File.Exists(packageConfig))
            {
                throw new FileNotFoundException("FIGUNITY test frame config is missing.", packageConfig);
            }

            FigunitySettings.EnsureUnityFolder(Path.GetDirectoryName(settings.frameConfigPath)?.Replace("\\", "/") ?? "Assets/FIGUNITY");
            File.Copy(packageConfig, FigunitySettings.ProjectAbsolutePath(settings.frameConfigPath), true);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("FIGUNITY installed test frame config: " + settings.frameConfigPath);
        }

        private static void RunNodeTool(string scriptName, FigunitySettingsAsset settings)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var scriptPath = Path.Combine(ResolvePackageRoot(), "Tools", scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("FIGUNITY node tool is missing.", scriptPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveNodePath(),
                Arguments = Quote(scriptPath),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables["FIGMA_WS_PORT"] = string.IsNullOrWhiteSpace(settings.figmaWsPort) ? "9225" : settings.figmaWsPort.Trim();

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
                    }

                    throw new TimeoutException("FIGUNITY node tool timed out: " + scriptName);
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("FIGUNITY node tool failed: " + scriptName + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
                }

                UnityEngine.Debug.Log("FIGUNITY node tool completed: " + scriptName + "\n" + stdout);
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
            return Directory.Exists(localPackage) ? localPackage : projectRoot;
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

        private static void ExitBatchmodeSuccessfully()
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
