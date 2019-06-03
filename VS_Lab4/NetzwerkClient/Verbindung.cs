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
        public string Addr { get; set; }
        public int Port { get; set; }
        public int NodeNr { get; set; }
    }
}
