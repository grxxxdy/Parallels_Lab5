using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Parallel_Lab5;

class Program : IDisposable
{
    private static Socket? _socket;
    private static readonly ConcurrentDictionary<string, string> pages = new();
    static void Main(string[] args)
    {
        string ip = "127.0.0.1";
        int port = 8080;
        
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        EndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        
        _socket.Bind(endPoint);
        _socket.Listen(100);
        
        Console.WriteLine($"\nServer running on {ip}:{port}");

        while (true)
        {
            var clientSocket = _socket.Accept();
            
            Console.WriteLine("\nClient connected.");
            
            ThreadPool.QueueUserWorkItem(_ => HandleClient(clientSocket));
        }
    }

    private static void HandleClient(Socket clientSocket)
    {
        string request = ReceiveRequest(clientSocket);
        string response, responseBody;

        if (request == "")
        {
            Console.WriteLine("Failed to read request from a client.");
            
            responseBody = JsonSerializer.Serialize(new
            {
                error = "Bad request",
                message = "Server could not read the request properly."
            });
            
            response = $"HTTP/1.1 400 Bad Request\r\n" +
                       $"Content-Type: application/json\r\n" +
                       $"Content-Length: {responseBody.Length}\r\n\r\n" +
                       $"{responseBody}";
        }
        else
        {
            string[] requestLines = request.Split("\r\n");
            string path = requestLines[0].Split(" ")[1];        // [0] - Get;   [1] - Page;   [2] - HTTP/1.1

            switch (path)
            {
                case "/":
                case "/index.html":
                    responseBody = GetPage("index.html");
                    response = FormHtmlResponse("200 OK", responseBody);
                    break;
                case "/page2":
                case "/page2.html":
                    responseBody = GetPage("page2.html");
                    response = FormHtmlResponse("200 OK", responseBody);
                    break;
                default:
                    responseBody = GetPage("404.html");
                    response = FormHtmlResponse("404 Not Found", responseBody);
                    break;
            }
        }
        
        SendResponse(clientSocket, Encoding.UTF8.GetBytes(response));
    }

    private static string ReceiveRequest(Socket clientSocket)
    {
        byte[] buffer = new byte[1024];
        string request = "";
        
        try
        {
            using (var stream = new MemoryStream())
            {
                while (true)
                {
                    int bytesReceived = clientSocket.Receive(buffer, buffer.Length, SocketFlags.None);
                    
                    stream.Write(buffer, 0, bytesReceived);
        
                    if (bytesReceived != buffer.Length) break;
                }
        
                request = Encoding.UTF8.GetString(stream.ToArray());
                Console.WriteLine(request);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine(ex.Message);
        }
        
        return request;
    }
    
    private static void SendResponse(Socket clientSocket, byte[] response)
    {
        clientSocket.Send(response);
        
        clientSocket.Shutdown(SocketShutdown.Both);
        clientSocket.Close();
    }
    
    private static string GetPage(string fileName)
    {
        return pages.GetOrAdd(fileName, fn => File.ReadAllText($"../../../Html/{fn}"));
    }

    private static string FormHtmlResponse(string messageCode, string responseBody)
    {
        return $"HTTP/1.1 {messageCode}\r\n" +
               $"Content-Type: text/html; charset=UTF-8\r\n" +
               $"Content-Length: {responseBody.Length}\r\n\r\n" +
               $"{responseBody}";
    }

    public void Dispose()
    {
        _socket?.Shutdown(SocketShutdown.Both);
        _socket?.Close();
        _socket?.Dispose();
    }
}