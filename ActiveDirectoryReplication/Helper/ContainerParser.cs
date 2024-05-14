using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectoryReplication.Helper
{
    public static class ContainerParser
    {
        public static IEnumerable<string> Parse(string text)
        {
            try
            {
                char delimiter = ';';
                var substrings = text.Split(delimiter).Where(s => !string.IsNullOrEmpty(s));
                return substrings.Select(s => s.Trim());
            }
            catch (Exception ex)
            {
                throw new Exception("Container Parsing Error: " + ex.Message);
            }

        }

    }
}
