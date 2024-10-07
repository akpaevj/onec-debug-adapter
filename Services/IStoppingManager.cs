using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace Onec.DebugAdapter.Services
{
    public interface IStoppingManager
    {
        void Run(DebugProtocolClient client, CancellationToken cancellationToken);
        Task<SetBreakpointsResponse> SetBreakpoints(SetBreakpointsArguments args);
        Task<SetExceptionBreakpointsResponse> SetExceptionBreakpoints(SetExceptionBreakpointsArguments args);
        Task<StackTraceResponse> GetCallStack(StackTraceArguments args);
        void ClearStackFrameInfo(int threadId);
        ScopesResponse GetScopes(ScopesArguments args);
        Task<VariablesResponse> GetVariables(VariablesArguments args);
        Task<EvaluateResponse> Evaluate(EvaluateArguments args);
    }
}