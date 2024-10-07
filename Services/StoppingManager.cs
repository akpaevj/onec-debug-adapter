using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.Services
{
    public class StoppingManager : IStoppingManager
    {
        private CancellationToken _cancellation;

        private readonly IDebugConfiguration _configuration;
        private readonly IMetadataProvider _metadataProvider;
        private readonly IDebugServerClient _debugServerClient;
        private readonly IDebugServerListener _listener;
        private readonly IDebugTargetsManager _targetsManager;
        private DebugProtocolClient _client = null!;

        private readonly ConcurrentDictionary<int, TaskCompletionSource> _callStackRequestTasks = new();
        private readonly ConcurrentDictionary<int, List<StackItemViewInfoData>> _threadsCallStack = new();
        private readonly References<(int ThreadId, int FrameId)> _frameIdentifiers = new();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<List<CalculationResultBaseData>>> _evaluationTasks = new();
        private readonly References<(int ThreadId, int FrameId, List<SourceCalculationDataItem> Path, ViewInterface Interface)> _variableIdentifiers = new();

        public StoppingManager(
            IDebugConfiguration debugConfiguration,
            IMetadataProvider metadataProvider,
            IDebugServerClient debugServerClient,
            IDebugServerListener debugServerListener,
            IDebugTargetsManager debugTargetsManager)
        {
            _configuration = debugConfiguration;
            _metadataProvider = metadataProvider;
            _debugServerClient = debugServerClient;
            _listener = debugServerListener;
            _targetsManager = debugTargetsManager;
        }

        public void Run(DebugProtocolClient client, CancellationToken cancellationToken)
        {
            _cancellation = cancellationToken;
            _client = client;

            _listener.CallStackFormed += CallStackFormedHandler;
            _listener.RuntimeException += RuntimeExceptionHandler;
            _listener.ExpressionEvaluated += ExpressionEvaluatedHandler;
        }

        public async Task<SetBreakpointsResponse> SetBreakpoints(SetBreakpointsArguments args)
        {
            var (Extension, ObjectId, PropertyId) = _metadataProvider.ModuleInfoByPath(args.Source.Path.CapitalizeFirstChar());

            var moduleInfo = new ModuleBpInfoInternal()
            {
                Id = new BslModuleIdInternal()
                {
                    Type = string.IsNullOrEmpty(Extension) ? BslModuleType.ConfigModule : BslModuleType.ExtensionModule,
                    ExtensionName = Extension,
                    ObjectId = ObjectId,
                    PropertyId = PropertyId
                }
            };
            args.Breakpoints.ForEach(bp =>
            {
                moduleInfo.BpInfo.Add(new BreakpointInfo()
                {
                    Line = bp.Line,
                    HitCount = 1,
                    IsActive = true,
                    PutDescription = bp.LogMessage,
                    ShowOutputMessage = !string.IsNullOrEmpty(bp.LogMessage),
                    ContinueExecution = !string.IsNullOrEmpty(bp.LogMessage),
                    Condition = bp.Condition ?? "",
                    BreakOnCondition = !string.IsNullOrEmpty(bp.Condition)
                });
            });
            var request = _configuration.CreateRequest<RdbgSetBreakpointsRequest>();
            request.BpWorkspace.Add(moduleInfo);

            await _debugServerClient.SetBreakpoints(request);

            return new SetBreakpointsResponse()
            {
                Breakpoints = moduleInfo.BpInfo.Select(c => new Breakpoint()
                {
                    Line = (int)(c.Line),
                    Source = args.Source,
                    Verified = true
                }).ToList()
            };
        }

        public async Task<SetExceptionBreakpointsResponse> SetExceptionBreakpoints(SetExceptionBreakpointsArguments args)
        {
            var request = _configuration.CreateRequest<RdbgSetRunTimeErrorProcessingRequest>();
            request.State = new RteFilterStorage()
            {
                StopOnErrors = args.FilterOptions switch
                {
                    null => false,
                    _ => args.FilterOptions.Count > 0
                }
            };
            var filter = args.FilterOptions?.FirstOrDefault();
            if (filter != null && !string.IsNullOrEmpty(filter.Condition))
            {
                request.State.AnalyzeErrorStr = true;
                request.State.StrTemplate.Add(new RteFilterItem()
                {
                    Include = true,
                    Str = filter.Condition
                });
            }

            await _debugServerClient.SetBreakOnRTE(request, _cancellation);

            return new SetExceptionBreakpointsResponse()
            {
                Breakpoints = new() { new() { Verified = true } }
            };
        }

        public async Task<StackTraceResponse> GetCallStack(StackTraceArguments args)
        {
            if (!_threadsCallStack.ContainsKey(args.ThreadId))
            {
                _callStackRequestTasks.TryAdd(args.ThreadId, new TaskCompletionSource());
                await _callStackRequestTasks[args.ThreadId].Task;
            }

            var callStack = _threadsCallStack[args.ThreadId];

            return new StackTraceResponse()
            {
                TotalFrames = callStack.Count,
                StackFrames = callStack.Select((c, index) =>
                {
                    return new StackFrame()
                    {
                        Id = _frameIdentifiers.Add((args.ThreadId, index)),
                        Name = $"{c.Presentation.GetUTF8String()} : {c.LineNo}",
                        Line = (int)(c.LineNo),
                        Source = new Source()
                        {
                            Path = _metadataProvider.ModulePathByInfo(
                                c.ModuleId.ExtensionName ?? "", 
                                c.ModuleId.ObjectId, 
                                c.ModuleId.PropertyId)
                        }
                    };
                }).ToList()
            };
        }

        public void ClearStackFrameInfo(int threadId)
        {
            _callStackRequestTasks.TryRemove(threadId, out _);
            _threadsCallStack.TryRemove(threadId, out _);
            _frameIdentifiers.Clear(item => item.ThreadId == threadId);
            _variableIdentifiers.Clear(item => item.ThreadId == threadId);
        }

        public ScopesResponse GetScopes(ScopesArguments args)
        {
            var (ThreadId, FrameId) = _frameIdentifiers.Get(args.FrameId);

            return new ScopesResponse()
            {
                Scopes = new List<Scope>()
                {
                    new()
                    {
                        Name = "Локальные",
                        VariablesReference = _variableIdentifiers.Add((ThreadId, FrameId, new(), ViewInterface.Context))
                    }
                }
            };
        }

        public async Task<VariablesResponse> GetVariables(VariablesArguments args)
        {
            (int ThreadId, int FrameId, List<SourceCalculationDataItem> Path, ViewInterface ViewInterface) = _variableIdentifiers.Get(args.VariablesReference);

            var debuggeeResponse = (Path.Count > 0) switch
            {
                true => await Eval(ThreadId, FrameId, Path, ViewInterface),
                _ => await EvalLocalVariables(ThreadId, FrameId)
            };
            var result = debuggeeResponse.First();

            var response = new VariablesResponse();

            switch(result.CalculationResult.ViewInterface)
            {
                case ViewInterface.Enum:
                    response.Variables = result.CalculationResult.ValueOfEnumInfo.Select(c =>
                    {
                        return new Variable()
                        {
                            Name = c.ValueInfo.Pres.GetUTF8String()
                        };
                    }).ToList();
                    break;
                case ViewInterface.Collection:
                    response.Variables = GetCollectionVariables(ThreadId, FrameId, Path, result);
                    break;
                case ViewInterface.Context:
                    response.Variables = result.CalculationResult.ValueOfContextPropInfo.Select(c =>
                    {
                        if (c.PropInfo.IsReaded)
                        {
                            var itemPath = new List<SourceCalculationDataItem>();
                            itemPath.AddRange(Path);
                            if (itemPath.Count == 0)
                                itemPath.Add(new()
                                {
                                    Expression = c.PropInfo.PropName,
                                    ItemType = SourceCalculationDataItemType.Expression
                                });
                            else
                                itemPath.Add(new()
                                {
                                    Property = c.PropInfo.PropName,
                                    ItemType = SourceCalculationDataItemType.Property
                                });

                            var itemReference = GetVariableReference(ThreadId, FrameId, itemPath, c.ValueInfo);

                            return new Variable()
                            {
                                Name = $"{c.PropInfo.PropName} ({c.ValueInfo.TypeName})",
                                Type = c.ValueInfo.TypeName,
                                Value = c.ValueInfo.Pres.GetUTF8String(),
                                VariablesReference = itemReference
                            };
                        }
                        else
                            return new Variable()
                            {
                                Name = c.PropInfo.PropName,
                                Value = c.PropInfo.ErrorStr.GetUTF8String()
                            };
                    }).ToList();
                    break;
                default:
                    break;
            };

            if (result.CalculationResult.ViewInterface == ViewInterface.Context && 
                result.ResultValueInfo.IsIIndexedCollectionRo &&
                result.ResultValueInfo.CollectionSizeSpecified)
            {
                var itemsEvalResult = await Eval(ThreadId, FrameId, Path, ViewInterface.Collection);
                var items = itemsEvalResult.First();

                response.Variables.AddRange(GetCollectionVariables(ThreadId, FrameId, Path, items));
            }

            return response;
        }

        private List<Variable> GetCollectionVariables(int threadId, int frameId, List<SourceCalculationDataItem> path, CalculationResultBaseData result)
        {
            return result.CalculationResult.ValueOfCollectionInfo.Select((c, index) =>
            {
                var itemPath = new List<SourceCalculationDataItem>();
                itemPath.AddRange(path);
                itemPath.Add(new SourceCalculationDataItem()
                {
                    Index = index,
                    IndexSpecified = true,
                    ItemType = SourceCalculationDataItemType.Index
                });

                var itemReference = GetVariableReference(threadId, frameId, itemPath, c.ValueInfo);

                return new Variable()
                {
                    Name = $"{index} ({c.ValueInfo.TypeName})",
                    Type = c.ValueInfo.TypeName,
                    Value = c.ValueInfo.Pres.GetUTF8String(),
                    VariablesReference = itemReference
                };
            }).ToList();
        }

        public async Task<EvaluateResponse> Evaluate(EvaluateArguments args)
        {
            var path = new List<SourceCalculationDataItem>()
            {
                new()
                {
                    Expression = args.Expression,
                    ItemType = SourceCalculationDataItemType.Expression
                }
            };

            var (ThreadId, FrameId) = _frameIdentifiers.Get((int)args.FrameId!);

            var debuggeeResponse = await Eval(ThreadId, FrameId, path, ViewInterface.Context);
            var debuggeeResponseItem = debuggeeResponse.FirstOrDefault();

            var response = new EvaluateResponse();
            if (debuggeeResponseItem == null)
                response.Result = "Ошибка получения результата вычисления выражения";
            else if (debuggeeResponseItem.ErrorOccurred)
                response.Result = debuggeeResponseItem.ExceptionStr.GetUTF8String();
            else
            {
                response.Result = debuggeeResponseItem.ResultValueInfo.Pres.GetUTF8String();
                response.Type = debuggeeResponseItem.ResultValueInfo.TypeName;

                response.VariablesReference = GetVariableReference(ThreadId, FrameId, path, debuggeeResponseItem.ResultValueInfo);
            }

            return response;
        }

        private async Task<List<CalculationResultBaseData>> EvalLocalVariables(int threadId, int frameId)
        {
            var resultId = Guid.NewGuid().ToString();
            _evaluationTasks.TryAdd(resultId, new());

            var presentationOptions = new DbgPresentationOptionsOfStringValue()
            {
                MaxTextSize = 307200
            };

            var sourceCalculationDataInfo = new SourceCalculationDataInfo()
            {
                ExpressionResultId = resultId
            };
            sourceCalculationDataInfo.Interfaces.Add(ViewInterface.Context);

            var calculationSource = new CalculationSourceDataStorage
            {
                StackLevel = frameId > 0 ? frameId : 0,
                SrcCalcInfo = sourceCalculationDataInfo,
                PresOptions = presentationOptions
            };

            var request = _configuration.CreateRequest<RdbgEvalLocalVariablesRequest>();
            request.TargetId = _targetsManager.GetTargetId(threadId).ToLight();
            request.CalcWaitingTime = 100;
            request.Expr.Add(calculationSource);

            var response = await _debugServerClient.EvalLocalVariables(request, _cancellation);

            if (response?.Result == null)
                await _evaluationTasks[resultId].Task;
            else
                _evaluationTasks[resultId].SetResult(new() { response!.Result });

            var result = _evaluationTasks[resultId].Task.Result;
            _evaluationTasks.Remove(resultId, out _);
            return result;
        }

        private async Task<List<CalculationResultBaseData>> Eval(int threadId, int frameId, List<SourceCalculationDataItem> path, ViewInterface viewInterface)
        {
            var resultId = Guid.NewGuid().ToString();
            _evaluationTasks.TryAdd(resultId, new());

            var presentationOptions = new DbgPresentationOptionsOfStringValue()
            {
                MaxTextSize = 307200
            };

            var sourceCalculationDataInfo = new SourceCalculationDataInfo()
            {
                ExpressionResultId = resultId
            };
            sourceCalculationDataInfo.Interfaces.Add(viewInterface);
            path.ForEach(c => sourceCalculationDataInfo.CalcItem.Add(c));

            var calculationSource = new CalculationSourceDataStorage
            {
                StackLevel = frameId > 0 ? frameId : 0,
                SrcCalcInfo = sourceCalculationDataInfo,
                PresOptions = presentationOptions
            };

            var request = _configuration.CreateRequest<RdbgEvalExprRequest>();
            request.TargetId = _targetsManager.GetTargetId(threadId).ToLight();
            request.CalcWaitingTime = 100;
            request.Expr.Add(calculationSource);

            var response = await _debugServerClient.EvalExpr(request, _cancellation);

            if (response?.Result == null || !response.ResultSpecified)
                await _evaluationTasks[resultId].Task;
            else
                _evaluationTasks[resultId].SetResult(response!.Result.ToList());

            var result = _evaluationTasks[resultId].Task.Result;
            _evaluationTasks.Remove(resultId, out _);
            return result;
        }

        private void CallStackFormedHandler(object? sender, CallStackFormedEventArgs e)
        {
            var threadId = _targetsManager.GetThreadId(e.Info.TargetId);

            if (e.Info.CallStackSpecified)
            {
                _threadsCallStack.TryRemove(threadId, out _);
                _threadsCallStack.TryAdd(threadId, e.Info.CallStack.Reverse().ToList());

                if (_callStackRequestTasks.TryGetValue(threadId, out var task))
                {
                    _callStackRequestTasks.TryRemove(threadId, out _);
                    task.SetResult();
                }
            }

            if (e.Info.HasMessage == true)
                _client.SendOutput(e.Info.Message);

            if (e.Info.SendMessageOnly == true)
                return;

            if (e.Info.StopByBp == true || e.Info.SuspendedByOther)
                _client.SendEvent(new StoppedEvent()
                {
                    Reason = StoppedEvent.ReasonValue.Breakpoint,
                    AllThreadsStopped = true,
                    ThreadId = _targetsManager.GetThreadId(e.Info.TargetId)
                });
        }

        private void RuntimeExceptionHandler(object? sender, RuntimeExceptionArgs e)
        {
            var threadId = _targetsManager.GetThreadId(e.Info.TargetId);

            if (e.Info.CallStackSpecified)
            {
                _threadsCallStack.TryRemove(threadId, out _);
                _threadsCallStack.TryAdd(threadId, e.Info.CallStack.Reverse().ToList());

                if (_callStackRequestTasks.TryGetValue(threadId, out var task))
                {
                    _callStackRequestTasks.TryRemove(threadId, out _);
                    task.SetResult();
                }
            }

            _client.SendEvent(new StoppedEvent()
            {
                Reason = StoppedEvent.ReasonValue.Exception,
                AllThreadsStopped = true,
                ThreadId = _targetsManager.GetThreadId(e.Info.TargetId)
            });

            _client.SendError(e.Info.Exception.Descr);
        }

        private void ExpressionEvaluatedHandler(object? sender, ExpressionEvaluatedEventArgs e)
        {
            if (_evaluationTasks.TryGetValue(e.Info.EvalExprResBaseData.ExpressionResultId, out var task))
                task.SetResult(new() { e.Info.EvalExprResBaseData });
        }

        private int GetVariableReference(int threadId, int frameId, List<SourceCalculationDataItem> path, BaseValueInfoData data)
        {
            var reference = 0;

            if (data.IsExpandable)
                reference = _variableIdentifiers.Add((threadId, frameId, path, ViewInterface.Context));
            else if (data.IsIIndexedCollectionRo)
                reference = _variableIdentifiers.Add((threadId, frameId, path, ViewInterface.Collection));

            return reference;
        }
    }
}
