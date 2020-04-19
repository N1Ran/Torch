﻿using System;
using System.IO;
using Torch.Utils;

namespace Torch.Tests
{
    public sealed class TestUtils
    {
        private static TorchAssemblyResolver _torchResolver;

        public static void Init()
        {
            if (_torchResolver == null)
                _torchResolver = new TorchAssemblyResolver(GetGameBinaries());
        }

        private static string GetGameBinaries()
        {
            var dir = Environment.CurrentDirectory;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                var gameBin = Path.Combine(dir, "GameBinaries");
                if (Directory.Exists(gameBin))
                    return gameBin;

                dir = Path.GetDirectoryName(dir);
            }

            throw new Exception("GetGameBinaries failed to find a folder named GameBinaries in the directory tree");
        }
    }
}