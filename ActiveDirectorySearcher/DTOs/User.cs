using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectorySearcher.DTOs
{

    public class User
    {
        public string Path { get; set; }
        public UserProperties Properties { get; set; }
    }

    public class UserProperties
    {
        public Int64[] badpasswordtime { get; set; }
        public string[] objectclass { get; set; }
        public int[] usnchanged { get; set; }
        public string[] department { get; set; }
        public int[] usncreated { get; set; }
        public DateTime[] whencreated { get; set; }
        public int[] primarygroupid { get; set; }
        public string[] name { get; set; }
        public DateTime[] dscorepropagationdata { get; set; }
        public DateTime[] whenchanged { get; set; }
        public long[] pwdlastset { get; set; }
        public string[] description { get; set; }
        public int[] useraccountcontrol { get; set; }
        public string[] cn { get; set; }
        public Int64[] lastlogon { get; set; }
        public int[] samaccounttype { get; set; }
        public string[] objectcategory { get; set; }
        public string[] objectsid { get; set; }
        public int[] badpwdcount { get; set; }
        public int[] lastlogoff { get; set; }
        public string[] mail { get; set; }
        public string[] distinguishedname { get; set; }
        public string[] memberof { get; set; }
        public int[] countrycode { get; set; }
        public long[] accountexpires { get; set; }
        public string[] samaccountname { get; set; }
        public int[] codepage { get; set; }
        public int[] logoncount { get; set; }
        public string[] adspath { get; set; }
        public string[] displayname { get; set; }
        public string[] objectguid { get; set; }
        public string[] givenname { get; set; }
        public int[] instancetype { get; set; }
    }

}
