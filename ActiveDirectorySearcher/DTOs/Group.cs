using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveDirectorySearcher.DTOs
{

    public class Group
    {
        public string Path { get; set; }
        public GroupProperties Properties { get; set; }
    }

    public class GroupProperties
    {
        public int[] instancetype { get; set; }
        public int[] samaccounttype { get; set; }
        public int[] usncreated { get; set; }
        public int[] usnchanged { get; set; }
        public string[] objectcategory { get; set; }
        public string[] member { get; set; }
        public string[] objectclass { get; set; }
        public string[] samaccountname { get; set; }
        public string[] distinguishedname { get; set; }
        public string[] objectguid { get; set; }
        public string[] adspath { get; set; }
        public string[] objectsid { get; set; }
        public DateTime[] dscorepropagationdata { get; set; }
        public string[] description { get; set; }
        public int[] admincount { get; set; }
        public string[] cn { get; set; }
        public string[] name { get; set; }
        public int[] systemflags { get; set; }
        public int[] grouptype { get; set; }
        public DateTime[] whenchanged { get; set; }
        public DateTime[] whencreated { get; set; }
        public bool[] iscriticalsystemobject { get; set; }
    }

}
