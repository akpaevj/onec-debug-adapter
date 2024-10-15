using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Extensions;
using Onec.DebugAdapter.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.V8
{
	public class DebugServerProcess : IDisposable
	{
		private readonly IDebugConfiguration _configuration;
		private DebugProtocolClient _client = null!;
		private bool _needSendEvent = true;

		private Process? _process;
		private bool disposedValue;

		public DebugServerProcess(IDebugConfiguration configuration)
		{
			_configuration = configuration;
		}

		public async Task Run(DebugProtocolClient client)
		{
			_client = client;


			var notifyFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			var arguments = new[]
			{
				$"--addr={_configuration.DebugServerHost}",
				$"--portRange=1550:1559",
				$"--ownerPID={Environment.ProcessId}",
				$"--notify=\"{notifyFilePath}\""
			};

			var exePath = Path.Join(_configuration.PlatformBin, "dbgs.exe");
			if (!File.Exists(exePath))
				throw new System.Exception("Исполняемый файл сервера отладки 1С не найден");

			_process = new Process
			{
				StartInfo = new ProcessStartInfo(exePath, string.Join(" ", arguments))
				{
					RedirectStandardError = true
				},
				EnableRaisingEvents = true,
			};
			_process.Exited += DebuggerExited;
			_process.Start();

			while (!_process.HasExited)
			{
				if (File.Exists(notifyFilePath))
				{
					try
					{
						using var stream = File.Open(notifyFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
						using var reader = new StreamReader(stream);

						if (stream.Length > 0)
						{
							var notifyData = reader.ReadToEnd();
							_configuration.SetDebugServerPort(int.Parse(notifyData.Split(':')[1]));
							break;
						}
					}
					catch (System.Exception) { }
				}
				else
					await Task.Delay(25);
			}

			if (File.Exists(notifyFilePath))
				File.Delete(notifyFilePath);
		}

		private void DebuggerExited(object? sender, EventArgs e)
		{
			if (_needSendEvent)
			{
				if (_process?.ExitCode != 0)
					_client.SendError(_process?.StandardError.ReadToEnd() ?? "");

				_client?.SendEvent(new TerminatedEvent());
			}
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

		~DebugServerProcess()
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
