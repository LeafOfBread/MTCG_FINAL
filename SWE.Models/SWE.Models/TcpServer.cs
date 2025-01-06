using System;
//using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Http;
using Newtonsoft.Json;
using Npgsql;
using static System.Formats.Asn1.AsnWriter;
using System.Net.WebSockets;
using System.Web;


namespace SWE.Models
{
    //definiert route mit method, path und handler
    public class Route
    {
        public string Method { get; }
        public string Path { get; }
        public Func<string, StreamWriter, Task> Handler { get; }

        public Route(string method, string path, Func<string, StreamWriter, Task> handler)
        {
            Method = method;
            Path = path;
            Handler = handler;
        }
    }

    //definiert router mit routes und methoden zum registrieren und handlen von routes
    public class Router
    {
        private List<Route> routes = new List<Route>();

        public void RegisterRoute(string method, string path, Func<string, StreamWriter, Task> handler)
        {
            routes.Add(new Route(method, path, handler));
        }

        private async Task SendResponse(StreamWriter writer, int statusCode, string responseBody)
        {
            writer.WriteLine($"HTTP/1.1 {statusCode} {GetStatusCodeDescription(statusCode)}");
            writer.WriteLine("Content-Type: application/json");
            writer.WriteLine($"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}");
            writer.WriteLine();  // Blank line separating headers and body
            writer.WriteLine(responseBody);
        }

