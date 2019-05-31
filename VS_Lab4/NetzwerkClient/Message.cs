using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetzwerkClientUDP
{
    class Message
    {
        public Verbindung verbindung;

        public enum MsgCommand
        {
            EXIT,
            INFO,
            ECHO
        }

        public int summe;
    }
}
