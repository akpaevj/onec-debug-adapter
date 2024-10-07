using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.Services;

namespace Onec.DebugAdapter.DebugServer
{
    public class DebugServerListener : IDebugServerListener
    {
        private CancellationToken _cancellation;

        private readonly IDebugServerClient _debugServerClient;
        private readonly IDebugConfiguration _debugConfiguration;
        private DebugProtocolClient? _debugProtocolClient;

        public event EventHandler<DebugTargetEventArgs>? DebugTargetEvent;
        public event EventHandler<CallStackFormedEventArgs>? CallStackFormed;
        public event EventHandler<ExpressionEvaluatedEventArgs>? ExpressionEvaluated;
        public event EventHandler<RuntimeExceptionArgs>? RuntimeException;

        public DebugServerListener(IDebugConfiguration debugConfiguration, IDebugServerClient debugServerClient)
        {
            _debugConfiguration = debugConfiguration;
            _debugServerClient = debugServerClient;
        }

        public void Run(DebugProtocolClient debugProtocolClient, CancellationToken cancellationToken)
        {
            _debugProtocolClient = debugProtocolClient;
            _cancellation = cancellationToken;

            Task.Run(async () =>
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var response = await _debugServerClient.PingDebugUiParams(_debugConfiguration.DebuggerID, cancellationToken);

                        foreach (var extCommand in response?.Result ?? new())
                        {
                            switch (extCommand.CmdId)
                            {
                                case DbguiExtCmds.TargetStarted:
                                    DebugTargetEvent?.Invoke(this, new DebugTargetEventArgs(extCommand.TargetId, true));
                                    break;
                                case DbguiExtCmds.TargetQuit:
                                    DebugTargetEvent?.Invoke(this, new DebugTargetEventArgs(extCommand.TargetId, false));
                                    break;
                                case DbguiExtCmds.CallStackFormed:
                                    CallStackFormed?.Invoke(this, new CallStackFormedEventArgs((extCommand as DbguiExtCmdInfoCallStackFormed)!));
                                    break;
                                case DbguiExtCmds.ExprEvaluated:
                                    ExpressionEvaluated?.Invoke(this, new ExpressionEvaluatedEventArgs((extCommand as DbguiExtCmdInfoExprEvaluated)!));
                                    break;
                                case DbguiExtCmds.RteProcessing:
                                    RuntimeException?.Invoke(this, new RuntimeExceptionArgs((extCommand as DbguiExtCmdInfoRte)!));
                                    break;
                                case DbguiExtCmds.Unknown:
                                case DbguiExtCmds.CorrectedBp:
                                case DbguiExtCmds.RteOnBpConditionProcessing:
                                case DbguiExtCmds.MeasureResultProcessing:
                                case DbguiExtCmds.ValueModified:
                                case DbguiExtCmds.ErrorViewInfo:
                                case DbguiExtCmds.ForegroundHelperSet:
                                case DbguiExtCmds.ForegroundHelperRequest:
                                case DbguiExtCmds.ForegroundHelperProcess:
                                case DbguiExtCmds.ShowMetadataObject:
                                default:
                                    throw new NotImplementedException($"Получено неизвестное значение CmdID: {extCommand.CmdId}");
                            }
                        }

                        await Task.Delay(25, cancellationToken);
                    }
                    catch (TaskCanceledException) { }
                    catch (System.Exception ex)
                    {
                        _debugProtocolClient.SendError(ex);
                    }
                }
            }, _cancellation).ConfigureAwait(false);
        }
    }
}
