using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onec.DebugAdapter.Extensions
{
    public static class StringExtensions
    {
        public static string CapitalizeFirstChar(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s; 

            return string.Create(s.Length, s, (chars, state) =>
            {
                state.AsSpan().CopyTo(chars);      // No slicing to save some CPU cycles
                chars[0] = char.ToUpper(chars[0]);
            });
        }
    }
}
