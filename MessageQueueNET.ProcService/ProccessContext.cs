using MessageQueueNET.ProcService.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueueNET.ProcService
{
    class ProccessContext : ITaskContext
    {
        public ProccessContext()
        {
        }

        public string Command { get; set; }
        public string Arguments { get; set; }

        public int ExitCode { get; set; }
        public string Output { get; set; }

        #region ITaskContext

        public long TaskId { get; set; }

        public DateTime StartTime { get; set; }

        public string LogFile { get; set; }

        #endregion
    }
}
