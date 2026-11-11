#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace NekoSuneVR.EyeInstaller
{
    /// <summary>
    /// Locates - and if necessary downloads - a portable Blender next to the Unity project,
    /// then runs background Python jobs against it.
    ///
    /// The layout matches the one OutfitRetargetingSystem already uses on this machine:
    ///
    ///   &lt;ProjectRoot&gt;/BlenderTools/blender-4.0.2-windows-x64/blender.exe
    ///   &lt;ProjectRoot&gt;/BlenderLogs/blender_log_&lt;timestamp&gt;.txt
    ///
    /// so an existing install is reused as-is and nothing is downloaded twice. Blender lives
    /// outside Assets/ on purpose - Unity must never try to import several GB of runtime files.
    /// </summary>
    public static class NekoBlenderBootstrap
    {
        /// <summary>
        /// Pinned deliberately. The Python job below is written against the Blender 4.0 API
        /// (bpy 3.x era operators, 4.0 FBX exporter defaults) and is not verified on other
        /// releases, so the version is exact rather than "whatever is installed".
        /// </summary>
        public const string RequiredVersion = "4.0.2";

        private const string WindowsFolderName = "blender-4.0.2-windows-x64";
        private const string WindowsArchiveName = "blender-4.0.2-windows-x64.zip";

        private const string DownloadUrl =
            "https://download.blender.org/release/Blender4.0/blender-4.0.2-windows-x64.zip";

        private const string OverridePrefKey = "NekoSuneVR.EyeInstaller.BlenderPathOverride";

        public static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        public static string ToolsRoot
        {
            get { return Path.Combine(ProjectRoot, "BlenderTools"); }
        }

        public static string LogsRoot
        {
            get { return Path.Combine(ProjectRoot, "BlenderLogs"); }
        }

        public static string ExpectedInstallFolder
        {
            get { return Path.Combine(ToolsRoot, WindowsFolderName); }
        }

        /// <summary>User-supplied blender executable, used ahead of anything auto-detected.</summary>
        public static string PathOverride
        {
            get { return EditorPrefs.GetString(OverridePrefKey, string.Empty); }
            set { EditorPrefs.SetString(OverridePrefKey, value ?? string.Empty); }
        }

        public static string DownloadUrlForDisplay
        {
            get { return DownloadUrl; }
        }

        // ------------------------------------------------------------------
        // Discovery
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the blender executable to use, or null when nothing usable is present.
        /// Only looks inside BlenderTools (plus an explicit override) - a system-wide Blender
        /// of an unknown version is deliberately ignored.
        /// </summary>
        public static string FindBlenderExecutable()
        {
            string over = PathOverride;
            if (!string.IsNullOrEmpty(over) && File.Exists(over))
                return over;

            if (!Directory.Exists(ToolsRoot))
                return null;

            string exact = Path.Combine(ExpectedInstallFolder, ExecutableName);
            if (File.Exists(exact))
                return exact;

            // Tolerate a differently-named folder as long as it holds the right executable
            // and reports the required version when asked.
            foreach (string dir in Directory.GetDirectories(ToolsRoot))
            {
                string candidate = Path.Combine(dir, ExecutableName);
                if (File.Exists(candidate) && ReportsRequiredVersion(candidate))
                    return candidate;
            }

            return null;
        }

        private static string ExecutableName
        {
            get
            {
#if UNITY_EDITOR_WIN
                return "blender.exe";
#else
                return "blender";
#endif
            }
        }

        /// <summary>Runs <c>blender --version</c> and checks the reported version.</summary>
        public static bool ReportsRequiredVersion(string executable)
        {
            string reported = QueryVersion(executable);
            return !string.IsNullOrEmpty(reported)
                   && reported.StartsWith(RequiredVersion, StringComparison.Ordinal);
        }

        /// <summary>Returns e.g. "4.0.2", or null when Blender could not be queried.</summary>
        public static string QueryVersion(string executable)
        {
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
                return null;

            try
            {
                var info = new ProcessStartInfo(executable, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(info))
                {
                    if (process == null) return null;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(20000);

                    foreach (string line in output.Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.StartsWith("Blender ", StringComparison.OrdinalIgnoreCase))
                            continue;

                        return trimmed.Substring("Blender ".Length).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoEye] Could not query Blender version: " + ex.Message);
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Install
        // ------------------------------------------------------------------

        /// <summary>
        /// Ensures a usable Blender exists, asking the user before downloading roughly 300 MB.
        /// Returns the executable path, or null if the user declined or the install failed.
        /// </summary>
        public static string EnsureBlender(bool askFirst = true)
        {
            string existing = FindBlenderExecutable();
            if (!string.IsNullOrEmpty(existing))
                return existing;

#if !UNITY_EDITOR_WIN
            EditorUtility.DisplayDialog(
                "Blender Required",
                "Automatic download is only implemented for Windows.\n\n" +
                "Download Blender " + RequiredVersion + " yourself, extract it to:\n\n" +
                ToolsRoot + "\n\n" +
                "then point the installer at the executable using the Blender Path override.",
                "OK");
            return null;
#else
            if (askFirst)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Blender " + RequiredVersion + " Not Found",
                    "The eye tool runs its mesh work inside Blender " + RequiredVersion + ".\n\n" +
                    "Nothing was found in:\n" + ToolsRoot + "\n\n" +
                    "Download the portable build now? This is about 300 MB and is extracted next to " +
                    "your project, outside the Assets folder, so Unity never imports it.",
                    "Download",
                    "Cancel");

                if (!proceed)
                    return null;
            }

            try
            {
                Directory.CreateDirectory(ToolsRoot);
                string archivePath = Path.Combine(ToolsRoot, WindowsArchiveName);

                if (!DownloadArchive(DownloadUrl, archivePath))
                    return null;

                if (!ExtractArchive(archivePath, ToolsRoot))
                    return null;

                try
                {
                    if (File.Exists(archivePath))
                        File.Delete(archivePath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[NekoEye] Extracted fine, but the archive could not be deleted: " + ex.Message);
                }

                string executable = Path.Combine(ExpectedInstallFolder, ExecutableName);
                if (!File.Exists(executable))
                {
                    EditorUtility.DisplayDialog(
                        "Blender Install Failed",
                        "The archive was extracted but no executable was found at:\n\n" + executable,
                        "OK");
                    return null;
                }

                // Verify by asking Blender itself rather than trusting the file name.
                string reported = QueryVersion(executable);
                if (string.IsNullOrEmpty(reported) || !reported.StartsWith(RequiredVersion, StringComparison.Ordinal))
                {
                    EditorUtility.DisplayDialog(
                        "Unexpected Blender Version",
                        "Expected Blender " + RequiredVersion + " but the downloaded build reports: " +
                        (string.IsNullOrEmpty(reported) ? "(no response)" : reported) + "\n\n" +
                        "The eye script is only verified against " + RequiredVersion + ".",
                        "OK");
                    return null;
                }

                Debug.Log("[NekoEye] Installed Blender " + reported + " at " + ExpectedInstallFolder);
                return executable;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Blender Install Failed", ex.Message, "OK");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
#endif
        }

        private static bool DownloadArchive(string url, string destination)
        {
            if (File.Exists(destination))
            {
                try { File.Delete(destination); }
                catch { /* re-downloading over a locked file is reported below */ }
            }

            using (var request = UnityWebRequest.Get(url))
            {
                // Streamed straight to disk; a 300 MB download must never sit in memory.
                request.downloadHandler = new DownloadHandlerFile(destination);
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    float progress = request.downloadProgress;
                    long received = (long)request.downloadedBytes;

                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Downloading Blender " + RequiredVersion,
                        string.Format("{0:0.0} MB received", received / 1048576f),
                        progress < 0f ? 0f : progress);

                    if (cancelled)
                    {
                        request.Abort();
                        EditorUtility.ClearProgressBar();
                        Debug.Log("[NekoEye] Blender download cancelled.");
                        return false;
                    }

                    Thread.Sleep(100);
                }

                EditorUtility.ClearProgressBar();

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    EditorUtility.DisplayDialog(
                        "Blender Download Failed",
                        request.error + "\n\nURL:\n" + url + "\n\n" +
                        "You can download it manually and extract it into:\n" + ToolsRoot,
                        "OK");
                    return false;
                }
            }

            return true;
        }

        private static bool ExtractArchive(string archivePath, string destinationFolder)
        {
            EditorUtility.DisplayProgressBar(
                "Installing Blender " + RequiredVersion,
                "Extracting archive (this takes a minute)...",
                0.5f);

            try
            {
                // PowerShell's Expand-Archive is present on every supported Windows version and
                // avoids depending on System.IO.Compression being referenced by the Editor.
                string arguments = string.Format(
                    "-NoProfile -NonInteractive -Command \"Expand-Archive -LiteralPath '{0}' -DestinationPath '{1}' -Force\"",
                    archivePath.Replace("'", "''"),
                    destinationFolder.Replace("'", "''"));

                var info = new ProcessStartInfo("powershell.exe", arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(info))
                {
                    if (process == null)
                        throw new InvalidOperationException("Could not start powershell.exe to extract the archive.");

                    string stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException("Expand-Archive failed:\n" + stdErr);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Extract Failed", ex.Message, "OK");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------
        // Running jobs
        // ------------------------------------------------------------------

        public class JobResult
        {
            public bool Succeeded;
            public bool Cancelled;
            public int ExitCode;
            public string LogPath;
            public string StdOut = string.Empty;
            public string StdErr = string.Empty;

            /// <summary>Last "Error:" line the script printed, if any.</summary>
            public string ScriptError = string.Empty;
        }

        /// <summary>
        /// Runs a Python script in background Blender, showing a cancelable progress bar driven by
        /// "Status: ..." / "Progress: 0.42" lines on stdout, and writing a full log to BlenderLogs.
        /// </summary>
        public static JobResult RunPythonJob(
            string blenderExecutable,
            string scriptPath,
            IList<string> scriptArguments,
            string progressTitle,
            string logLabel)
        {
            var result = new JobResult();

            if (string.IsNullOrEmpty(blenderExecutable) || !File.Exists(blenderExecutable))
                throw new FileNotFoundException("Blender executable not found.", blenderExecutable);

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("Blender python script not found.", scriptPath);

            var argumentBuilder = new StringBuilder();
            argumentBuilder.Append("--background --factory-startup --python ");
            argumentBuilder.Append(Quote(scriptPath));
            argumentBuilder.Append(" --");
            foreach (string argument in scriptArguments)
            {
                argumentBuilder.Append(' ');
                argumentBuilder.Append(Quote(argument));
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var pending = new Queue<string>();

            var info = new ProcessStartInfo(blenderExecutable, argumentBuilder.ToString())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ProjectRoot,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            string status = "Starting Blender...";
            float progress = 0f;

            try
            {
                using (var process = new Process { StartInfo = info })
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data == null) return;
                        lock (pending)
                        {
                            stdout.AppendLine(e.Data);
                            pending.Enqueue(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data == null) return;
                        lock (pending) stderr.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.HasExited)
                    {
                        DrainPending(pending, ref status, ref progress, ref result.ScriptError);

                        if (EditorUtility.DisplayCancelableProgressBar(progressTitle, status, progress))
                        {
                            result.Cancelled = true;
                            try { process.Kill(); }
                            catch (Exception ex) { Debug.LogWarning("[NekoEye] Could not kill Blender: " + ex.Message); }
                            break;
                        }

                        Thread.Sleep(80);
                    }

                    process.WaitForExit();
                    DrainPending(pending, ref status, ref progress, ref result.ScriptError);

                    result.ExitCode = process.ExitCode;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            result.StdOut = stdout.ToString();
            result.StdErr = stderr.ToString();
            result.Succeeded = !result.Cancelled
                               && result.ExitCode == 0
                               && string.IsNullOrEmpty(result.ScriptError);

            result.LogPath = WriteLog(logLabel, argumentBuilder.ToString(), result);
            return result;
        }

        private static void DrainPending(Queue<string> pending, ref string status, ref float progress, ref string scriptError)
        {
            lock (pending)
            {
                while (pending.Count > 0)
                {
                    string line = pending.Dequeue();
                    if (line == null) continue;

                    if (line.StartsWith("Status:", StringComparison.Ordinal))
                    {
                        status = line.Substring("Status:".Length).Trim();
                    }
                    else if (line.StartsWith("Progress:", StringComparison.Ordinal))
                    {
                        float parsed;
                        if (float.TryParse(
                                line.Substring("Progress:".Length).Trim(),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out parsed))
                        {
                            progress = Mathf.Clamp01(parsed);
                        }
                    }
                    else if (line.StartsWith("Error:", StringComparison.Ordinal))
                    {
                        scriptError = line.Substring("Error:".Length).Trim();
                    }
                }
            }
        }

        private static string WriteLog(string label, string commandLine, JobResult result)
        {
            try
            {
                Directory.CreateDirectory(LogsRoot);

                string path = Path.Combine(
                    LogsRoot,
                    string.Format("neko_eye_log_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now));

                var builder = new StringBuilder();
                builder.AppendLine("=== Neko Eye Installer - Blender Execution Log ===");
                builder.AppendLine("Label: " + label);
                builder.AppendLine("Timestamp: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine("Exit code: " + result.ExitCode);
                builder.AppendLine("Cancelled: " + result.Cancelled);
                builder.AppendLine();
                builder.AppendLine("=== Command Line ===");
                builder.AppendLine(commandLine);
                builder.AppendLine();
                builder.AppendLine("=== Standard Output ===");
                builder.AppendLine(result.StdOut);
                builder.AppendLine();
                builder.AppendLine("=== Standard Error ===");
                builder.AppendLine(result.StdErr);

                File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoEye] Could not write Blender log: " + ex.Message);
                return string.Empty;
            }
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Trailing backslashes would escape the closing quote on Windows.
            string trimmed = value.TrimEnd('\\');
            int trailing = value.Length - trimmed.Length;
            return "\"" + trimmed + new string('\\', trailing * 2) + "\"";
        }
    }
}
#endif
