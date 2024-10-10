using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.V8
{
    internal class InfoBasesReader
    {
        public static async Task<List<InfoBaseItem>> Read()
        {
            var result = new List<InfoBaseItem>();

            var basePath = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => Environment.GetEnvironmentVariable("APPDATA"),
                _ => Environment.GetEnvironmentVariable("HOME")
            };

            var iBasesPath = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => Path.Join(basePath, @"1C\1CEStart\ibases.v8i"),
                _ => Path.Join(basePath, @".1C\1CEStart\ibases.v8i"),
            };

            if (File.Exists(iBasesPath))
            {
                var content = await File.ReadAllTextAsync(iBasesPath);
                var infoBasesParams = Regex.Split(content, "(?=\\[.*\\])").Where(c => !string.IsNullOrEmpty(c.Trim())).ToList();
                infoBasesParams.ForEach(infoBaseParams =>
                {
                    var lines = Regex.Split(infoBaseParams, "\r?\n").Where(c => !string.IsNullOrEmpty(c.Trim())).ToArray();
                    var name = lines[0][1..^1];

                    var properties = new Dictionary<string, string?>();
                    foreach(var line in lines[1..])
                    {
                        var i  = line.IndexOf('=');
                        if (i < 0)
                            properties.Add(line, null);
                        else
                            properties.Add(line[..i], line[(i + 1)..]);
                    }
                    result.Add(new InfoBaseItem(name, properties));
                });
            }

            return result;
        } 
    }
}
