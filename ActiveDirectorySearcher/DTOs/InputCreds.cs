using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectorySearcher.DTOs
{
    public class InputCreds
    {
        public InputCreds(string domain, string userName, string password, int port,string license) { 
            this.Domain = domain;
            this.UserName = userName;
            this.Password = password;
            this.Port = port;
            this.License = license;
        }
        public string Domain{ get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
        public string License { get; set; }
    }
}
