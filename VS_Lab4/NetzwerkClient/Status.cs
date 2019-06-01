using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetzwerkClientUDP
{
    class Status
    {
        public int AnzahlNachbarn { get; }
        public bool informed { get; set; }
        public Verbindung upward_Node { get; set; }
        public int Speicher { get; }

        public Status(int AnzahlNachbarn, int Speicher, Verbindung upward_Node = null, bool informed = false)
        {
            this.AnzahlNachbarn = AnzahlNachbarn;
            this.Speicher = Speicher;
            this.upward_Node = upward_Node;
            this.informed = informed;
        }
    }
}
