using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ValheimVRM;

static class ConfigurationSaveTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    static AvatarConfigSaveException Fails(Action action)
    {
        try { action(); }
        catch (AvatarConfigSaveException ex) { return ex; }
        throw new Exception("Expected configuration save failure.");
    }

    static IOException NativeFailure(int code) => new IOException("Injected OS replacement failure", unchecked((int)0x80070000) | code);

    internal static void Run(string root)
    {
        Directory.CreateDirectory(root);
        var previousInfo = AvatarConfigFile.Info;
        var previousWarning = AvatarConfigFile.Warning;
        var logs = new List<string>();
        AvatarConfigFile.Info = logs.Add;
        AvatarConfigFile.Warning = logs.Add;
        try
        {
            var path = Path.Combine(root, "配置.json");
            for (int i = 0; i < 12; i++) AvatarConfigFile.WriteAllText(path, "选择 " + i, "repeat save");
            Check(File.ReadAllText(path) == "选择 11", "Repeated create/replace did not persist the latest value.");
            Check(Directory.GetFiles(root).Length == 1, "Successful commits leaked temporary/backup files.");
            Check(logs.Any(s => s.Contains("SAVE_OK") && s.Contains("replace-with-backup")), "Replacement success was not diagnosed.");

            int attempts = 0;
            AvatarConfigFile.Save(path, p => File.WriteAllText(p, "after transient lock"), "transient lock", (source, target, backup) => {
                if (++attempts == 1) throw NativeFailure(32);
                File.Replace(source, target, backup);
            });
            Check(attempts == 2 && File.ReadAllText(path) == "after transient lock", "Transient lock was not retried successfully.");
            Check(logs.Any(s => s.Contains("win32=32") && s.Contains("REPLACE_FAILED")), "Native failure code was not recorded.");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                attempts = 0;
                AvatarConfigFile.Save(path, p => File.WriteAllText(p, "fallback Unicode 中文"), "1175 fallback", (source, target, backup) => {
                    attempts++; throw NativeFailure(1175);
                });
                Check(attempts == 3 && File.ReadAllText(path) == "fallback Unicode 中文", "Error 1175 did not recover through Windows move fallback.");
                Check(logs.Any(s => s.Contains("win32=1175")) && logs.Any(s => s.Contains("method=windows-move-fallback")),
                    "Fallback diagnostics do not identify the reported Windows failure and successful recovery.");
                Check(Directory.GetFiles(root).Length == 1, "Successful fallback leaked its recovery backup.");

                AvatarConfigFile.Save(path, p => File.WriteAllText(p, "unsupported replacement recovered"), "unsupported fallback",
                    (source, target, backup) => { throw new PlatformNotSupportedException("Injected filesystem limitation"); });
                Check(File.ReadAllText(path) == "unsupported replacement recovered", "Unsupported replace had no Windows fallback.");

                attempts = 0;
                AvatarConfigFile.Save(path, p => File.WriteAllText(p, "partial replacement recovered"), "partial fallback", (source, target, backup) => {
                    attempts++; File.Move(target, backup); throw NativeFailure(1177);
                });
                Check(attempts == 1 && File.ReadAllText(path) == "partial replacement recovered", "Partial replace was retried or lost the pending value.");
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string original = File.ReadAllText(path);
                File.SetAttributes(path, FileAttributes.ReadOnly);
                try
                {
                    var error = Fails(() => AvatarConfigFile.WriteAllText(path, "must not overwrite readonly", "readonly save"));
                    Check(File.ReadAllText(path) == original && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0,
                        "Readonly failure changed the old contents/attributes.");
                    Check(error.Message.Contains(Path.GetFullPath(path)) && logs.Any(s => s.Contains("SAVE_FAILED") && s.Contains("ReadOnly")),
                        "Readonly failure omitted its absolute path or file attributes.");
                }
                finally { File.SetAttributes(path, FileAttributes.Normal); }

                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Fails(() => AvatarConfigFile.WriteAllText(path, "must not overwrite locked", "locked save"));
                    Check(File.ReadAllText(path) == original, "A held-open target was truncated/deleted.");
                }
                AvatarConfigFile.WriteAllText(path, "after handle release", "unlocked save");
                Check(File.ReadAllText(path) == "after handle release", "Retry after releasing the handle did not persist.");
            }

            var obstruction = Path.Combine(root, "not-a-directory");
            File.WriteAllText(obstruction, "fixture");
            var directoryFailure = Fails(() => AvatarConfigFile.WriteAllText(Path.Combine(obstruction, "options.json"), "value", "directory save"));
            Check(directoryFailure.Message.Contains("create-directory") && logs.Any(s => s.Contains("stage=create-directory")),
                "Directory creation failure had no stage diagnostic.");

            AvatarConfigFile.WriteAllText(path, "old before partial failure", "prepare rollback");
            attempts = 0;
            Fails(() => AvatarConfigFile.Save(path, p => File.WriteAllText(p, "pending"), "rollback", (source, target, backup) => {
                attempts++; File.Move(target, backup); File.Delete(source); throw NativeFailure(1177);
            }));
            Check(attempts == 1 && File.ReadAllText(path) == "old before partial failure", "Missing target was not restored from its recovery copy.");
            Check(logs.Any(s => s.Contains("SAVE_RESTORED")), "Rollback was not recorded.");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string leftover = null;
                var originalFailure = NativeFailure(112);
                try
                {
                    var failure = Fails(() => AvatarConfigFile.Save(path, p => {
                        leftover = p; File.WriteAllText(p, "incomplete"); File.SetAttributes(p, FileAttributes.ReadOnly); throw originalFailure;
                    }, "cleanup failure"));
                    Check(ReferenceEquals(failure.InnerException, originalFailure) && failure.Message.Contains("write-temporary"),
                        "Cleanup masked the original write failure.");
                    Check(logs.Any(s => s.Contains("CLEANUP_FAILED")), "Failed temporary cleanup was not diagnosed.");
                }
                finally { if (leftover != null && File.Exists(leftover)) { File.SetAttributes(leftover, FileAttributes.Normal); File.Delete(leftover); } }
            }

            var models = Path.Combine(root, "models");
            var config = Path.Combine(root, "preferences");
            Directory.CreateDirectory(models);
            foreach (var name in new[] { "Old", "New" }) File.WriteAllText(Path.Combine(models, name + ".vrm"), "fixture");
            var catalog = new AvatarCatalog(models, config); catalog.Refresh(); catalog.Select("Player", "Old"); catalog.Select("Other", "Old");
            var selections = Path.Combine(config, "avatar_selections.json");
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                File.SetAttributes(selections, FileAttributes.ReadOnly);
                try
                {
                    Fails(() => catalog.Select("Player", "New"));
                    Check(catalog.Resolve("Player") == "Old", "Transactional Select changed memory after a failed save.");
                    Fails(() => catalog.SelectForSession("Player", "New"));
                    Check(catalog.Resolve("Player") == "New" && catalog.Resolve("Other") == "Old", "Failed session persistence lost the applied choice or changed another character.");
                    var restart = new AvatarCatalog(models, config); restart.Refresh(); restart.LoadSelections();
                    Check(restart.Resolve("Player") == "Old", "Failed persistence unexpectedly changed the disk selection.");
                    Fails(() => catalog.SelectForSession("Player", AvatarCatalog.OriginalModel));
                    Check(catalog.IsOriginalSelected("Player"), "Native choice was lost after failed session persistence.");
                }
                finally { File.SetAttributes(selections, FileAttributes.Normal); }
            }
            catalog.SelectForSession("Player", "New"); catalog.LoadSelections();
            Check(catalog.Resolve("Player") == "New", "Restored access did not persist the current selection.");

            AvatarConfigFile.Info = message => { throw new Exception("Logger unavailable"); };
            AvatarConfigFile.WriteAllText(path, "logger-independent commit", "logger save");
            Check(File.ReadAllText(path) == "logger-independent commit", "Diagnostics participated in committing the file.");
            Check(!Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "File-save regressions leaked temporary files.");
            Console.WriteLine("PASS: repeated saves, bounded retry, directory diagnostics, rollback and logger isolation; Windows probes cover 1175/unsupported/partial replacement recovery, readonly/locked target protection, cleanup diagnostics, and session/native selection after failed persistence.");
        }
        finally { AvatarConfigFile.Info = previousInfo; AvatarConfigFile.Warning = previousWarning; }
    }
}
