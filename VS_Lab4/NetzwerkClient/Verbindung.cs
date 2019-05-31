using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetzwerkClientUDP
{
    class Verbindung
    {
        public string addr { get; set; }
        public int port { get; set; }
        public int nodeNr { get; set; }
    }
}
