using MessageQueueNET.Extensions;
﻿using MessageQueueNET.Models;
using MessageQueueNET.Services.Abstraction;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MessageQueueNET.Services
{
    public class QueuePersistFileSystem : IQueuesPersistService
    {
        private const string QueuePropertiesFilename = "_queue.properties.json";
        private const string UnconfirmedFolder = "_unconfirmed";
        private readonly string _rootPath;

        public QueuePersistFileSystem(IOptionsMonitor<QueuePersistFileSystemOptions> optionsMonitor)
        {
            var rootPath = optionsMonitor.CurrentValue.RootPath;
            _rootPath = Path.GetFullPath(String.IsNullOrWhiteSpace(rootPath) ? "." : rootPath);
        }

        #region Path Helper

        private static string ItemFilename(Guid itemId) => $"{itemId.ToString().ToLower()}.json";

        // Every path is built here: the queue name is validated and the result must stay inside the root path
        private string QueuePath(string queueName, params string[] parts)
        {
            if (!queueName.IsValidQueueName())
            {
                throw new ArgumentException($"Invalid queue name: {queueName}");
            }

            var queueRoot = Path.GetFullPath(Path.Combine(_rootPath, queueName));
            var fullPath = Path.GetFullPath(Path.Combine([queueRoot, .. parts]));

            if (!IsInside(queueRoot, _rootPath) || !(fullPath == queueRoot || IsInside(fullPath, queueRoot)))
            {
                throw new ArgumentException($"Invalid path for queue: {queueName}");
            }

            return fullPath;
        }

        private static bool IsInside(string path, string parent)
        {
            var parentWithSeparator = parent.EndsWith(Path.DirectorySeparatorChar)
                ? parent
                : parent + Path.DirectorySeparatorChar;

            return path.StartsWith(parentWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        #endregion

        #region IQueuePersist

        async public Task<bool> PersistQueueProperties(Queue queue)
        {
            try
            {
                if (queue?.Properties != null)
                {
                    FileInfo fi = new FileInfo(QueuePath(queue.Name, QueuePropertiesFilename));

                    if (!fi.Directory!.Exists)
                    {
                        fi.Directory.Create();
                    }

                    var jsonString = JsonSerializer.Serialize(queue.Properties);
                    await File.WriteAllTextAsync(fi.FullName, jsonString);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        async public Task<QueueProperties?> GetQueueProperties(string queueName)
        {
            try
            {
                FileInfo fi = new FileInfo(QueuePath(queueName, QueuePropertiesFilename));
                if (fi.Exists)
                {
                    var jsonString = await File.ReadAllTextAsync(fi.FullName);
                    return JsonSerializer.Deserialize<QueueProperties>(jsonString);
                }
            }
            catch { }
            return null;
        }

        async public Task<IEnumerable<QueueItem>> GetAllItems(string queueName)
        {
            var result = new List<QueueItem>();
            DirectoryInfo di = new DirectoryInfo(QueuePath(queueName));

            if (di.Exists)
            {
                foreach (var fi in di.GetFiles("*.json"))
                {
                    if (fi.Name.StartsWith("_"))
                    {
                        continue;
                    }

                    var jsonString = await File.ReadAllTextAsync(fi.FullName);
                    result.Add(JsonSerializer.Deserialize<QueueItem>(jsonString)!);
                }
            }
            return result;
        }

        async public Task<IEnumerable<QueueItem>> GetAllUnconfirmedItems(string queueName)
        {
            var result = new List<QueueItem>();
            DirectoryInfo di = new DirectoryInfo(QueuePath(queueName, UnconfirmedFolder));

            if (di.Exists)
            {
                foreach (var fi in di.GetFiles("*.json"))
                {
                    if (fi.Name.StartsWith("_"))
                    {
                        continue;
                    }

                    var jsonString = await File.ReadAllTextAsync(fi.FullName);
                    result.Add(JsonSerializer.Deserialize<QueueItem>(jsonString)!);
                }
            }

            return result;
        }

        async public Task<bool> PersistQueueItem(string queueName, QueueItem item)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(QueuePath(queueName));

                if (!di.Exists)
                {
                    di.Create();
                }

                var jsonString = JsonSerializer.Serialize(item);
                await File.WriteAllTextAsync(Path.Combine(di.FullName, ItemFilename(item.Id)), jsonString);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<IEnumerable<string>> QueueNames()
        {
            List<string> queueNames = new List<string>();

            try
            {
                DirectoryInfo di = new DirectoryInfo(_rootPath);
                foreach (var queueDirectory in di.GetDirectories())
                {
                    queueNames.Add(queueDirectory.Name);
                }
            }
            catch { }

            return Task.FromResult<IEnumerable<string>>(queueNames);
        }

        public Task<bool> RemoveQueue(string queueName)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(QueuePath(queueName));
                if (di.Exists)
                {
                    di.Delete(true);
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> RemoveQueueMessages(string queueName)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(QueuePath(queueName));
                if (di.Exists)
                {
                    foreach (var fi in di.GetFiles())
                    {
                        if (fi.Name.StartsWith("_"))
                        {
                            continue;  // eg: _queue.properties.json
                        }
                        fi.Delete();
                    }
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> RemoveQueueUnconfirmedMessages(string queueName)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(QueuePath(queueName, UnconfirmedFolder));
                if (di.Exists)
                {
                    foreach (var fi in di.GetFiles())
                    {
                        fi.Delete();
                    }
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> RemoveQueueItem(string queueName, Guid itemId)
        {
            try
            {
                FileInfo fi = new FileInfo(QueuePath(queueName, ItemFilename(itemId)));
                if (fi.Exists)
                {
                    fi.Delete();
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        async public Task<bool> PersistUnconfirmedQueueItem(string queueName, QueueItem item)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(QueuePath(queueName, UnconfirmedFolder));

                if (!di.Exists)
                {
                    di.Create();
                }

                var jsonString = JsonSerializer.Serialize(item);
                await File.WriteAllTextAsync(Path.Combine(di.FullName, ItemFilename(item.Id)), jsonString);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> RemoveUnconfirmedQueueItem(string queueName, Guid itemId)
        {
            try
            {
                FileInfo fi = new FileInfo(QueuePath(queueName, UnconfirmedFolder, ItemFilename(itemId)));
                if (fi.Exists)
                {
                    fi.Delete();
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        #endregion
    }
}
