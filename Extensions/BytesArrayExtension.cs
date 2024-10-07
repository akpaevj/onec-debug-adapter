using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.Extensions
{
    internal static class BytesArrayExtension
    {
        internal static string GetUTF8String(this byte[] data)
            => data == null ? string.Empty : Encoding.UTF8.GetString(data);
    }
}
