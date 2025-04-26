using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Parallel_Lab5;

class Program : IDisposable
{
    private static Socket? _socket;
    static void Main(string[] args)
    {
        string ip = "127.0.0.1";
        int port = 8080;
        
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        EndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        
        _socket.Bind(endPoint);
        _socket.Listen(100);
        
        Console.WriteLine($"\nServer running on {ip}:{port}");

        _socket.BeginAccept(AcceptedCallback, null);
        
        while(true)
            Console.ReadLine(); // So server doesn't stop
    }

    private static void AcceptedCallback(IAsyncResult asyncResult)
    {
        try
        {
            // Accept the connection and start accepting again
            Socket clientSocket = _socket!.EndAccept(asyncResult);

            Console.WriteLine("\nClient connected.");

            _socket.BeginAccept(AcceptedCallback, null);

            // Start handling client
            //ThreadPool.QueueUserWorkItem(_ => HandleClient(clientSocket));
            ReceiveRequest(clientSocket);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in AcceptCallback: " + ex.Message);
        }
    }

    private static void HandleClient(Socket clientSocket)
    {
        ReceiveRequest(clientSocket);
    }

    private static void ReceiveRequest(Socket clientSocket)
    {
        byte[] buffer = new byte[1024];

        clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, 
            Tuple.Create(clientSocket, buffer, new StringBuilder()));
    }

    private static void ReceiveCallback(IAsyncResult asyncResult)
    {
        // Get context
        var state = asyncResult.AsyncState as Tuple<Socket, byte[], StringBuilder>;
        Socket clientSocket = state.Item1;
        byte[] buffer = state.Item2;
        StringBuilder requestBuilder = state.Item3;
        
        // End receive
        int bytesReceived = clientSocket.EndReceive(asyncResult);
        requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesReceived));
        
        // Check if the message is received completely
        if (bytesReceived != buffer.Length)
        {
            Console.WriteLine(requestBuilder.ToString());
            SendResponse(clientSocket, requestBuilder.ToString());
            return;
        }
        
        // If not, receive again
        buffer = new byte[1024];
        clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, 
            Tuple.Create(clientSocket, buffer, requestBuilder));
    }
    
    private static void SendResponse(Socket clientSocket, string request)
    {
        string responseString, responseBody;
        
        if (request == "")
        {
            Console.WriteLine("Failed to read request from a client.");
            
            responseBody = JsonSerializer.Serialize(new
            {
                error = "Bad request",
                message = "Server could not read the request properly."
            });
            
            responseString = $"HTTP/1.1 400 Bad Request\r\n" +
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
                    responseString = FormHtmlResponse("200 OK", responseBody);
                    break;
                case "/page2":
                case "/page2.html":
                    responseBody = GetPage("page2.html");
                    responseString = FormHtmlResponse("200 OK", responseBody);
                    break;
                default:
                    responseBody = GetPage("404.html");
                    responseString = FormHtmlResponse("404 Not Found", responseBody);
                    break;
            }
        }
        
        byte[] response = Encoding.UTF8.GetBytes(responseString);
        clientSocket.BeginSend(response, 0, response.Length, SocketFlags.None, SendCallback, clientSocket);
    }

    private static void SendCallback(IAsyncResult asyncResult)
    {
        Socket clientSocket = asyncResult.AsyncState as Socket;

        try
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in SendCallback: " + ex.Message);
        }
    }
    
    private static string GetPage(string fileName)
    {
        return File.ReadAllText($"../../../Html/{fileName}");
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