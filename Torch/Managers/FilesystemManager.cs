﻿using System;
using System.IO;
using NLog;

namespace Torch.Managers
{
    public class FilesystemManager : Manager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public FilesystemManager(ITorchBase torchInstance) : base(torchInstance)
        {
            var torch = new FileInfo(typeof(FilesystemManager).Assembly.Location).Directory.FullName;
            TempDirectory = Directory.CreateDirectory(Path.Combine(torch, "tmp")).FullName;
            TorchDirectory = torch;

            _log.Debug($"Clearing tmp directory at {TempDirectory}");
            ClearTemp();
        }

        /// <summary>
        ///     Temporary directory for Torch that is cleared every time the program is started.
        /// </summary>
        public string TempDirectory { get; }

        /// <summary>
        ///     Directory that contains the current Torch assemblies.
        /// </summary>
        public string TorchDirectory { get; }

        private void ClearTemp()
        {
            foreach (var file in Directory.GetFiles(TempDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException)
                {
                    _log.Debug($"Failed to delete file {file}, it's probably in use by another process'");
                }
                catch (Exception ex)
                {
                    _log.Warn($"Unhandled exception when clearing temp files. You may ignore this. {ex}");
                }
            }
        }

        /// <summary>
        ///     Move the given file (if it exists) to a temporary directory that will be cleared the next time the application
        ///     starts.
        /// </summary>
        public void SoftDelete(string path, string file)
        {
            var source = Path.Combine(path, file);
            if (!File.Exists(source))
                return;

            var rand = Path.GetRandomFileName();
            var dest = Path.Combine(TempDirectory, rand);
            File.Move(source, rand);
            var rsource = Path.Combine(path, rand);
            File.Move(rsource, dest);
        }
    }
}