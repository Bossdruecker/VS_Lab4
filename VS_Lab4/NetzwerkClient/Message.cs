using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetzwerkClientUDP
{
    class Message
    {
        public enum MsgCommand
        {
            EXIT,
            MSG,
            INFO,
            ECHO
        }

        public Verbindung verbindung;
        public MsgCommand command;
        public int sum;
        public string payload = "";
        public int neightInformed;

        public Message(Verbindung verbindung, MsgCommand command, int sum, int neightInformed=0, string payload="")
        {
            this.verbindung = verbindung;
            this.command = command;
            this.sum = sum;
            this.payload = payload;
            this.neightInformed = neightInformed;
        }
    }
}
