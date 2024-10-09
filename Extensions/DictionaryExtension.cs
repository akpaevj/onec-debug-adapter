using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Onec.DebugAdapter.Extensions
{
    internal static class DictionaryExtension
    {
        internal static string[]? GetValueAsStringArray(this Dictionary<string, JToken> data, string key)
        {
            if (data.ContainsKey(key))
            {
                var value = data[key];

                if (value is JArray array)
                {
                    var items = new string[array.Count];

                    for ( int i = 0; i < array.Count; i++ )
                    {
                        var token = array[i];
                        if (token.Type == JTokenType.String)
                            items[i] = token.ToString();
                        else
                            throw new Exception("Unexpected token type");
                    }

                    return items;
                }
            }

            return null;
        }
    }
}
