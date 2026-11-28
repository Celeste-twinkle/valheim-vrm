using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace ValheimVRM
{
    internal sealed class AvatarConfigSaveException : IOException
    {
        internal AvatarConfigSaveException(string path, string stage, Exception cause)
            : base("Cannot save configuration \"" + path + "\" at " + stage + ": " + cause.Message, cause) { }
    }

    // Configuration commits share one implementation, including optional model
    // TXT files. Logging is supplied by the plugin so file tests need no Unity.
    internal static class AvatarConfigFile
    {
        internal static Action<string> Info;
        internal static Action<string> Warning;
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode,
            ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool MoveFileEx(string source, string destination, uint flags);

        internal static void LogEnvironment(string directory)
        {
            Log(Info, "CONFIG_DIRECTORY path=" + Quote(directory) + " " + Describe(directory) +
                " " + DescribeVolume(directory) + " os=" + Quote(Environment.OSVersion.ToString()) +
                " runtime=" + Environment.Version + " pointerBytes=" + IntPtr.Size);
        }

        internal static void WriteAllText(string path, string contents, string operation)
        {
            Save(path, temporary => File.WriteAllText(temporary, contents, Utf8), operation);
        }

        internal static void WriteAllLines(string path, IEnumerable<string> lines, string operation)
        {
            Save(path, temporary => File.WriteAllLines(temporary, lines, Utf8), operation);
        }

        internal static void Save(string path, Action<string> writeTemporary, string operation,
            Action<string, string, string> replaceFile = null)
        {
            string temporary = null, backup = null, stage = "resolve-path", method = null;
            bool committed = false;
            try
            {
                path = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(path);
                var token = Guid.NewGuid().ToString("N");
                temporary = path + "." + token + ".tmp";
                backup = path + "." + token + ".bak";
                Log(Info, "SAVE_START operation=" + Quote(operation) + " path=" + Quote(path) +
                    " temporary=" + Quote(temporary) + " backup=" + Quote(backup) + " target=" + Describe(path));
                stage = "create-directory";
                Directory.CreateDirectory(directory);
                stage = "write-temporary";
                writeTemporary(temporary);
                if (!File.Exists(path))
                {
                    stage = "move-new";
                    File.Move(temporary, path);
                    method = "move-new";
                }
                else
                {
                    stage = "replace";
                    method = Replace(temporary, path, backup, operation, replaceFile, ref stage);
                }
                committed = true;
                Log(Info, "SAVE_OK operation=" + Quote(operation) + " path=" + Quote(path) +
                    " method=" + method + " target=" + Describe(path));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                RestoreMissingTarget(path, backup, operation);
                Log(Warning, "SAVE_FAILED operation=" + Quote(operation) + " stage=" + stage +
                    " path=" + Quote(path) + " temporary=" + Quote(temporary) + " backup=" + Quote(backup) +
                    " target=" + Describe(path) + " temporaryState=" + Describe(temporary) + " backupState=" + Describe(backup) +
                    " directory=" + Describe(Path.GetDirectoryName(path)) +
                    " " + DescribeVolume(path) + " " + Error(ex) + "\n" + ex);
                throw new AvatarConfigSaveException(path, stage, ex);
            }
            finally
            {
                // A cleanup failure must never replace the save exception or
                // turn a committed selection into an apparent switch failure.
                Cleanup(temporary, operation);
                if (committed) Cleanup(backup, operation);
            }
        }

        static string Replace(string temporary, string path, string backup, string operation,
            Action<string, string, string> replaceFile, ref string stage)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (replaceFile == null) File.Replace(temporary, path, backup);
                    else replaceFile(temporary, path, backup);
                    return "replace-with-backup";
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    Log(Warning, "REPLACE_FAILED operation=" + Quote(operation) + " attempt=" + attempt +
                        " path=" + Quote(path) + " " + Error(ex) + " message=" + Quote(ex.Message));
                    // Do not retry a partially completed replacement, which
                    // could overwrite its recovery copy. Bound transient waits
                    // to 60 ms total, and never retry access/readonly failures.
                    if (attempt < 3 && Retryable(ex) && File.Exists(temporary) &&
                        File.Exists(path) && !File.Exists(backup))
                    {
                        Thread.Sleep(20 * attempt);
                        continue;
                    }
                    if (!CanUseWindowsMove(ex) || !File.Exists(temporary)) throw;
                    stage = "fallback-backup";
                    if (!File.Exists(backup) && File.Exists(path)) File.Copy(path, backup, false);
                    stage = "fallback-move";
                    Log(Warning, "SAVE_FALLBACK operation=" + Quote(operation) + " path=" + Quote(path) +
                        " backup=" + Quote(backup) + " method=MoveFileEx " + DescribeVolume(path));
                    // Same-directory rename with REPLACE_EXISTING and
                    // WRITE_THROUGH; never delete or truncate the old target
                    // first. This also avoids ReplaceFile's metadata merging.
                    if (!MoveFileEx(temporary, path, 0x1 | 0x8))
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new IOException("MoveFileEx: " + new Win32Exception(error).Message,
                            unchecked((int)0x80070000) | error);
                    }
                    return "windows-move-fallback";
                }
            }
        }

        static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is NotSupportedException || ex is SecurityException;

        static int Win32Error(Exception ex) => (ex.HResult & unchecked((int)0xffff0000)) == unchecked((int)0x80070000)
            ? ex.HResult & 0xffff : 0;

        static bool Retryable(Exception ex)
        {
            if (!(ex is IOException)) return false;
            int code = Win32Error(ex);
            return code == 0 || code == 32 || code == 33 || code == 1175 || code == 1176 || code == 1177;
        }

        static bool CanUseWindowsMove(Exception ex)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            if (ex is NotSupportedException) return true;
            if (!(ex is IOException)) return false;
            int code = Win32Error(ex);
            return code == 0 || code == 1 || code == 50 || code == 120 || code == 1175 || code == 1176 || code == 1177;
        }

        static void RestoreMissingTarget(string path, string backup, string operation)
        {
            if (string.IsNullOrEmpty(backup) || !File.Exists(backup) || File.Exists(path)) return;
            try
            {
                File.Move(backup, path);
                Log(Warning, "SAVE_RESTORED operation=" + Quote(operation) + " path=" + Quote(path));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Log(Warning, "RESTORE_FAILED operation=" + Quote(operation) + " path=" + Quote(path) +
                    " recoveryBackup=" + Quote(backup) + " " + Error(ex) + " message=" + Quote(ex.Message));
            }
        }

        static void Cleanup(string path, string operation)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception ex) when (IsFileError(ex))
            {
                Log(Warning, "CLEANUP_FAILED operation=" + Quote(operation) + " path=" + Quote(path) +
                    " " + Error(ex) + " message=" + Quote(ex.Message));
            }
        }

        static string Describe(string path)
        {
            if (string.IsNullOrEmpty(path)) return "missing";
            try { return "attributes=" + Quote(File.GetAttributes(path).ToString()); }
            catch (FileNotFoundException) { return "missing"; }
            catch (DirectoryNotFoundException) { return "missing"; }
            catch (Exception ex) when (IsFileError(ex)) { return "attributesUnavailable=" + Quote(Error(ex)); }
        }

        static string DescribeVolume(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                var drive = new DriveInfo(root);
                return "volume=" + Quote(root) + " filesystem=" + Quote(drive.DriveFormat) + " freeBytes=" + drive.AvailableFreeSpace;
            }
            catch (Exception ex) { return "volumeUnavailable=" + Quote(ex.GetType().Name + ": " + ex.Message); }
        }

        static string Error(Exception ex) => "exception=" + ex.GetType().Name + " hresult=0x" + ex.HResult.ToString("X8") +
            " win32=" + (Win32Error(ex) == 0 ? "unknown" : Win32Error(ex).ToString());

        static string Quote(string value) => "\"" + (value ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"";

        static void Log(Action<string> logger, string message)
        {
            try { logger?.Invoke(message); }
            catch (Exception) { } // Diagnostics do not participate in committing a file.
        }
    }
}
