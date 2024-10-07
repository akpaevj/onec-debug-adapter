using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.Services
{
    public class ConsoleDebugAdapterService : BackgroundService
    {
        private readonly ILogger<ConsoleDebugAdapterService> _logger;
        private readonly V8DebugAdapter _debugAdapter;

        public ConsoleDebugAdapterService(V8DebugAdapter debugAdapter, ILogger<ConsoleDebugAdapterService> logger)
        {
            _debugAdapter = debugAdapter;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _debugAdapter.Run(Console.OpenStandardInput(), Console.OpenStandardOutput(), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }
    }
}
