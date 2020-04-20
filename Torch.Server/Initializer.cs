﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using NLog;
using Sandbox.Engine.Utils;
using Torch.UI.Views;
using VRage.FileSystem;

namespace Torch.Server
{
    public class Initializer
    {
        private const string STEAMCMD_DIR = "steamcmd";
        private const string STEAMCMD_ZIP = "temp.zip";

        private const string RUNSCRIPT = @"force_install_dir ../
login anonymous
app_update 298740
quit";

        private static readonly Logger Log = LogManager.GetLogger(nameof(Initializer));
        private static readonly string STEAMCMD_PATH = $"{STEAMCMD_DIR}\\steamcmd.exe";
        private static readonly string RUNSCRIPT_PATH = $"{STEAMCMD_DIR}\\runscript.txt";
        private string _basePath;

        private bool _init;

        public Initializer(string basePath)
        {
            _basePath = basePath;
        }

        public TorchConfig Config { get; private set; }

        public TorchServer Server { get; private set; }

        public bool Initialize(string[] args)
        {
            if (_init)
                return false;

#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += HandleException;
#endif

#if DEBUG
            //enables logging debug messages when built in debug mode. Amazing.
            LogManager.Configuration.AddRule(LogLevel.Debug, LogLevel.Debug, "main");
            LogManager.Configuration.AddRule(LogLevel.Debug, LogLevel.Debug, "console");
            LogManager.Configuration.AddRule(LogLevel.Debug, LogLevel.Debug, "wpf");
            LogManager.ReconfigExistingLoggers();
            Log.Debug("Debug logging enabled.");
#endif

            // This is what happens when Keen is bad and puts extensions into the System namespace.
            if (!Enumerable.Contains(args, "-noupdate"))
                RunSteamCmd();

            var basePath = new FileInfo(typeof(Program).Assembly.Location).Directory.ToString();
            var apiSource = Path.Combine(basePath, "DedicatedServer64", "steam_api64.dll");
            var apiTarget = Path.Combine(basePath, "steam_api64.dll");

            if (!File.Exists(apiTarget))
            {
                File.Copy(apiSource, apiTarget);
            }
            else if (File.GetLastWriteTime(apiTarget) < File.GetLastWriteTime(apiSource))
            {
                File.Delete(apiTarget);
                File.Copy(apiSource, apiTarget);
            }

            var havokSource = Path.Combine(basePath, "DedicatedServer64", "Havok.dll");
            var havokTarget = Path.Combine(basePath, "Havok.dll");

            if (!File.Exists(havokTarget))
            {
                File.Copy(havokSource, havokTarget);
            }
            else if (File.GetLastWriteTime(havokTarget) < File.GetLastWriteTime(havokSource))
            {
                File.Delete(havokTarget);
                File.Copy(havokSource, havokTarget);
            }

            Config = InitConfig();
            if (!Config.Parse(args))
                return false;

            if (!string.IsNullOrEmpty(Config.WaitForPID))
            {
                try
                {
                    var pid = int.Parse(Config.WaitForPID);
                    var waitProc = Process.GetProcessById(pid);
                    Log.Info("Continuing in 5 seconds.");
                    Log.Warn($"Waiting for process {pid} to close");
                    while (!waitProc.HasExited)
                    {
                        Console.Write(".");
                        Thread.Sleep(1000);
                    }
                }
                catch
                {
                    // ignored
                }
            }

            _init = true;
            return true;
        }

        public void Run()
        {
            Server = new TorchServer(Config);

            if (Config.NoGui)
            {
                Server.Init();
                Server.Start();
            }
            else
            {
#if !DEBUG
                if (!Config.IndependentConsole)
                {
                    Console.SetOut(TextWriter.Null);
                    NativeMethods.FreeConsole();
                }
#endif

                var gameThread = new Thread(() =>
                {
                    Server.Init();

                    if (Config.Autostart)
                        Server.Start();
                });

                gameThread.Start();

                var ui = new TorchUI(Server);
                ui.ShowDialog();
            }
        }

        private TorchConfig InitConfig()
        {
            var configName = "Torch.cfg";
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), configName);
            if (File.Exists(configName))
            {
                Log.Info($"Loading config {configPath}");
                return TorchConfig.LoadFrom(configPath);
            }
            else
            {
                Log.Info($"Generating default config at {configPath}");
                var config = new TorchConfig {InstancePath = Path.GetFullPath("Instance")};
                config.Save(configPath);
                return config;
            }
        }

        public static void RunSteamCmd()
        {
            var log = LogManager.GetLogger("SteamCMD");

            if (!Directory.Exists(STEAMCMD_DIR))
            {
                Directory.CreateDirectory(STEAMCMD_DIR);
            }

            if (!File.Exists(RUNSCRIPT_PATH))
                File.WriteAllText(RUNSCRIPT_PATH, RUNSCRIPT);

            if (!File.Exists(STEAMCMD_PATH))
            {
                try
                {
                    log.Info("Downloading SteamCMD.");
                    using (var client = new WebClient())
                        client.DownloadFile("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", STEAMCMD_ZIP);

                    ZipFile.ExtractToDirectory(STEAMCMD_ZIP, STEAMCMD_DIR);
                    File.Delete(STEAMCMD_ZIP);
                    log.Info("SteamCMD downloaded successfully!");
                }
                catch (Exception e)
                {
                    log.Error("Failed to download SteamCMD, unable to update the DS.");
                    log.Error(e);
                    return;
                }
            }

            log.Info("Checking for DS updates.");
            var steamCmdProc = new ProcessStartInfo(STEAMCMD_PATH, "+runscript runscript.txt")
            {
                WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), STEAMCMD_DIR),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.ASCII
            };
            var cmd = Process.Start(steamCmdProc);

            // ReSharper disable once PossibleNullReferenceException
            while (!cmd.HasExited)
            {
                log.Info(cmd.StandardOutput.ReadLine());
                Thread.Sleep(100);
            }
        }

        private void LogException(Exception ex)
        {
            if (ex is AggregateException ag)
            {
                foreach (var e in ag.InnerExceptions)
                    LogException(e);

                return;
            }

            Log.Fatal(ex);

            if (ex is ReflectionTypeLoadException extl)
            {
                foreach (var exl in extl.LoaderExceptions)
                    LogException(exl);

                return;
            }

            if (ex.InnerException != null)
            {
                LogException(ex.InnerException);
            }
        }

        private void HandleException(object sender, UnhandledExceptionEventArgs e)
        {
            Server.FatalException = true;
            var ex = (Exception)e.ExceptionObject;
            LogException(ex);
            if (MyFakes.ENABLE_MINIDUMP_SENDING)
            {
                var path = Path.Combine(MyFileSystem.UserDataPath, "Minidump.dmp");
                Log.Info($"Generating minidump at {path}");
                Log.Error("Keen broke the minidump, sorry.");
                //MyMiniDump.Options options = MyMiniDump.Options.WithProcessThreadData | MyMiniDump.Options.WithThreadInfo;
                //MyMiniDump.Write(path, options, MyMiniDump.ExceptionInfo.Present);
            }

            LogManager.Flush();
            if (Config.RestartOnCrash)
            {
                Console.WriteLine("Restarting in 5 seconds.");
                Thread.Sleep(5000);
                var exe = typeof(Program).Assembly.Location;
                Config.WaitForPID = Process.GetCurrentProcess().Id.ToString();
                Process.Start(exe, Config.ToString());
            }
            else
            {
                MessageBox.Show("Torch encountered a fatal error and needs to close. Please check the logs for details.");
            }

            Process.GetCurrentProcess().Kill();
        }
    }
}