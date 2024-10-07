using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Onec.DebugAdapter.DebugProtocol;
using Onec.DebugAdapter.DebugServer;
using Onec.DebugAdapter.Metadata;
using Onec.DebugAdapter.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using System.Xml;

namespace Onec.DebugAdapter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureDefaults(args)
                .ConfigureServices((context, sc) =>
                {
                    sc.AddTransient<IDebugServerClient, DebugServerClient>();
                    sc.AddSingleton<IDebugConfiguration, DebugConfiguration>();
                    sc.AddSingleton<IMetadataProvider, MetadataProvider>();
                    sc.AddSingleton<IDebugServerListener, DebugServerListener>();
                    sc.AddSingleton<IDebugTargetsManager, DebugTargetsManager>();
                    sc.AddSingleton<IStoppingManager, StoppingManager>();
                    sc.AddSingleton<V8DebugAdapter>();
                    sc.AddSingleton<IDebugAdapterExtender, DebugAdapterExtender>();

                    if (context.Configuration.GetValue("debug", false))
                        sc.AddHostedService<TcpDebugAdapterService>();
                    else
                        sc.AddHostedService<ConsoleDebugAdapterService>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
