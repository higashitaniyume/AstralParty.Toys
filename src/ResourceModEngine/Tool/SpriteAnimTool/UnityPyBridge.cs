using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ResourceModLoader.Tool.SpriteAnimTool
{
    internal static class UnityPyBridge
    {
        private const string ProgressPrefix = "[RML_PROGRESS]";

        public static bool TryExport(string bundlePath, string exportRoot, out string message)
        {
            return TryRun("export", bundlePath, exportRoot, out _, out message, "--unique-names");
        }

        public static bool TryImport(string bundlePath, string imagesRoot, string outBundle, bool preserveTimeline, out string message)
        {
            var extraArgs = preserveTimeline
                ? new[] { "--preserve-timeline" }
                : Array.Empty<string>();
            return TryRun(
                "import-clips",
                bundlePath,
                imagesRoot,
                outBundle,
                out message,
                extraArgs);
        }

        private static bool TryRun(
            string command,
            string bundlePath,
            string workingPath,
            out string scriptPath,
            out string message,
            params string[] extraArgs)
        {
            scriptPath = string.Empty;
            message = string.Empty;

            string? resolvedScriptPath = ResolveUnityPyScriptPath(out var resolveMessage, bundlePath, workingPath);
            if (string.IsNullOrWhiteSpace(resolvedScriptPath))
            {
                message = resolveMessage;
                return false;
            }

            scriptPath = resolvedScriptPath;

            var failures = new List<string>();
            foreach (var launcher in EnumeratePythonLaunchers(bundlePath, workingPath, resolvedScriptPath))
            {
                if (TryRunProcess(
                    launcher.fileName,
                    launcher.prefixArgs,
                    resolvedScriptPath,
                    command,
                    bundlePath,
                    workingPath,
                    extraArgs,
                    out var runMessage))
                {
                    message = runMessage;
                    return true;
                }

                failures.Add($"{launcher.displayName}: {runMessage}");
            }

            message = failures.Count > 0
                ? string.Join(" | ", failures)
                : "未找到可用的 Python 解释器";
            return false;
        }

        private static bool TryRun(
            string command,
            string bundlePath,
            string imagesRoot,
            string outBundle,
            out string message,
            params string[] extraArgs)
        {
            message = string.Empty;

            string? resolvedScriptPath = ResolveUnityPyScriptPath(out var resolveMessage, bundlePath, imagesRoot, outBundle);
            if (string.IsNullOrWhiteSpace(resolvedScriptPath))
            {
                message = resolveMessage;
                return false;
            }

            var failures = new List<string>();
            foreach (var launcher in EnumeratePythonLaunchers(bundlePath, imagesRoot, outBundle, resolvedScriptPath))
            {
                var args = new List<string>
                {
                    "-o",
                    outBundle
                };
                args.AddRange(extraArgs);

                if (TryRunProcess(
                    launcher.fileName,
                    launcher.prefixArgs,
                    resolvedScriptPath,
                    command,
                    bundlePath,
                    imagesRoot,
                    args.ToArray(),
                    out var runMessage))
                {
                    message = runMessage;
                    return true;
                }

                failures.Add($"{launcher.displayName}: {runMessage}");
            }

            message = failures.Count > 0
                ? string.Join(" | ", failures)
                : "未找到可用的 Python 解释器";
            return false;
        }

        private static bool TryRunProcess(
            string pythonFileName,
            string[] prefixArgs,
            string scriptPath,
            string command,
            string bundlePath,
            string workingPath,
            string[] extraArgs,
            out string message)
        {
            message = string.Empty;

            try
            {
                using var process = new Process();
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                var sync = new object();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = pythonFileName,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in prefixArgs)
                    process.StartInfo.ArgumentList.Add(arg);

                process.StartInfo.ArgumentList.Add(scriptPath);
                process.StartInfo.ArgumentList.Add(command);
                process.StartInfo.ArgumentList.Add(bundlePath);
                process.StartInfo.ArgumentList.Add(workingPath);

                foreach (var arg in extraArgs)
                    process.StartInfo.ArgumentList.Add(arg);

                process.OutputDataReceived += (_, eventArgs) =>
                    HandleProcessLine(command, eventArgs.Data, isError: false, stdout, sync);
                process.ErrorDataReceived += (_, eventArgs) =>
                    HandleProcessLine(command, eventArgs.Data, isError: true, stderr, sync);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(300000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    message = $"执行 UnityPy {command} 超时";
                    return false;
                }

                process.WaitForExit();

                string output = string.Join(Environment.NewLine,
                    new[] { stdout.ToString().Trim(), stderr.ToString().Trim() }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                if (process.ExitCode != 0)
                {
                    message = string.IsNullOrWhiteSpace(output)
                        ? $"退出码 {process.ExitCode}"
                        : $"退出码 {process.ExitCode}, 输出: {output}";
                    return false;
                }

                message = string.IsNullOrWhiteSpace(output)
                    ? $"UnityPy {command} 成功"
                    : output;
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static void HandleProcessLine(
            string command,
            string? line,
            bool isError,
            StringBuilder builder,
            object sync)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            lock (sync)
            {
                builder.AppendLine(line);
            }

            if (TryHandleProgressLine(line))
                return;

            if (isError)
                Log.Warn($"[UnityPy:{command}] {line}");
            else
                Log.Info($"[UnityPy:{command}] {line}");
        }

        private static bool TryHandleProgressLine(string line)
        {
            if (!line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
                return false;

            string payload = line.Substring(ProgressPrefix.Length).Trim();
            string[] parts = payload.Split('|', 3);
            if (parts.Length < 2)
                return false;

            string kind = parts[0].Trim();
            int value = 0;
            _ = int.TryParse(parts[1].Trim(), out value);
            string desc = parts.Length >= 3 ? parts[2].Trim() : string.Empty;

            if (kind.Equals("SETUP", StringComparison.OrdinalIgnoreCase))
            {
                Log.SetupProgress(value);
                if (!string.IsNullOrWhiteSpace(desc))
                    Log.StepProgress(desc, 0);
                return true;
            }

            if (kind.Equals("STEP", StringComparison.OrdinalIgnoreCase))
            {
                Log.StepProgress(desc, Math.Max(0, value));
                return true;
            }

            if (kind.Equals("DONE", StringComparison.OrdinalIgnoreCase))
            {
                Log.FinalizeProgress(string.IsNullOrWhiteSpace(desc) ? null : desc);
                return true;
            }

            return false;
        }

        private static IEnumerable<(string fileName, string[] prefixArgs, string displayName)> EnumeratePythonLaunchers(params string?[] contextPaths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static (string fileName, string[] prefixArgs, string displayName) Launcher(string fileName, string[] prefixArgs, string displayName)
                => (fileName, prefixArgs, displayName);

            string? configuredPython = Environment.GetEnvironmentVariable("RML_PYTHON");
            if (!string.IsNullOrWhiteSpace(configuredPython))
            {
                string normalized = configuredPython.Trim();
                if (seen.Add(normalized))
                    yield return Launcher(normalized, Array.Empty<string>(), normalized);
            }

            foreach (var candidatePath in EnumerateCandidatePythonPaths(contextPaths))
            {
                if (seen.Add(candidatePath))
                    yield return Launcher(candidatePath, Array.Empty<string>(), candidatePath);
            }

            foreach (var candidate in new[]
            {
                Launcher("py", new[] { "-3.12" }, "py -3.12"),
                Launcher("py", new[] { "-3.11" }, "py -3.11"),
                Launcher("py", new[] { "-3.10" }, "py -3.10"),
                Launcher("py", new[] { "-3" }, "py -3"),
                Launcher("python", Array.Empty<string>(), "python"),
                Launcher("python3", Array.Empty<string>(), "python3")
            })
            {
                string key = candidate.fileName + "|" + string.Join(" ", candidate.prefixArgs);
                if (seen.Add(key))
                    yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateCandidatePythonPaths(params string?[] contextPaths)
        {
            var candidates = new List<string>();

            void AddCandidatesFrom(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                try
                {
                    var fullPath = Path.GetFullPath(path);
                    var directory = Directory.Exists(fullPath)
                        ? new DirectoryInfo(fullPath)
                        : new FileInfo(fullPath).Directory;

                    for (int i = 0; directory != null && i < 8; i++, directory = directory.Parent)
                    {
                        candidates.Add(Path.Combine(directory.FullName, ".venv", "Scripts", "python.exe"));
                        candidates.Add(Path.Combine(directory.FullName, "venv", "Scripts", "python.exe"));
                    }
                }
                catch
                {
                }
            }

            AddCandidatesFrom(AppContext.BaseDirectory);
            AddCandidatesFrom(Environment.CurrentDirectory);
            foreach (var contextPath in contextPaths)
                AddCandidatesFrom(contextPath);

            return candidates
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(File.Exists);
        }

        private static string? ResolveUnityPyScriptPath(out string message, params string?[] contextPaths)
        {
            message = string.Empty;

            string? configuredScriptPath = Environment.GetEnvironmentVariable("RML_UNITYPY_SCRIPT");
            if (!string.IsNullOrWhiteSpace(configuredScriptPath))
            {
                string candidate = configuredScriptPath.Trim();
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);

                message = $"RML_UNITYPY_SCRIPT 指向的脚本不存在: {candidate}";
            }

            if (EmbeddedUnityPyAssets.TryPrepare(out var embeddedScriptPath, out var embeddedMessage))
                return embeddedScriptPath;

            string? looseScriptPath = FindLooseUnityPyScript(contextPaths);
            if (!string.IsNullOrWhiteSpace(looseScriptPath))
                return looseScriptPath;

            message = string.IsNullOrWhiteSpace(message)
                ? embeddedMessage
                : message + " | " + embeddedMessage;
            return null;
        }

        private static string? FindLooseUnityPyScript(params string?[] contextPaths)
        {
            var candidates = new List<string>();

            void AddCandidatesFrom(string? baseDir)
            {
                if (string.IsNullOrWhiteSpace(baseDir))
                    return;

                try
                {
                    var current = new DirectoryInfo(Path.GetFullPath(baseDir));
                    for (int i = 0; current != null && i < 8; i++, current = current.Parent)
                    {
                        candidates.Add(Path.Combine(current.FullName, "Resources", "UnityPy", "sprite_ab_io_mesh.py"));
                    }
                }
                catch
                {
                }
            }

            AddCandidatesFrom(AppContext.BaseDirectory);
            AddCandidatesFrom(Environment.CurrentDirectory);
            foreach (var contextPath in contextPaths)
                AddCandidatesFrom(contextPath);

            return candidates
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(File.Exists);
        }
    }
}