using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using WebSocketSharp;
using CefSharp.Structs;
using CefSharp;
using System.Windows.Forms.VisualStyles;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Data.SqlTypes;
using System.IO.Compression;

namespace ServerDeploymentAssistant.src.Network
{
    /// <summary>
    /// That class used for UDP discovery. No used in Lines Browser. Not tested.
    /// </summary>
    public static class NetworkManager
    {
        static UdpClient receivingClient;
        static UdpClient sendingClient;
        static Thread udpReciving;
        const int udpDiscoveryPort = 54545;
        const int udpSendDiscoveryPort = 54546;
        const string broadcastAddress = "255.255.255.255";

        delegate void AddMessage(string message);

        public static void StartUdpDiscoveryServer()
        {
            receivingClient = new UdpClient(udpDiscoveryPort);
            ThreadStart start = new ThreadStart(UdpDiscoveryReciver);
            udpReciving = new Thread(start);
            udpReciving.IsBackground = true;
            udpReciving.Start();


            sendingClient = new UdpClient(broadcastAddress, 1337);
            sendingClient.EnableBroadcast = true;

        }
        private static void UdpDiscoveryReciver()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, udpDiscoveryPort);
            AddMessage messageDelegate = UdpMessageRecived;
            while (true)
            {
                byte[] data = receivingClient.Receive(ref endPoint);
                string message = Encoding.ASCII.GetString(data);
                UdpMessageRecived(message);
            }
        }
        private static void UdpMessageRecived(string packetJSON)
        {
            try
            {
                var udpPacket = JsonConvert.DeserializeObject<DiscoveryPacket>(packetJSON);
                switch (udpPacket.PType)
                {
                    case DiscoveryPacketType.AddressRequest:
                        Logger.CreateLog("[ACK] Client requested address for connect.");
                        var packet = new DiscoveryPacket
                        {
                            PType = DiscoveryPacketType.ACK,
                            ServerAddress = Utils.GetLocalIPAddress()
                        };
                        var rawPacket = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet));
                        sendingClient.Send(rawPacket, rawPacket.Length);

                        break;

                    case DiscoveryPacketType.ACK:
                        break;
                    default:
                        break;
                }
            }
            catch (Exception) {}
        }
    }
}

