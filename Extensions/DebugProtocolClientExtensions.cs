using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace Onec.DebugAdapter.Extensions
{
    internal static class DebugProtocolClientExtensions
    {
        internal static void SendOutput(this DebugProtocolClient client, string message)
           => client.SendOutputEvent(message, OutputEvent.CategoryValue.Stdout);

        internal static void SendError(this DebugProtocolClient client, Exception ex)
            => client.SendError(ex.Message);

        internal static void SendError(this DebugProtocolClient client, string message, Exception ex)
            => client.SendError($"{message}: {ex}");

        internal static void SendError(this DebugProtocolClient client, string message)
            => client.SendOutputEvent(message, OutputEvent.CategoryValue.Stderr);

        internal static void SendOutputEvent(this DebugProtocolClient client, string message, OutputEvent.CategoryValue category)
        {
            client.SendEvent(new OutputEvent()
            {
                Category = category,
                Output = message
            });
        }
    }
}
