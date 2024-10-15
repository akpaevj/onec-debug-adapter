using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugProtocol;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.V8;
using System.Net.Http.Headers;
using Exception = System.Exception;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace Onec.DebugAdapter.Services
{
    public class V8DebugAdapter : DebugAdapterBase, IDisposable
    {
        private CancellationToken _cancellation;
        private bool disposedValue;
        private bool _attached = false;

        private bool _linesStartAt1 = false;

        private readonly IDebugConfiguration _configuration;
        private readonly IMetadataProvider _metadataProvider;
        private readonly IDebugServerClient _debugServerClient;
        private readonly IDebugServerListener _debugServerListener;
        private readonly IDebugTargetsManager _debugTargetsManager;
        private readonly IStoppingManager _stoppingManager;
        private readonly IDebugAdapterExtender _debugAdapterExtender;
        private readonly DebugServerProcess _debugServer;
		private readonly DebuggeeProcess _debuggee;

		public V8DebugAdapter(
            IDebugConfiguration configuration,
            IMetadataProvider metadataProvider,
            IDebugServerClient debugServerClient,
            IDebugServerListener debugServerListener,
            IDebugTargetsManager debugTargetsManager,
            IStoppingManager stoppingManager,
            IDebugAdapterExtender debugAdapterExtender,
            DebuggeeProcess debuggee,
            DebugServerProcess debugServer)
        {
            _configuration = configuration;
            _metadataProvider = metadataProvider;
            _debugServerClient = debugServerClient;
            _debugServerListener = debugServerListener;
            _debugTargetsManager = debugTargetsManager;
            _stoppingManager = stoppingManager;
            _debugAdapterExtender = debugAdapterExtender;
			_debugServer = debugServer;
            _debuggee = debuggee;

		}

        public async Task Run(Stream input, Stream output, CancellationToken cancellationToken = default)
        {
            _cancellation = cancellationToken;
            InitializeProtocolClient(input, output);
            _cancellation.Register(Protocol.Stop);

            await Task.Run(async () =>
            {
                _debugAdapterExtender.Init(Protocol, _cancellation);

                Protocol.Run();
                Protocol.WaitForReader();

                await Disconnect();
            }, cancellationToken);
        }

        protected override void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder)
        {
			_linesStartAt1 = responder.Arguments.LinesStartAt1 ?? false;

            responder.SetResponse(new()
            {
                SupportsEvaluateForHovers = true,
                SupportsExceptionFilterOptions = true,
                SupportsConditionalBreakpoints = true,
                SupportsLogPoints = true,
                SupportsSingleThreadExecutionRequests = true,
                ExceptionBreakpointFilters = new()
                {
                    new()
                    {
                        Filter = "all",
                        Label = "Остановка по ошибке",
                        Description = "Остановка при возникновении исключения времени выполнения",
                        SupportsCondition = true,
                        ConditionDescription = "Искомая подстрока текста исключения"
                    }
                }
            });
        }

        protected override async void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            try
            {
                await InitLaunchAttach(responder, responder.Arguments.ConfigurationProperties, true);
            }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка запуска отладки (запуск)", ex);
            }
        }

        protected override async void HandleAttachRequestAsync(IRequestResponder<AttachArguments> responder)
        {
            try
            {
                await InitLaunchAttach(responder, responder.Arguments.ConfigurationProperties, false);
            }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка запуска отладки (присоединение)", ex);
            }
        }

        protected override void HandleThreadsRequestAsync(IRequestResponder<ThreadsArguments, ThreadsResponse> responder)
        {
            try
            {
                responder.SetResponse(_debugTargetsManager.GetThreads(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка при выполнение запроса подключенных предметов отладки", ex);
            }
        }

        protected override async void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder)
        {
            try
            {
                responder.SetResponse(await _stoppingManager.SetBreakpoints(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка при обработке запроса конфигурации точек останова", ex);
            }
        }

        protected override async void HandleSetExceptionBreakpointsRequestAsync(IRequestResponder<SetExceptionBreakpointsArguments, SetExceptionBreakpointsResponse> responder)
        {
            try
            {
                responder.SetResponse(await _stoppingManager.SetExceptionBreakpoints(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка при обработке запроса конфигурации останова по исключению", ex);
            }
        }

        protected override async void HandleStackTraceRequestAsync(IRequestResponder<StackTraceArguments, StackTraceResponse> responder)
        {
            try
            {
                responder.SetResponse(await _stoppingManager.GetCallStack(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, $"Ошибка при получении стека вызовов потока {responder.Arguments.ThreadId}", ex);
            }
        }

        protected override void HandleScopesRequestAsync(IRequestResponder<ScopesArguments, ScopesResponse> responder)
        {
            try
            {
                responder.SetResponse(_stoppingManager.GetScopes(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, $"Ошибка при получении областей переменных потока", ex);
            }
        }

        protected override async void HandleVariablesRequestAsync(IRequestResponder<VariablesArguments, VariablesResponse> responder)
        {
            try
            {
                responder.SetResponse(await _stoppingManager.GetVariables(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, $"Ошибка при получении переменных", ex);
            }
        }

        protected override async void HandleEvaluateRequestAsync(IRequestResponder<EvaluateArguments, EvaluateResponse> responder)
        {
            try
            {
                responder.SetResponse(await _stoppingManager.Evaluate(responder.Arguments));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetProtocolError(responder, $"Ошибка при вычислении выражения", ex);
            }
        }

        protected override async void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder)
            => await SendStepEvent(responder, responder.Arguments.ThreadId, DebugStepAction.Continue, responder.Arguments.SingleThread == true, () =>
            {
                responder.SetResponse(new ContinueResponse() { AllThreadsContinued = responder.Arguments.SingleThread != true });
            });

        protected override async void HandleNextRequestAsync(IRequestResponder<NextArguments> responder)
            => await SendStepEvent(responder, responder.Arguments.ThreadId, DebugStepAction.Step, responder.Arguments.SingleThread == true);

        protected override async void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder)
            => await SendStepEvent(responder, responder.Arguments.ThreadId, DebugStepAction.StepIn, responder.Arguments.SingleThread == true);

        protected override async void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder)
            => await SendStepEvent(responder, responder.Arguments.ThreadId, DebugStepAction.StepOut, responder.Arguments.SingleThread == true);

        protected override async void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder)
        {
            try
            {
                await Disconnect();
                responder.SetResponse(new DisconnectResponse());
            }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка присоединения к серверу отладки", ex);
            }
        }

        private async Task InitLaunchAttach(IRequestResponder responder, Dictionary<string, JToken> configurationArgs, bool launch)
        {
            await _configuration.Init(configurationArgs);

            if (_configuration.IsFileInfoBase)
                await _debugServer.Run(Protocol);

            await _debugServerClient.Test(_cancellation);

            var response = await _debugServerClient.AttachDebugUI(_configuration.CreateRequest<RdbgAttachDebugUiRequest>(i =>
            {
                i.Options = new DebuggerOptions()
                {
                    ForegroundAbility = true
                };
            }));

			if (launch)
				_debuggee.Run(Protocol);

			_attached = true;

            switch (response!.Result)
            {
                case AttachDebugUiResult.Unknown:
					SetProtocolError(responder, "Неизвестная ошибка при подключении к серверу отладки");
                    break;
                case AttachDebugUiResult.IbInDebug:
					SetProtocolError(responder, "Информационная база уже отлаживается");
                    break;
                case AttachDebugUiResult.NotRegistered:
					SetProtocolError(responder, "Не удалось подключиться к серверу отладки");
                    break;
                case AttachDebugUiResult.CredentialsRequired:
                case AttachDebugUiResult.FullCredentialsRequired:
					SetProtocolError(responder, "Ошибка аутентификации на сервере отладки");
                    break;
                default:
					await _metadataProvider.Init(Protocol, _cancellation);
                    _debugServerListener.Run(Protocol, _cancellation);
					await _debugTargetsManager.Run(Protocol, _cancellation);
                    _stoppingManager.Run(Protocol, _cancellation);

					responder.SetResponse(launch ? new LaunchResponse() : new AttachResponse());
					Protocol.SendEvent(new InitializedEvent());

					break;
            };
        }

        private async Task Disconnect()
        {
            if (_attached)
                await _debugServerClient.DetachDebugUI(_configuration.CreateRequest<RdbgDetachDebugUiRequest>());

            _attached = false;
        }

        private async Task SendStepEvent<T>(T responder, int threadId, DebugStepAction action, bool singleThread, Action? successAction = null) where T : IRequestResponder
        {
            try
            {
                var request = _configuration.CreateRequest<RdbgStepRequest>();
                request.TargetId = _debugTargetsManager.GetTargetId(threadId).ToLight();
                request.Action = action;

                var response = await _debugServerClient.Step(request, _cancellation);

                if (response?.ItemSpecified == true)
                    foreach (var item in response!.Item)
                    {
                        if (_debugTargetsManager.DebugTargetAttached(item.TargetId))
                        {
                            var itemThreadId = _debugTargetsManager.GetThreadId(item.TargetId);

                            switch (item.State)
                            {
                                case DbgTargetState.Worked:
                                    Protocol.SendEvent(new ContinuedEvent()
                                    {
                                        ThreadId = itemThreadId,
                                        AllThreadsContinued = false
                                    });
                                    break;
                                default:
                                    break;
                            };
                        }
                    }

                successAction?.Invoke();
            }
            catch (Exception ex)
            {
                SetProtocolError(responder, "Ошибка отправки события шага отладки", ex);
            }
        }

        private static void SetProtocolError(IRequestResponder responder, string message)
            => responder.SetError(new ProtocolException(message));

		private void SetProtocolError(IRequestResponder responder, string message, Exception exception)
        {
			Protocol.SendError(exception);
			responder.SetError(new ProtocolException(message));
		}

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _debugServerClient.Dispose();
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
