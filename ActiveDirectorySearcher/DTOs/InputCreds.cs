using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectorySearcher.DTOs
{
    public record InputCreds(string Domain, string UserName, string Password, int Port, string License);
}
