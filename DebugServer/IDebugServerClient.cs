namespace Onec.DebugAdapter.DebugServer
{
    public interface IDebugServerClient : IDisposable
    {
        Task<RdbgAttachDebugUiResponse?> AttachDebugUI(RdbgAttachDebugUiRequest request, CancellationToken cancellationToken = default);
        Task<RdbgAttachDetachDbgTargetResponse?> AttachDetachDbgTargets(RdbgAttachDetachDebugTargetsRequest request, CancellationToken cancellationToken = default);
        Task<RdbgDetachDebugUiResponse?> DetachDebugUI(RdbgDetachDebugUiRequest request, CancellationToken cancellationToken = default);
        Task<RdbgEvalLocalVariablesResponse?> EvalLocalVariables(RdbgEvalLocalVariablesRequest request, CancellationToken cancellationToken = default);
        Task<RdbgEvalExprResponse?> EvalExpr(RdbgEvalExprRequest request, CancellationToken cancellationToken = default);
        Task<RdbgGetCallStackResponse?> GetCallStack(RdbgGetCallStackRequest request, CancellationToken cancellationToken = default);
        Task<RdbgsGetDbgTargetsResponse?> GetDbgTargets(RdbgsGetDbgTargetsRequest request, CancellationToken cancellationToken = default);
        Task InitSettings(RdbgSetInitialDebugSettingsRequest request, CancellationToken cancellationToken = default);
        Task<RdbgPingDebugUiResponse?> PingDebugUiParams(string dbgUi, CancellationToken cancellationToken = default);
        Task SetAutoAttachSettings(RdbgSetAutoAttachSettingsRequest request, CancellationToken cancellationToken = default);
        Task SetBreakOnRTE(RdbgSetRunTimeErrorProcessingRequest request, CancellationToken cancellationToken = default);
        Task SetBreakpoints(RdbgSetBreakpointsRequest request, CancellationToken cancellationToken = default);
        Task<RdbgStepResponse?> Step(RdbgStepRequest request, CancellationToken cancellationToken = default);
        Task Test(CancellationToken cancellationToken = default);
    }
}