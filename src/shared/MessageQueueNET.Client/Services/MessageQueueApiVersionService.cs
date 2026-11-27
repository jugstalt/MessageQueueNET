using MessageQueueNET.Client.Services.Abstraction;
using System;
using System.Reflection;

namespace MessageQueueNET.Client.Services
{
    public class MessageQueueApiVersionService : IMessageQueueApiVersionService
    {
        private readonly Version _version;

        public MessageQueueApiVersionService()
        {
            var assembly = Assembly.GetAssembly(typeof(QueueClient))!;

            // The version comes from the git tag (MinVer). MinVer sets AssemblyVersion to major.0.0.0 only
            // (binding compatibility), the file version is major.minor.patch.0 => use the file version.
            var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

            _version = Version.TryParse(fileVersion, out var version)
                ? version
                : assembly.GetName().Version!;
        }

        public Version Version => _version;
    }
}
