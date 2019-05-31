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
            new int[] { 1,2,3,6,10 },
            new int[] { 1,2 },
            new int[] { 2,6,8,9,11,12 },
            new int[] { 9,11 },
            new int[] { 2,4,9,10 },
            new int[] { 1,10 },
            new int[] { 4,9,12 },
            new int[] { 4,5,6,8,11 },
            new int[] { 2,7,12 },
            new int[] { 4,5 },
            new int[] { 7,8,10 }
        };

        public static bool exit = true;

        static void Main(string[] args)
        {
            List<Verbindung> network = new List<Verbindung>();
            Verbindung node = new Verbindung();
            if (args.Length == 0)
            {
                UdpClient udpServer = new UdpClient(5000);  //Port ist 5000
                node = DefineNode(getLocalIP(), 5000, 0);

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
                node = DefineNode(getLocalIP(), int.Parse(args[0]), int.Parse(args[1]));


                string output = JsonConvert.SerializeObject(node);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);

                IPEndPoint ipEndPoint = new IPEndPoint(getLocalIP(), 5000);
                //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                SendData(udpServer, ipEndPoint, sendByte);
                Console.WriteLine(node.nodeNr.ToString() + " Knoten ist online!");


                //receive Modus
                while (network.Count != nodes[int.Parse(args[1]) - 1].Length)
                {
                    var groupEP = new IPEndPoint(IPAddress.Any, int.Parse(args[0]));
                    Byte[] data = udpServer.Receive(ref groupEP);
                    string returnData = Encoding.ASCII.GetString(data);
                    Console.WriteLine(returnData);
                    Verbindung refNode = JsonConvert.DeserializeObject<Verbindung>(returnData);
                    network.Add(refNode);
                }

                Console.WriteLine(node.nodeNr.ToString() + " hat alle Kanten empfange und ist bereit");                         // <= ab hier sind die knoten bereit für den verteilten Algorithmus
                ipEndPoint = new IPEndPoint(getLocalIP(), 5003);

                Thread ReceiveThread = new Thread(() => ReceiveDataThread(udpServer, int.Parse(args[0])));
                ReceiveThread.Start();
                Thread SendThread = new Thread(() => SendDataThread(udpServer, ipEndPoint));
                SendThread.Start();



                //Random zufall = new Random();
                //Thread.Sleep(zufall.Next(1000, 20000));
                //Console.ReadKey();
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
            Verbindung verb = new Verbindung();
            verb.addr = addr.ToString();
            verb.port = port;
            verb.nodeNr = num;
            return verb;
        }

        private static void SendNeighbor(UdpClient udpServer, Verbindung verb, List<Verbindung> network)
        {
            for (int i = nodes[verb.nodeNr - 1].Length; i > 0; i--)
            {
                Verbindung temp = network.Find(r => r.nodeNr == nodes[verb.nodeNr - 1][i - 1]);
                //IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(verb.addr), verb.port);
                string output = JsonConvert.SerializeObject(temp);
                Byte[] sendByte = Encoding.ASCII.GetBytes(output);
                //udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                //senden der NachbarnIP an Client
                SendData(udpServer, new IPEndPoint(IPAddress.Parse(verb.addr), verb.port), sendByte);
            }
        }

        private static void ExitAll(List<Verbindung> network)
        {

        }

        private static void ReceiveDataThread(UdpClient udpServer, int port)
        {
            while (exit)
            {
                var groupEP = new IPEndPoint(IPAddress.Any, port);
                Byte[] data = udpServer.Receive(ref groupEP);
                Console.WriteLine(Encoding.ASCII.GetString(data));
                if (Encoding.ASCII.GetString(data) == "!Exit")
                {
                    exit = false;
                }
            }
            Console.WriteLine("Receive thread zuende");
        }

        private static void SendDataThread(UdpClient udpServer, IPEndPoint ipEndPoint)
        {
            while (exit)
            {
                var input = Console.ReadLine();
                Byte[] sendByte = Encoding.ASCII.GetBytes(input);
                udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
                Console.WriteLine("gesendet!");
            }
            Console.WriteLine("Receive thread zuende");
        }

        private static Byte[] ReceiveData(UdpClient udpServer, int port)
        {
            var groupEP = new IPEndPoint(IPAddress.Any, port);
            Byte[] data = udpServer.Receive(ref groupEP);
            return data;
        }

        private static void SendData(UdpClient udpServer, IPEndPoint ipEndPoint, Byte[] sendByte)
        {
            udpServer.Send(sendByte, sendByte.Length, ipEndPoint);
        }
    }
}

