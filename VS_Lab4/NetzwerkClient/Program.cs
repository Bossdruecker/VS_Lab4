using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        //private static ReaderWriterLockSlim methodLock = new ReaderWriterLockSlim();
        static object lockA = new object();
        static object lockB = new object();
        static readonly int exitCode = 0;
        static int gesamtSpeicher = 0;

        static void Main(string[] args)
        {
            List<Verbindung> network = new List<Verbindung>();
            Verbindung myCon = new Verbindung();
            if (args.Length == 0)
            {
                UdpClient udpServer = new UdpClient(5000);  //Port ist 5000
                myCon = DefineNode(getLocalIP(), 5000, 0);

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
                            Verbindung temp = network.Find(r => r.nodeNr == (i + 1));
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
                myCon = DefineNode(getLocalIP(), int.Parse(args[0]), int.Parse(args[1]));


                string output = JsonConvert.SerializeObject(myCon);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);

                IPEndPoint ipEndPoint = new IPEndPoint(getLocalIP(), 5000);
                //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                SendData(udpServer, ipEndPoint, sendByte);
                Console.WriteLine(myCon.nodeNr.ToString() + " Knoten ist online!");


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

                Console.WriteLine(myCon.nodeNr.ToString() + " hat alle Kanten empfange und ist bereit");                         // <= ab hier sind die knoten bereit für den verteilten Algorithmus

                Status st = new Status(network.Count, int.Parse(args[1]));
                Thread ReceiveThread = new Thread(() => ReceiveDataThread(udpServer, int.Parse(args[0]), myCon, network, ref st));
                Thread SendThread = new Thread(() => SendDataThread(udpServer, myCon, network, ref st));

                SendThread.Start();
                ReceiveThread.Start();
                



                //Random zufall = new Random();
                //Thread.Sleep(zufall.Next(1000, 20000));
                Console.ReadKey();
            }
        }

        private static IPAddress getLocalIP()
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
                addr = addr.ToString(),
                port = port,
                nodeNr = num
            };
            return verb;
        }

        private static void SendNeighbor(UdpClient udpServer, Verbindung verb, List<Verbindung> network)
        {
            for (int i = nodes[verb.nodeNr - 1].Length; i > 0; i--)
            {
                Verbindung temp = network.Find(r => r.nodeNr == nodes[verb.nodeNr - 1][i - 1]);
                string output = JsonConvert.SerializeObject(temp);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);
                SendData(udpServer, new IPEndPoint(IPAddress.Parse(verb.addr), verb.port), sendByte);
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
                Thread.Sleep(1000);
                var groupEP = new IPEndPoint(IPAddress.Any, port);
                Byte[] data = udpServer.Receive(ref groupEP);
                string returnData = Encoding.ASCII.GetString(data);
                Message msg = JsonConvert.DeserializeObject<Message>(returnData);
                switch (msg.command)
                {
                    case Message.MsgCommand.EXIT:
                        changeMsgCommand(ref msg, Message.MsgCommand.EXIT);
                        output = JsonConvert.SerializeObject(msg);
                        sendByte = Encoding.ASCII.GetBytes(output);
                        netw.ForEach(delegate (Verbindung v)
                        {
                            SendData(udpServer, new IPEndPoint(IPAddress.Parse(v.addr), v.port), sendByte);
                            Console.WriteLine("Info an " + v.port.ToString() + " gesendet!");
                        });
                        Console.ReadKey();
                        Environment.Exit(exitCode);
                        break;
                    case Message.MsgCommand.MSG:
                        Console.WriteLine("msg from: " + msg.verbindung.port + ": " + msg.payload);
                        break;
                    case Message.MsgCommand.INFO:
                        Console.WriteLine("Info von " + msg.verbindung.port.ToString() + " bekommen!");
                        incNeightInformed(ref msg);
                        Console.WriteLine("out of is informed " + st.informed.ToString() + "incNeightInformed: " + msg.neightInformed.ToString());
                        int node = msg.verbindung.port;
                        if (st.informed == false)
                        {
                            st.informed = true;
                            Console.WriteLine("in of is informed " + st.informed.ToString());
                            st.upward_Node = msg.verbindung;
                            node = msg.verbindung.port;

                            //msg = new Message(con, Message.MsgCommand.INFO, 0);
                            changeMsgCommand(ref msg, Message.MsgCommand.INFO);
                            changeCon(ref msg, con);
                            output = JsonConvert.SerializeObject(msg);
                            sendByte = Encoding.ASCII.GetBytes(output);
                            netw.ForEach(delegate (Verbindung v)
                            {
                                if (v.port != node)
                                {
                                    SendData(udpServer, new IPEndPoint(IPAddress.Parse(v.addr), v.port), sendByte);
                                    Console.WriteLine("Info an " + v.port.ToString() + " gesendet!");
                                }
                            });
                            Console.WriteLine("in of is informed finisch Info on Neighs");
                        }
                        else
                        {
                            Console.WriteLine("Bin informiert haben jedoch von " + msg.verbindung.port.ToString() + "Naschricht erhalten");
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
                                    changeMsgCommand(ref msg, Message.MsgCommand.ECHO);
                                    changeCon(ref msg, con);

                                    output = JsonConvert.SerializeObject(msg);
                                    sendByte = Encoding.ASCII.GetBytes(output);
                                    SendData(udpServer, new IPEndPoint(int.Parse(st.upward_Node.addr), st.upward_Node.port), sendByte);
                                    Console.WriteLine("ECHO an " + st.upward_Node.port.ToString() + " gesendet!");
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
                            changeMsgCommand(ref msg, Message.MsgCommand.ECHO);
                            changeCon(ref msg, con);
                            addSpeicher(ref msg, gesamtSpeicher);
                            output = JsonConvert.SerializeObject(msg);
                            sendByte = Encoding.ASCII.GetBytes(output);
                            SendData(udpServer, new IPEndPoint(int.Parse(st.upward_Node.addr), st.upward_Node.port), sendByte);
                            Console.WriteLine("ECHO an " + st.upward_Node.port.ToString() + " gesendet!");
                        }
                        break;
                    default:
                        break;
                }
                //Console.WriteLine(Encoding.ASCII.GetString(data));
            }
            Console.WriteLine("Receive thread zuende");
        }

        //Nachricht aus der Console senden <= Thread Methode
        private static void SendDataThread(UdpClient udpServer, Verbindung con, List<Verbindung> netw, ref Status st)
        {
            bool exit = true;
            while (exit)
            {
                Thread.Sleep(1000);
                String input = Console.ReadLine();
                string output;
                Message msg;
                Byte[] sendByte;

                switch (input)
                {
                    case "!Start":
                        updateStatus(ref st, true, con);
                        msg = new Message(con, Message.MsgCommand.INFO, 0);
                        output = JsonConvert.SerializeObject(msg);
                        sendByte = Encoding.ASCII.GetBytes(output);
                        netw.ForEach(delegate (Verbindung v)
                        {
                            SendData(udpServer, new IPEndPoint(IPAddress.Parse(v.addr), v.port), sendByte);
                            Console.WriteLine("Info an " + v.port.ToString() + " gesendet!");
                        });
                        break;
                    //optional wenn man nachricht an alle knoten schicken möchte
                    //case "!MSG":
                    //    input = Console.ReadLine();
                    //    msg = new Message(con, Message.MsgCommand.MSG, 0, input);
                    //    break;
                    //case "!Exit":
                    //    //myProcess.Close();
                    //    msg = new Message(con, Message.MsgCommand.EXIT, 0);
                    //    output = JsonConvert.SerializeObject(msg);
                    //    sendByte = Encoding.ASCII.GetBytes(output);
                    //    netw.ForEach(delegate (Verbindung v)
                    //    {
                    //        SendData(udpServer, new IPEndPoint(IPAddress.Parse(v.addr), v.port), sendByte);
                    //        Console.WriteLine("EXIT an " + v.port.ToString() + " gesendet!");
                    //    });
                    //    Console.ReadKey();
                    //    Environment.Exit(exitCode);
                    //    break;
                    default:
                        Console.WriteLine("Never Ever");
                        break;
                }
                //sendByte = Encoding.ASCII.GetBytes(input);
                //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                Console.WriteLine("gesendet! "  + st.informed.ToString() + " informed");
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
            //try
            //{
            //    Monitor.Enter(_object);
            //        udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
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

                udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }

        private static void updateStatus(ref Status st, bool inf, Verbindung upwardNode)
        {
            //st.informed = inf;
            //st.upward_Node = upwardNode;
            //methodLock.EnterWriteLock();
            //try
            //{
            //    st.informed = inf;
            //    st.upward_Node = upwardNode;
            //}
            //finally
            //{
            //    methodLock.ExitReadLock();
            //}
            //lock (_object)
            //{
            //    st.informed = inf;
            //    st.upward_Node = upwardNode;
            //}
            //try
            //{
            //    Monitor.Enter(_object);
            //    st.informed = inf;
            //    st.upward_Node = upwardNode;
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
                st.informed = inf;
                st.upward_Node = upwardNode;
            }
            finally
            {
                if (locked) Monitor.Exit(o);
            }
        }

        private static void incNeightInformed(ref Message msg)
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

        private static void changeMsgCommand(ref Message msg, Message.MsgCommand command)
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

        private static void changeCon(ref Message msg, Verbindung verb)
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

        private static void addSpeicher(ref Message msg, int speicher)
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