        private string GetStatusCodeDescription(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                201 => "Created",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "Unknown"
            };
        }
    }

    //tcp server mit users, sessions und router
    public class TcpServer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Router _router;
        private readonly UserService _userService;
        private readonly Package _packageService;

        private List<Package> packs;


        public TcpServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _router = new Router();
            // Retrieve UserService and Package from the service provider and pass them to InitRoutes
            var userService = _serviceProvider.GetRequiredService<UserService>();
            var packageService = _serviceProvider.GetRequiredService<Package>();
            packs = new List<Package>();
        }

        public void Start(string host, int port)
        {
            IPAddress ip = host == "localhost" ? IPAddress.Any : IPAddress.Parse(host);
            TcpListener server = new TcpListener(ip, port);
            server.Start();

            Console.WriteLine($"Server started on {host}:{port}");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
        }
        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    string requestLine = await reader.ReadLineAsync();
                    string[] requestParts = requestLine.Split(' ');

                    if (requestParts.Length < 3) return;

                    int contentLength = 0;
                    string authHeader = null;
                    string line;
                    bool headersEnd = false; // Flag to detect end of headers

                    while (!headersEnd && !string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        if (line.StartsWith("Content-Length:"))
                        {
                            contentLength = int.Parse(line.Split(':')[1].Trim());
                        }
                        else if (line.StartsWith("Authorization:"))
                        {
                            authHeader = line.Split(':')[1].Trim();
                        }
                        else if (line == "") // Detect end of headers when an empty line is encountered
                        {
                            headersEnd = true;
                        }
                    }

                    // Log content length for debugging
                    Console.WriteLine($"Content-Length: {contentLength}");
                    Console.WriteLine($"Authorization: {authHeader}");

                    Console.WriteLine($"Request Method: {requestParts[0]}");
                    Console.WriteLine($"Request Path: {requestParts[1]}");

                    string method = requestParts[0];
                    string path = requestParts[1].Trim();

                    // Handle body
                    char[] buffer = new char[contentLength];
                    if (contentLength > 0)
                    {
                        await reader.ReadAsync(buffer, 0, contentLength);
                    }
                    string body = new string(buffer);

                    Console.WriteLine($"Request Body: {body}");

                    // Check if the path is correctly matched to "/users" or "/sessions"
                    if (path == "/sessions" && method == "POST")
                    {
                        await HandleLogin(body, new Dictionary<string, string>(), writer);
                    }
                    else if (path == "/users" && method == "POST")
                    {
                        await HandleRegisterUser(body, new Dictionary<string, string>(), writer);
                    }
                    else if (path == "/packages" && method == "POST")
                    {
                        List<Dictionary<string, object>> parsedBody = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(body);
                        await HandlePackages(parsedBody, authHeader, stream);
                    }


                    else if (path.StartsWith("/transactions/packages") && method == "POST")
                    {
                        await HandleAcquirePackages(authHeader, stream);
                    }

                    else
                    {
                        await SendResponse(writer, 404, "Not Found");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }

        public async Task<int> HandleAcquirePackages(string Auth, NetworkStream stream)
        {
            Console.WriteLine("Debug 4\n");
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var packageService = scope.ServiceProvider.GetRequiredService<Package>();
            var cardService = scope.ServiceProvider.GetRequiredService<Card>();
            Console.WriteLine("Debug 3\n");

            // Step 1: Authenticate the user
            var token = Auth.Replace("Bearer ", "").Trim();
            var user = await userService.GetUserByTokenAsync(token);  // Ensure this is awaited
            Console.WriteLine("Debug 1\n");
            if (user == null)
            {
                await SendResponsePackage(stream, 401, "Unauthorized");
                return -1;
            }

            // Step 2: Check if the user has enough coins
            if (user.coins < 5)
            {
                await SendResponsePackage(stream, 400, "Insufficient coins");
                return -1;
            }
            Console.WriteLine("Debug 2\n");

            // Step 3: Get the package ID (you can modify this logic as needed)
            // You might choose to use a specific package or a random package.
            var packageId = 1; // Example, replace with logic to get the packageId

            // Step 4: Acquire the cards from the package
            var package = await packageService.GetPackageCardsAsync(packageId); // Pass the packageId here
            if (package == null || package.Count == 0)  // Use Count here after awaiting the task
            {
                await SendResponsePackage(stream, 400, "Package is empty");
                return -1;
            }

            // Step 5: Deduct coins and add the cards to the user's inventory
            await userService.DeductCoinsFromUserAsync(user, 5); // Deduct 5 coins for the purchase
            await cardService.AddCardsToUserInventoryAsync(user, package); // Add the cards from the package to the user's inventory

            // Step 6: Send success response
            await SendResponsePackage(stream, 201, "Package acquired successfully");

            return 0;
        }


        public async Task<int> HandlePackages(List<Dictionary<string, object>> receive, string Auth, NetworkStream stream)
        {
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");

            var packagesINT = new Package().createPackage(Auth, receive, connection);
            if (packagesINT.Item2 == 0)
            {
                // packs.Add(packagesINT.Item1);
                Console.WriteLine("Packages created by Admin");
                SendResponsePackage(stream, 201, "");
                return 0;
            }
            SendResponsePackage(stream, 404, "not Authorized");
            return -1;
        }




        private async Task HandleLogin(string body, Dictionary<string, string> headers, StreamWriter writer)
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var request = JsonConvert.DeserializeObject<UserLoginRequest>(body);

            if (request == null)
            {
                await SendResponse(writer, 400, "Request is NULL");
                return;
            }

            var result = await userService.AuthenticateUserAsync(request.username, request.password);

            if (result.IsSuccess)
            {
                await SendResponse(writer, 200, JsonConvert.SerializeObject(new { token = result.Token }));
            }
            else
            {
                await SendResponse(writer, 401, "Invalid username or password");
            }
        }

        private async Task HandleRegisterUser(string body, Dictionary<string, string> headers, StreamWriter writer)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<UserService>();
                var user = JsonConvert.DeserializeObject<User>(body);

                if (user == null)
                {
                    await SendResponse(writer, 400, "Invalid request");
                    return;
                }

                var result = await userService.RegisterUserAsync(user);

                if (result.IsSuccess)
                {
                    await SendResponse(writer, 201, "User created successfully");
                }
                else
                {
                    await SendResponse(writer, 409, "User already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleRegisterUser: {ex.Message}");
                await SendResponse(writer, 500, "Internal Server Error");
            }
        }

        private async Task SendResponsePackage(NetworkStream stream, int statusCode, string message)
        {
            string statusDescription = statusCode switch
            {
                200 => "OK",
                201 => "Created",
                401 => "Unauthorized",
                403 => "Forbidden",
                500 => "Internal Server Error",
                _ => "Unknown"
            };

            // HTTP/1.1 response
            string response = $"HTTP/1.1 {statusCode} {statusDescription}\r\n" +
                              "Content-Type: application/json\r\n" +
                              "Connection: close\r\n" +  // Close the connection after the response
                              "\r\n" +  // End of headers
                              message;

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private async Task SendResponse(StreamWriter writer, int statusCode, string message)
        {
            await writer.WriteLineAsync($"HTTP/1.1 {statusCode} OK");
            await writer.WriteLineAsync($"Content-Type: application/json");
            await writer.WriteLineAsync($"Content-Length: {message.Length}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();  // Ensure flush is done

        }
    }

    public class UserLoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }
}