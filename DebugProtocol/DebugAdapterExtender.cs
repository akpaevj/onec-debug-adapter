using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.DebugProtocol
{
    public class DebugAdapterExtender : IDebugAdapterExtender
    {
        private CancellationToken _cancellation;
        private readonly IDebugTargetsManager _debugTargetsManager;
        private readonly IDebugServerListener _debugServerListener;
        private DebugProtocolClient _client = null!;

        public DebugAdapterExtender(IDebugTargetsManager debugTargetsManager, IDebugServerListener serverListener)
        {
            _debugServerListener = serverListener;
            _debugTargetsManager = debugTargetsManager;
        }

        public void Init(DebugProtocolClient debugProtocolClient, CancellationToken cancellationToken)
        {
            _cancellation = cancellationToken;
            _client = debugProtocolClient;

            _client.RegisterRequestType<SetAutoAttachTargetTypesRequest, SetAutoAttachTargetTypesArguments>(async handler =>
            {
                try
                {
                    var types = handler.Arguments.Types.Select(c => Enum.Parse<DebugTargetType>(c));
                    await _debugTargetsManager.SetAutoAttachTargetTypes(types.ToList());
                }
                catch (System.Exception ex)
                {
                    handler.SetError(new ProtocolException("Ошибка установки автоподключаемых типов предметов отладки", ex));
                }
            });

            _client.RegisterRequestType<AttachDebugTargetRequest, AttachDebugTargetArguments>(async handler =>
            {
                try
                {
                    var targets = await _debugTargetsManager.GetDebugTargets();
                    var target = targets.FirstOrDefault(c => c.Id == handler.Arguments.Id);

                    if (target != null)
                    {
                        var toAttachTargets = new List<DebugTargetId>() { target };
                        await _debugTargetsManager.AttachDebugTargets(toAttachTargets);
                    }
                }
                catch (System.Exception ex)
                {
                    handler.SetError(new ProtocolException("Ошибка подключения предмета отладки", ex));
                }
            });

            _client.RegisterRequestType<DebugTargetsRequest, DebugTargetsArguments, DebugTargetsResponse>(async handler =>
            {
                try
                {
                    var response = await _debugTargetsManager.GetDebugTargets();
                    var attached = _debugTargetsManager.GetAttachedDebugTargets();

                    handler.SetResponse(new DebugTargetsResponse()
                    {
                        Items = response.Where(i => attached.FirstOrDefault(c => c.Id == i.Id) == null).Select(c => new DebugTargetItem()
                        {
                            Id = c.Id,
                            Seance = c.SeanceNo.ToString(),
                            Type = c.TargetType.GetTypePresentation(),
                            User = c.GetUserName()
                        }).ToArray()
                    });
                }
                catch (System.Exception ex)
                {
                    handler.SetError(new ProtocolException("Ошибка получения списка предметов отладки", ex));
                }
            });

            _debugServerListener.ShowMetadataObject += ShowMetadataObjectHandler;
        }

        private void ShowMetadataObjectHandler(object? sender, ShowMetadataObjectArgs e)
        {
            
        }
    }
}
