using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetzwerkClientUDP
{
    class Program
    {
        //22 Kanten 12 Knoten
        public static int[][] nodes = new int[][]
        {
            new int[] { 2,3,7 },
            new int[] { 1,3,4,6,10 },
            new int[] { 1,2 },
            new int[] { 2,6,8,9,11,12 },
            new int[] { 9,11 },
            new int[] { 2,4,9,10 },
            new int[] { 1,10,12 },
            new int[] { 4,9,12 },
            new int[] { 4,5,6,8,11 },
            new int[] { 2,6,7,12 },
            new int[] { 4,5,9 },
            new int[] { 4,7,8,10 }
        };

        public static bool exit = true;

        public struct UdpState
        {
            public UdpClient u;
            public IPEndPoint e;
        }

        static void Main(string[] args)
        {
            List<Verbindung> network = new List<Verbindung>();
            Verbindung myCon = new Verbindung();
            if (args.Length == 0)
            {
                UdpClient udpServer = new UdpClient(5000);  //Port ist 5000
                myCon = DefineNode(GetLocalIP(), 5000, 0);
                //Console.ReadKey();

                for (int i = 1; i < 13; i++)
                {
                    int port = 5000 + i;
                    Process.Start("NetzwerkClient.exe", port.ToString() + " " + i.ToString());
                }

                bool exitintern = true;

                while (exitintern)
                {
                    Byte[] data = ReceiveData(udpServer, 5000);
                    string returnData = Encoding.ASCII.GetString(data);
                    Console.WriteLine(returnData);
                    Verbindung refNode = JsonConvert.DeserializeObject<Verbindung>(returnData);
                    network.Add(refNode);
                    if (network.Count == 12)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            Verbindung temp = network.Find(r => r.NodeNr == (i + 1));
                            SendNeighbor(udpServer, temp, network);
                        }
                        Console.WriteLine("Das Netzwerk wurde erstellt und ist jetzt online");
                    }
                }
            }
            else
            {
                //Console.ReadKey();
                UdpClient udpServer = new UdpClient(int.Parse(args[0]));
                myCon = DefineNode(GetLocalIP(), int.Parse(args[0]), int.Parse(args[1]));


                string output = JsonConvert.SerializeObject(myCon);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);

                IPEndPoint ipEndPoint = new IPEndPoint(GetLocalIP(), 5000);
                //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                SendData(udpServer, ipEndPoint, sendByte);
                Console.WriteLine(myCon.NodeNr.ToString() + " Knoten ist online!");


                //receive Modus
                while (network.Count != nodes[int.Parse(args[1]) - 1].Length)
                {
                    //var groupEP = new IPEndPoint(IPAddress.Any, int.Parse(args[0]));
                    Byte[] data = ReceiveData(udpServer, int.Parse(args[0]));
                    string returnData = Encoding.ASCII.GetString(data);
                    Console.WriteLine(returnData);
                    Verbindung refNode = JsonConvert.DeserializeObject<Verbindung>(returnData);
                    network.Add(refNode);
                }

                Console.WriteLine(myCon.NodeNr.ToString() + " hat alle Kanten empfange und ist bereit");                         // <= ab hier sind die knoten bereit für den verteilten Algorithmus

                Status myStatus = new Status(network.Count, int.Parse(args[1]));

                Thread myReceiveThread = new Thread(() => ReceiveThread(udpServer, myCon, network, ref myStatus));
                myReceiveThread.IsBackground = true;
                myReceiveThread.Start();

                while (exit)
                {
                    string input = Console.ReadLine();
                    if (input == "!Start")
                    {
                        StartAlgo(udpServer, myCon, network, ref myStatus);
                        exit = false;
                    }
                }
                Console.ReadKey();
            }
        }

        private static IPAddress GetLocalIP()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            return IPAddress.Parse("127.0.0.1");
        }

        private static Verbindung DefineNode(IPAddress addr, int port, int num)
        {
            Verbindung verb = new Verbindung
            {
                Addr = addr.ToString(),
                Port = port,
                NodeNr = num
            };
            return verb;
        }

        private static void SendNeighbor(UdpClient udpServer, Verbindung verb, List<Verbindung> network)
        {
            for (int i = nodes[verb.NodeNr - 1].Length; i > 0; i--)
            {
                Verbindung temp = network.Find(r => r.NodeNr == nodes[verb.NodeNr - 1][i - 1]);
                string output = JsonConvert.SerializeObject(temp);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);
                SendData(udpServer, new IPEndPoint(IPAddress.Parse(verb.Addr), verb.Port), sendByte);
            }
        }

        //Funktion Daten empfangen und als Byte Array zurückgeben
        private static Byte[] ReceiveData(UdpClient udpServer, int port)
        {
            Byte[] data = null;
            var groupEP = new IPEndPoint(IPAddress.Any, port);
            data = udpServer.Receive(ref groupEP);
            return data;
        }

        //Byte Array schicken
        private static void SendData(UdpClient udpServer, IPEndPoint ipEndPoint, Byte[] sendByte)
        {
            udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
        }

        private static void UpdateStatus(ref Status st, bool inf, Verbindung upwardNode)
        {
            st.Informed = inf;
            st.Upward_Node = upwardNode;
        }

        private static void IncNeightInformed(ref Message msg)
        {
            msg.neightInformed++;
        }

        private static void ChangeMsgCommand(ref Message msg, Message.MsgCommand command)
        {
            msg.command = command;
        }

        private static void ChangeCon(ref Message msg, Verbindung verb)
        {
            msg.verbindung = verb;
        }

        private static void AddSpeicher(ref Message msg, int speicher)
        {
            msg.sum = speicher;
        }

        private static void ReceiveThread(UdpClient server, Verbindung my, List<Verbindung> net, ref Status st)
        {
            //bool exit = true;
            while (exit)
            {
                Byte[] data = ReceiveData(server, my.Port);
                //Nachricht empfangen 
                //Console.WriteLine("Habe eine nachricht empfangen");
                //Console.ReadKey();
                string returnData = Encoding.ASCII.GetString(data);
                Console.WriteLine(returnData);
                Message msg = JsonConvert.DeserializeObject<Message>(returnData);

                switch (msg.command)
                {
                    case Message.MsgCommand.EXIT:
                        break;
                    case Message.MsgCommand.MSG:
                        break;
                    case Message.MsgCommand.INFO:
                        st.CountInformed++;
                        if (st.Informed == false)
                        {
                            st.online = true;
                            st.Informed = true;
                            st.Upward_Node = msg.verbindung;
                            int inc = msg.neightInformed + 1;
                            foreach (Verbindung element in net)
                            {
                                msg = new Message(my, Message.MsgCommand.INFO, 0, inc);
                                string output = JsonConvert.SerializeObject(msg);
                                Byte[] sendByte = Encoding.ASCII.GetBytes(output);
                                Console.WriteLine("Send Info an: " + element.Port.ToString());
                                SendData(server, new IPEndPoint(IPAddress.Parse(element.Addr), element.Port), sendByte);
                            }
                        }
                        if (st.AnzahlNachbarn == st.CountInformed && st.online == true)
                        {
                            if (st.Initiator == true)
                            {
                                st.Speicher += msg.sum;
                                Console.WriteLine(st.Speicher.ToString());
                                exit = false;
                                Console.WriteLine("Echo erhalten Thread ist hiermit beendet");
                            }
                            else
                            {
                                msg = new Message(my, Message.MsgCommand.ECHO, msg.sum + st.Speicher, msg.neightInformed);
                                string output = JsonConvert.SerializeObject(msg);
                                Byte[] sendByte = Encoding.ASCII.GetBytes(output);
                                Console.WriteLine("Send Echo Initiator: " + st.Initiator.ToString());
                                Console.WriteLine("Send Echo to: " + msg.verbindung.Port.ToString());
                                SendData(server, new IPEndPoint(IPAddress.Parse(st.Upward_Node.Addr), st.Upward_Node.Port), sendByte);
                            }
                        }
                        break;
                    case Message.MsgCommand.ECHO:
                        if (st.online)
                        {
    st.                     Speicher += msg.sum;
                            msg = new Message(my, Message.MsgCommand.ECHO, msg.sum + st.Speicher, msg.neightInformed);
                            Console.WriteLine("receive Echo von: "+msg.verbindung.Port.ToString());
                        }
                        st.online = false;
                        break;
                    default:
                        break;
                }
                //Console.ReadKey();
            }
        }

        private static void StartAlgo(UdpClient server, Verbindung myCon, List<Verbindung> netw, ref Status myStatus)
        {
            myStatus.Initiator = true;
            Message msg = new Message(myCon, Message.MsgCommand.INFO, 0);
            string output = JsonConvert.SerializeObject(msg);
            Byte[] sendByte = Encoding.ASCII.GetBytes(output);
            foreach (Verbindung element in netw)
            {
                SendData(server, new IPEndPoint(IPAddress.Parse(element.Addr), element.Port), sendByte);
                //Console.ReadKey();
            }
        }
    }
}

