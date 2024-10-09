using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Onec.DebugAdapter.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.V8
{
    internal class Debuggee : IDisposable
    {
        private readonly IDebugConfiguration _configuration;
        private DebugProtocolClient _client;
        private bool _needSendEvent = true;

        private Process? _process;
        private bool disposedValue;

        public Debuggee(IDebugConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Run(DebugProtocolClient client)
        {
            _client = client;

            var arguments = new[]
            {
                $"/IBNAME \"{_configuration.InfoBase.Name}\"",
                "/TCOMP -SDC",
                "/DisableStartupMessages",
                "/DisplayPerformance",
                "/TechnicalSpecialistMode",
                "/DEBUG -http -attach",
                $"/DEBUGGERURL \"http://{_configuration.DebugServerHost}:{_configuration.DebugServerPort}\"",
                "/O Normal"
            };

            var exePath = Path.Join(_configuration.PlatformBin, "1cv8c.exe");
            if (!File.Exists(exePath))
                throw new Exception("Исполняемый файл клиента 1С не найден");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(exePath, string.Join(" ", arguments)),
                EnableRaisingEvents = true
            };
            process.Exited += DebuggeeExited;
            process.Start();
        }

        private void DebuggeeExited(object? sender, EventArgs e)
        {
            if (_needSendEvent)
                _client?.SendEvent(new TerminatedEvent());
        }

        public void Stop()
        {
            _needSendEvent = false;
            _process?.Kill();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                }

                Stop();
                disposedValue = true;
            }
        }

        // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
        ~Debuggee()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
