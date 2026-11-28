using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValheimVRM.Manager
{
    internal sealed class FileChanges : IDisposable
    {
        private sealed class Change
        {
            internal string RelativePath;
            internal string StagedPath;
            internal string BackupPath;
            internal bool NewOnly;
            internal bool Installed;
        }

        private readonly GameInstallation game;
        private readonly FileStream gate;
        private readonly string workspace;
        private readonly List<Change> changes = new List<Change>();
        private bool keepBackups;

        internal FileChanges(GameInstallation game)
        {
            this.game = game;
            workspace = game.Resolve(".valheimvrm-" + Guid.NewGuid().ToString("N"));
            try
            {
                gate = new FileStream(game.Resolve(".valheimvrm.lock"), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException error) when ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 80)
            {
                throw new IOException("此目录正在由另一个管理器操作。", error);
            }
        }

        internal void Stage(string relativePath, Action<Stream> write, bool newOnly = false)
        {
            var destination = Path.Combine(workspace, "new", relativePath);
            game.Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                write(stream);
            changes.Add(new Change { RelativePath = relativePath, StagedPath = destination, NewOnly = newOnly });
        }

        internal void Remove(string relativePath)
        {
            if (File.Exists(game.Resolve(relativePath)))
                changes.Add(new Change { RelativePath = relativePath });
        }

        internal void Commit()
        {
            var applied = new List<Change>();
            try
            {
                foreach (var change in changes)
                {
                    var destination = game.Resolve(change.RelativePath);
                    applied.Add(change);
                    if (File.Exists(destination))
                    {
                        if (change.NewOnly)
                            throw new IOException("目标文件已经存在：" + destination);
                        var backup = Path.Combine(workspace, "previous", change.RelativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Move(destination, backup);
                        change.BackupPath = backup;
                    }
                    if (change.StagedPath != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Move(change.StagedPath, destination);
                        change.Installed = true;
                    }
                }
            }
            catch
            {
                foreach (var change in Enumerable.Reverse(applied))
                {
                    try
                    {
                        var destination = game.Resolve(change.RelativePath);
                        if (change.Installed)
                            File.Delete(destination);
                        if (change.BackupPath != null)
                            File.Move(change.BackupPath, destination);
                    }
                    catch (IOException) { keepBackups = true; }
                    catch (UnauthorizedAccessException) { keepBackups = true; }
                }
                if (keepBackups)
                    throw new IOException("有文件无法自动还原。备份位于：" + Path.Combine(workspace, "previous"));
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                if (!keepBackups && Directory.Exists(workspace))
                    Directory.Delete(workspace, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally { gate.Dispose(); }
        }
    }
}
