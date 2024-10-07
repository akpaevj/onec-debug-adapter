using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.DebugProtocol
{
    internal class AttachDebugTargetRequest : DebugRequest<AttachDebugTargetArguments>
    {
        public AttachDebugTargetRequest() : base("AttachDebugTargetRequest") { }
    }

    internal class AttachDebugTargetArguments
    {
        public string Id { get; set; }
    }
}
