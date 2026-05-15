using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MicroSignalingServer
{
    class Program
    {
        // Key: Lobby Code, Value: Host's Public IP and Port
        static ConcurrentDictionary<string, IPEndPoint> lobbies = new ConcurrentDictionary<string, IPEndPoint>();

        static async Task Main(string[] args)
        {
            int port = 5555;
            using UdpClient server = new UdpClient(port, AddressFamily.InterNetwork);
            
            Console.WriteLine($"[SERVER START] UDP Signaling Server running on port {port}...");

            while (true)
            {
                try
                {
                    // Wait for any incoming UDP packet
                    UdpReceiveResult result = await server.ReceiveAsync();
                    IPEndPoint clientEndPoint = result.RemoteEndPoint;
                    
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    string[] parts = message.Split('|');

                    if (parts.Length != 2) continue; // Ignore malformed packets

                    string command = parts[0]; // "HOST", "JOIN" or "CLOSE"
                    string code = parts[1].ToUpper(); // e.g., "XYZ123"

                    Console.WriteLine($">{command} {code}");

                    if (command == "HOST")
                    {
                        // 2 Register the host's public IP and Port
                        lobbies[code] = clientEndPoint;
                        Console.WriteLine($"[HOST] Code: {code} created by {clientEndPoint}");
                    }
                    else if (command == "JOIN")
                    {
                        if (lobbies.TryRemove(code, out IPEndPoint hostEndPoint))
                        {
                            Console.WriteLine($"[MATCH] Code: {code}! Connecting {hostEndPoint} and {clientEndPoint}");

                            // Send the Host's IP/Port to the Joiner
                            string toJoiner = $"MATCH|{hostEndPoint.Address}|{hostEndPoint.Port}";
                            byte[] joinerBytes = Encoding.UTF8.GetBytes(toJoiner);
                            await server.SendAsync(joinerBytes, joinerBytes.Length, clientEndPoint);

                            // Send the Joiner's IP/Port to the Host
                            string toHost = $"MATCH|{clientEndPoint.Address}|{clientEndPoint.Port}";
                            byte[] hostBytes = Encoding.UTF8.GetBytes(toHost);
                            await server.SendAsync(hostBytes, hostBytes.Length, hostEndPoint);
                        }
                        else
                        {
                            Console.WriteLine($"[FAIL] Join attempted for invalid code: {code}");
                        }
                    }
                    else if ( command == "CANCEL")
                    {
                        if (lobbies.TryGetValue(code, out IPEndPoint hostEndPoint) && hostEndPoint.Equals(clientEndPoint))
                        {
                            lobbies.Remove(code, out IPEndPoint value);
                            Console.WriteLine($"[CANCEL] lobby: {code}");
                        }
                        else
                        {
                            Console.WriteLine($"[FAIL] Cancel attempted for code: {code}; {clientEndPoint} different from expected {hostEndPoint}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {ex.Message}");
                }
            }
        }
    }
}