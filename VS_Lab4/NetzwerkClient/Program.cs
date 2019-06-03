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

        static readonly int exitCode = 0;
        static int gesamtSpeicher = 0;

        static void Main(string[] args)
        {
            List<Verbindung> network = new List<Verbindung>();
            Verbindung myCon = new Verbindung();
            if (args.Length == 0)
            {
                UdpClient udpServer = new UdpClient(5000);  //Port ist 5000
                myCon = DefineNode(GetLocalIP(), 5000, 0);

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
                        Console.WriteLine("Wenn sie das netzwerk herunterfahren wollen bitte #ja# tippen");
                        string c = Console.ReadLine();
                        if(c == "#ja#")
                        {
                            Message msg = new Message(myCon, Message.MsgCommand.EXIT, 0);
                            string input = JsonConvert.SerializeObject(msg);
                            Byte[] inputData = Encoding.ASCII.GetBytes(input);
                            SendDataToNeight(udpServer, inputData, network);
                            exitintern = false;
                        }
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
                SendData(udpServer, ipEndPoint, sendByte);
                Console.WriteLine(myCon.NodeNr.ToString() + " Knoten ist online!");


                //receive Modus
                while (network.Count != nodes[int.Parse(args[1]) - 1].Length)
                {
                    var groupEP = new IPEndPoint(IPAddress.Any, int.Parse(args[0]));
                    Byte[] data = ReceiveData(udpServer, int.Parse(args[0]));
                    string returnData = Encoding.ASCII.GetString(data);
                    Console.WriteLine(returnData);
                    Verbindung refNode = JsonConvert.DeserializeObject<Verbindung>(returnData);
                    network.Add(refNode);
                }

                Console.WriteLine(myCon.NodeNr.ToString() + " hat alle Kanten empfange und ist bereit");                         // <= ab hier sind die knoten bereit für den verteilten Algorithmus

                Status st = new Status(network.Count, int.Parse(args[1]));
                Thread ReceiveThread = new Thread(() => ReceiveDataThread(udpServer, int.Parse(args[0]), myCon, network, ref st))
                {
                    IsBackground = true
                };
                ReceiveThread.Start();

                Console.WriteLine("Wenn sie den Algo Starten wollen bitte !Start tippen");

                string start = Console.ReadLine();
                if(start == "!Start")
                {
                    Message msg = new Message(myCon, Message.MsgCommand.INFO, 0);
                    string input = JsonConvert.SerializeObject(msg);
                    Byte[] inputData = Encoding.ASCII.GetBytes(input);
                    SendDataToNeight(udpServer, inputData, network);
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

        //Nachrichten empfangen <= Thread Methode
        private static void ReceiveDataThread(UdpClient udpServer, int port, Verbindung con, List<Verbindung> netw, ref Status st)
        {
            bool exit = true;
            string output;
            Byte[] sendByte;

            while (exit)
            {
                //Thread.Sleep(1000);
                var groupEP = new IPEndPoint(IPAddress.Any, port);
                Byte[] data = udpServer.Receive(ref groupEP);
                string returnData = Encoding.ASCII.GetString(data);
                Message msg = JsonConvert.DeserializeObject<Message>(returnData);
                switch (msg.command)
                {
                    case Message.MsgCommand.EXIT:
                        Environment.Exit(exitCode);
                        break;
                    case Message.MsgCommand.MSG:
                        Console.WriteLine("msg from: " + msg.verbindung.Port + ": " + msg.payload);
                        break;
                    case Message.MsgCommand.INFO:
                        Console.WriteLine("Info von " + msg.verbindung.Port.ToString() + " bekommen!");
                        IncNeightInformed(ref msg);
                        Console.WriteLine("out of is informed " + st.informed.ToString() + "incNeightInformed: " + msg.neightInformed.ToString());
                        int node = msg.verbindung.Port;
                        if (st.informed == false)
                        {
                            st.informed = true;
                            Console.WriteLine("in of is informed " + st.informed.ToString());
                            st.upward_Node = msg.verbindung;
                            node = msg.verbindung.Port;

                            //msg = new Message(con, Message.MsgCommand.INFO, 0);
                            ChangeMsgCommand(ref msg, Message.MsgCommand.INFO);
                            ChangeCon(ref msg, con);
                            output = JsonConvert.SerializeObject(msg);
                            sendByte = Encoding.ASCII.GetBytes(output);
                            netw.ForEach(delegate (Verbindung v)
                            {
                                if (v.Port != node)
                                {
                                    SendData(udpServer, new IPEndPoint(IPAddress.Parse(v.Addr), v.Port), sendByte);
                                    Console.WriteLine("Info an " + v.Port.ToString() + " gesendet!");
                                }
                            });
                            Console.WriteLine("in of is informed finisch Info on Neighs");
                        }
                        else
                        {
                            Console.WriteLine("Bin informiert haben jedoch von " + msg.verbindung.Port.ToString() + "Naschricht erhalten");
                            if (st.AnzahlNachbarn == msg.neightInformed)
                            {
                                Console.WriteLine("Alle Nachbarn wurden schon informiert");
                                if (st.upward_Node == con)
                                {
                                    gesamtSpeicher += st.Speicher;
                                    Console.WriteLine("Speicher beträgt: " + gesamtSpeicher.ToString());
                                }
                                else
                                {
                                    gesamtSpeicher += st.Speicher;
                                    //msg = new Message(con, Message.MsgCommand.ECHO, gesamtSpeicher);
                                    ChangeMsgCommand(ref msg, Message.MsgCommand.ECHO);
                                    ChangeCon(ref msg, con);

                                    output = JsonConvert.SerializeObject(msg);
                                    sendByte = Encoding.ASCII.GetBytes(output);
                                    SendData(udpServer, new IPEndPoint(int.Parse(st.upward_Node.Addr), st.upward_Node.Port), sendByte);
                                    Console.WriteLine("ECHO an " + st.upward_Node.Port.ToString() + " gesendet!");
                                }
                            }
                        }
                        Console.WriteLine(st.informed.ToString());
                        break;
                    case Message.MsgCommand.ECHO:
                        Console.WriteLine("Echo received");
                        if (st.upward_Node == con)
                        {
                            gesamtSpeicher += st.Speicher;
                            Console.WriteLine("Speicher beträgt: " + gesamtSpeicher.ToString());
                        }
                        else
                        {
                            gesamtSpeicher += st.Speicher;
                            //msg = new Message(con, Message.MsgCommand.ECHO, gesamtSpeicher);
                            ChangeMsgCommand(ref msg, Message.MsgCommand.ECHO);
                            ChangeCon(ref msg, con);
                            AddSpeicher(ref msg, gesamtSpeicher);
                            output = JsonConvert.SerializeObject(msg);
                            sendByte = Encoding.ASCII.GetBytes(output);
                            SendData(udpServer, new IPEndPoint(int.Parse(st.upward_Node.Addr), st.upward_Node.Port), sendByte);
                            Console.WriteLine("ECHO an " + st.upward_Node.Port.ToString() + " gesendet!");
                        }
                        break;
                    default:
                        break;
                }
                //Console.WriteLine(Encoding.ASCII.GetString(data));
            }
            Console.WriteLine("Receive thread zuende");
        }

        //Funktion Daten empfangen und als Byte Array zurückgeben
        private static Byte[] ReceiveData(UdpClient udpServer, int port)
        {
            Byte[] data = null;
            //try
            //{
            //    Monitor.Enter(_object);
            //    var groupEP = new IPEndPoint(IPAddress.Any, port);
            //    data = udpServer.Receive(ref groupEP);
            //    Monitor.Exit(_object);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //}
            //return data;
            object o = new object();
            bool locked = false;

            try
            {
                Monitor.Enter(o, ref locked);

                var groupEP = new IPEndPoint(IPAddress.Any, port);
                data = udpServer.Receive(ref groupEP);
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
            return data;
        }

        //Byte Array schicken
        private static void SendData(UdpClient udpServer, IPEndPoint ipEndPoint, Byte[] sendByte)
        {
            udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
        }

        private static void SendDataToNeight(UdpClient udpServer, Byte[] sendByte, List<Verbindung> network)
        {
            //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
            foreach (Verbindung verb in network)
            {
                IPEndPoint ip = new IPEndPoint(IPAddress.Parse(verb.Addr), verb.Port);
                udpServer.Send(sendByte, sendByte.Length, ip);
            }
        }
        private static void IncNeightInformed(ref Message msg)
        {
            //lock (_object)
            //{
            //    msg.neightInformed++;
            //}
            //try
            //{
            //    Monitor.Enter(_object);
            //    msg.neightInformed++;
            //    Monitor.Exit(_object);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //}
            object o = new object();
            bool locked = false;
            try
            {
                Monitor.Enter(o, ref locked);
                msg.neightInformed++;
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }

        private static void ChangeMsgCommand(ref Message msg, Message.MsgCommand command)
        {
            //try
            //{
            //    Monitor.Enter(_object);
            //    msg.command = command;
            //    Monitor.Exit(_object);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //}
            //lock (_object)
            //{
            //    msg.command = command;
            //}
            object o = new object();
            bool locked = false;
            try
            {
                Monitor.Enter(o, ref locked);
                msg.command = command;
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }

        private static void ChangeCon(ref Message msg, Verbindung verb)
        {

            //try
            //{
            //    Monitor.Enter(_object);
            //    msg.verbindung = verb;
            //    Monitor.Exit(_object);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //}
            //lock (_object)
            //{
            //    msg.verbindung = verb;
            //}
            object o = new object();
            bool locked = false;
            try
            {
                Monitor.Enter(o, ref locked);
                msg.verbindung = verb;
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }

        private static void AddSpeicher(ref Message msg, int speicher)
        {
            //lock (_object)
            //{
            //    msg.sum = speicher;
            //}
            //try
            //{
            //    bool entered = Monitor.TryEnter()
            //    Monitor.Enter(_object);
            //    msg.sum = speicher;
            //    Monitor.Exit(_object);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //}
            object o = new object();
            bool locked = false;
            try
            {
                Monitor.Enter(o, ref locked);
                msg.sum = speicher;
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }
    }
}

