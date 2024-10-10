using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace Onec.DebugAdapter.Services
{
    public class TcpDebugAdapterService : BackgroundService
    {
        private readonly ILogger<TcpDebugAdapterService> _logger;
        private readonly V8DebugAdapter _debugAdapter;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly int _port;

        public TcpDebugAdapterService(V8DebugAdapter debugAdapter, IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration, ILogger<TcpDebugAdapterService> logger)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _debugAdapter = debugAdapter;
            _logger = logger;

            _port = configuration.GetValue("port", 4711);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Starting listening for client on {_port} port");
                var listener = TcpListener.Create(_port);
                listener.Start();

                using var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _logger.LogInformation($"Client connected ({client.Client.RemoteEndPoint})");

                using var stream = client.GetStream();

                await _debugAdapter.Run(stream, stream, stoppingToken);

                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                    _hostApplicationLifetime.StopApplication();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            _logger.LogInformation($"Stopping adapter host");
        }
    }
}
