using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetzwerkClientUDP
{
    class UdpState
    {
        public UdpClient u { get; set; }
        public IPEndPoint e { get; set; }
    }
}
