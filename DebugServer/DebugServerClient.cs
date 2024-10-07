using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Onec.DebugAdapter.Services;
using RestSharp;
using System.Net.Http.Headers;

namespace Onec.DebugAdapter.DebugServer
{
    public class DebugServerClient : IDebugServerClient, IDisposable
    {
        private readonly IDebugConfiguration _configuration;
        private RestClient _client = null!;

        public DebugServerClient(IDebugConfiguration configuration)
        {
            _configuration = configuration;

            _configuration.Initialized += (sender, args) => {
                var options = new RestClientOptions($"http://{_configuration.DebugServerHost}:{_configuration.DebugServerPort}/e1crdbg")
                {
                    ThrowOnAnyError = true,
                    UserAgent = "1CV8"
                };

                _client = new RestClient(options, configureSerialization: s =>
                {
                    s.UseSerializer<RequestSerializer>();
                })
                {
                    AcceptedContentTypes = new string[] { ContentType.Xml }
                };
            };
        }

        public async Task Test(CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("rdbgTest");
            request.AddQueryParameter("cmd", "test");

            await _client.PostAsync<RdbgTestRequest>(request, cancellationToken);
        }

        public async Task ClearBreakOnNextStatement(RdbgSetBreamOnNextStatementRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "clearBreakOnNextStatement");
            restRequest.AddXmlBody(request);

            await _client.PostAsync<RdbgSetBreamOnNextStatementRequest>(restRequest, cancellationToken);
        }

        public async Task<RdbgAttachDebugUiResponse?> AttachDebugUI(RdbgAttachDebugUiRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "attachDebugUI");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgAttachDebugUiResponse>(restRequest, cancellationToken);
        }

        public async Task InitSettings(RdbgSetInitialDebugSettingsRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "initSettings");
            restRequest.AddXmlBody(request);

            await _client.PostAsync(restRequest, cancellationToken);
        }

        public async Task<RdbgDetachDebugUiResponse?> DetachDebugUI(RdbgDetachDebugUiRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "detachDebugUI");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgDetachDebugUiResponse>(restRequest, cancellationToken);
        }

        public async Task<RdbgsGetDbgTargetsResponse?> GetDbgTargets(RdbgsGetDbgTargetsRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "getDbgTargets");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgsGetDbgTargetsResponse>(restRequest, cancellationToken);
        }

        public async Task SetBreakpoints(RdbgSetBreakpointsRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "setBreakpoints");
            restRequest.AddXmlBody(request);

            await _client.PostAsync(restRequest, cancellationToken);
        }

        public async Task SetBreakOnRTE(RdbgSetRunTimeErrorProcessingRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "setBreakOnRTE");
            restRequest.AddXmlBody(request);

            await _client.PostAsync(restRequest, cancellationToken);
        }

        public async Task<RdbgGetCallStackResponse?> GetCallStack(RdbgGetCallStackRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "getCallStack");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgGetCallStackResponse>(restRequest, cancellationToken);
        }

        public async Task<RdbgPingDebugUiResponse?> PingDebugUiParams(string dbgUi, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "pingDebugUIParams");
            restRequest.AddQueryParameter("dbgui", dbgUi);
            //restRequest.AddXmlBody(new RdbgPingDebugUiRequest());

            return await _client.PostAsync<RdbgPingDebugUiResponse>(restRequest, cancellationToken);
        }

        public async Task SetAutoAttachSettings(RdbgSetAutoAttachSettingsRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "setAutoAttachSettings");
            restRequest.AddXmlBody(request);

            await _client.PostAsync<RdbgSetAutoAttachSettingsRequest>(restRequest, cancellationToken);
        }

        public async Task<RdbgAttachDetachDbgTargetResponse?> AttachDetachDbgTargets(RdbgAttachDetachDebugTargetsRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "attachDetachDbgTargets");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgAttachDetachDbgTargetResponse>(restRequest, cancellationToken);
        }

        public async Task<RdbgEvalLocalVariablesResponse?> EvalLocalVariables(RdbgEvalLocalVariablesRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "evalLocalVariables");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgEvalLocalVariablesResponse>(restRequest, cancellationToken);
        }

        public async Task<RdbgEvalExprResponse?> EvalExpr(RdbgEvalExprRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "evalExpr");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgEvalExprResponse>(restRequest, cancellationToken);
        }

        public async Task<RdbgStepResponse?> Step(RdbgStepRequest request, CancellationToken cancellationToken = default)
        {
            var restRequest = new RestRequest("rdbg");
            restRequest.AddQueryParameter("cmd", "step");
            restRequest.AddXmlBody(request);

            return await _client.PostAsync<RdbgStepResponse>(restRequest, cancellationToken);
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
