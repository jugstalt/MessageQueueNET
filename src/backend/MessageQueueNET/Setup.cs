using MessageQueueNET.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace MessageQueueNET
{
    public class Setup
    {
        public bool TrySetup(string[] args)
        {
            try
            {
                if (SystemInfo.IsWindows)
                {
                    WindowPreSetup();
                }
                else if (SystemInfo.IsLinux)
                {
                    if (!String.IsNullOrEmpty(GetEnvironmentVariable("MESSAGEQUEUE__PERSIST__ROOTPATH")))
                    {
                        return true;
                    }

                    LinuxPreSetup();
                }

                var fiConfig = new FileInfo("_config/message-queue.json");
                if (!fiConfig.Exists)
                {
                    var fi = new FileInfo("_config/proto/_message-queue.json");
                    if (fi.Exists)
                    {
                        string configContent = String.Empty;

                        Console.WriteLine("#################################################################################################################");
                        Console.WriteLine("Setup:");
                        Console.WriteLine("First start: creating simple _config/message-queue.json. You can modify this file for your production settings...");
                        Console.WriteLine("#################################################################################################################");

                        if (SystemInfo.IsWindows)
                        {
                            configContent = WindowsSetup(fi.FullName, args);
                        }
                        else if (SystemInfo.IsLinux)
                        {
                            configContent = LinuxSetup(fi.FullName, args);
                        }

                        if (!String.IsNullOrEmpty(configContent))
                        {
                            Console.WriteLine(configContent);

                            var configTargets = new List<string>(new string[] { $"{fiConfig.Directory!.FullName}/message-queue.json" });

                            foreach (var configTarget in configTargets)
                            {
                                fiConfig = new FileInfo(configTarget);

                                if (!fiConfig.Directory!.Exists)
                                {
                                    fiConfig.Directory.Create();
                                }

                                File.WriteAllText(fiConfig.FullName, configContent);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning: can't intialize configuration for first start:");
                Console.WriteLine(ex.Message);

                return false;
            }

            return true;
        }

        #region Windows

        private void WindowPreSetup()
        {

        }

        private string WindowsSetup(string configTemplateFile, string[] args)
        {
            var fi = new FileInfo(configTemplateFile);

            var configText = File.ReadAllText(fi.FullName);
            configText = configText.Replace("{data-root-path}", DataRootPath(configTemplateFile).ToJsonString());

            return configText;
        }

        #endregion

        #region Linux

        private const string EnvKey_DataRootPath = "MESSAGEQUEUE_DATA_PATH";

        private void LinuxPreSetup()
        {

        }

        private string LinuxSetup(string configTemplateFile, string[] args)
        {
            string dataRootPath = DataRootPath(configTemplateFile);
            var fi = new FileInfo(configTemplateFile);

            var configText = File.ReadAllText(fi.FullName);
            configText = configText.Replace("{data-root-path}", dataRootPath.ToJsonString());

            return configText;
        }

        #endregion

        #region Helper

        private string? GetEnvironmentVariable(string name)
        {
            var environmentVariables = Environment.GetEnvironmentVariables();

            if (environmentVariables.Contains(name) && !String.IsNullOrWhiteSpace(environmentVariables[name]?.ToString()))
            {
                return environmentVariables[name]?.ToString();
            }

            return null;
        }

        private string DataRootPath(string configTemplateFile)
        {
            if (SystemInfo.IsWindows)
            {
                // ZIP layout: <install root>/<version>/api/app/_config/proto/_message-queue.json
                // data next to the version folders (<install root>/data), so it survives an update to a new version folder
                var versionFolder = new FileInfo(configTemplateFile).Directory!.Parent!.Parent!.Parent!.Parent!;
                return (versionFolder.Parent ?? versionFolder).FullName;
            }
            else if (SystemInfo.IsLinux)
            {
                return GetEnvironmentVariable(EnvKey_DataRootPath) ?? "/etc/message-queue";
            }
            throw new Exception("Setup: Unsupported OS!");
        }

        #endregion
    }
}
