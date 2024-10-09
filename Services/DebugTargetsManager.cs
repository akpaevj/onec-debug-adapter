using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using Onec.DebugAdapter.DebugProtocol;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using System.Threading;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace Onec.DebugAdapter.Services
{
    public class DebugTargetsManager : IDebugTargetsManager
    {
        private CancellationToken _cancellation;

        private readonly IDebugConfiguration _configuration;
        private readonly IDebugServerClient _debugServerClient;
        private readonly IDebugServerListener _serverListener;
        private DebugProtocolClient _client = null!;

        private readonly Dictionary<string, int> _threadIds = new();
        private readonly Dictionary<int, DebugTargetId> _attachedTargets = new();
        private readonly List<DebugTargetType> _autoAttachTargetTypes = new();

        public DebugTargetsManager(IDebugConfiguration debugConfiguration, IDebugServerClient debugServerClient, IDebugServerListener debugServerListener)
        {
            _configuration = debugConfiguration;
            _debugServerClient = debugServerClient;
            _serverListener = debugServerListener;

            _serverListener.DebugTargetEvent += DebugTargetEventHandler;
        }

        public async Task Run(DebugProtocolClient client, CancellationToken cancellationToken)
        {
            _cancellation = cancellationToken;
            _client = client;

            await SetAutoAttachTargetTypes(_configuration.InitialTargetTypes.ToList());
        }

        public DebugTargetId GetTargetId(int threadId)
            => _attachedTargets[threadId];

        public ThreadsResponse GetThreads(ThreadsArguments args)
        {
            return new ThreadsResponse()
            {
                Threads = _attachedTargets.Select(c => new Thread()
                {
                    Id = c.Key,
                    Name = $"{c.Value.TargetType.GetTypePresentation()} ({c.Value.GetUserName()}, {c.Value.SeanceNo})"
                }).ToList()
            };
        }
        public DebugTargetId[] GetAttachedDebugTargets()
            => _attachedTargets.Values.ToArray();

        public async Task<DebugTargetId[]> GetDebugTargets()
        {
            var response = await _debugServerClient.GetDbgTargets(_configuration.CreateRequest<RdbgsGetDbgTargetsRequest>(), _cancellation);

            return response!.Id.ToArray();
        }

        public async Task SetAutoAttachTargetTypes(List<DebugTargetType> types)
        {
            try
            {
                var request = _configuration.CreateRequest<RdbgSetAutoAttachSettingsRequest>();
                request.AutoAttachSettings = new DebugAutoAttachSettings();
                types.ForEach(c => request.AutoAttachSettings.TargetType.Add(c));

                await _debugServerClient.SetAutoAttachSettings(request, _cancellation);

                _autoAttachTargetTypes.Clear();
                _autoAttachTargetTypes.AddRange(types);
            }
            catch (System.Exception ex)
            {
                _client.SendError("Ошибка установки параметров автоматического подключения предметов отладки", ex);
                return;
            }
        }

        private async void DebugTargetEventHandler(object? sender, DebugTargetEventArgs e)
        {
            try
            {
                if (e.Started)
                    await AttachDebugTargets(new() { e.TargetId });
                else
                    await DetachDebugTargets(new() { e.TargetId.ToLight() }, false);
            }
            catch (System.Exception ex)
            {
                _client.SendError("Не удалось обработать событие предмета отладки", ex);
            }
        }

        public async Task AttachDebugTargets(List<DebugTargetId> debugTargets)
        {
            if (debugTargets.Count == 0) 
                return;

            var request = _configuration.CreateRequest<RdbgAttachDetachDebugTargetsRequest>();
            request.Attach = true;
            debugTargets.ForEach(c => request.Id.Add(c.ToLight()));

            await _debugServerClient.ClearBreakOnNextStatement(_configuration.CreateRequest<RdbgSetBreamOnNextStatementRequest>(), _cancellation);

            var response = await _debugServerClient.AttachDetachDbgTargets(request, _cancellation);

            foreach (var debugTarget in debugTargets)
            {
                var threadId = AddThreadId(debugTarget);

                _client.SendEvent(new ThreadEvent()
                {
                    Reason = ThreadEvent.ReasonValue.Started,
                    ThreadId = threadId
                });
            }

            _client.SendEvent(new DebugTargetsUpdatedEvent());
        }

        public async Task DetachDebugTargets(List<DebugTargetIdLight> debugTargets, bool sendDetachRequest)
        {
            var toHandle = debugTargets.Where(c => _threadIds.ContainsKey(c.Id)).ToList();

            if (toHandle.Count == 0)
                return;

            var request = _configuration.CreateRequest<RdbgAttachDetachDebugTargetsRequest>();
            request.Attach = false;
            toHandle.ForEach(c => request.Id.Add(c));

            if (sendDetachRequest)
                await _debugServerClient.AttachDetachDbgTargets(request, _cancellation);

            toHandle.ForEach(c =>
            {
                var threadId = GetThreadId(c);

                RemoveThreadId(c);

                _client.SendEvent(new ThreadEvent()
                {
                    Reason = ThreadEvent.ReasonValue.Exited,
                    ThreadId = threadId
                });
            });

            _client.SendEvent(new DebugTargetsUpdatedEvent());
        }

        public List<DebugTargetType> GetAutoAttachTargetTypes()
            => _autoAttachTargetTypes;

        public int GetThreadId(DebugTargetIdLight debugTargetId)
        {
            lock (_threadIds)
            {
                if (_threadIds.TryGetValue(debugTargetId.Id, out var id))
                    return id;
                else
                    throw new System.Exception($"Поток для предмета отладки ({debugTargetId.Id}) не зарегистрирован");
            }
        }

        private void RemoveThreadId(DebugTargetIdLight debugTargetId)
        {
            lock (_threadIds)
            {
                _threadIds.Remove(debugTargetId.Id, out var threadId);
                _attachedTargets.Remove(threadId);
            }
        }

        private int AddThreadId(DebugTargetId debugTargetId)
        {
            lock (_threadIds)
            {
                if (_threadIds.TryGetValue(debugTargetId.Id, out _))
                    throw new System.Exception($"Поток для предмета отладки ({debugTargetId.Id}) уже зарегистрирован");

                var id = _threadIds.Count + 1;
                _threadIds.Add(debugTargetId.Id, id);

                _attachedTargets.Add(id, debugTargetId);

                return id;
            }
        }

        public bool DebugTargetAttached(DebugTargetIdLight debugTarget)
            => _threadIds.ContainsKey(debugTarget.Id);
    }
}
