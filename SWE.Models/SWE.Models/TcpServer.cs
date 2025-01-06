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


        public async Task HandleRequest(string method, string path, string body, Dictionary<string, string> headers, StreamWriter writer)
        {
            // Find a matching route based on the HTTP method and path
            var route = routes.FirstOrDefault(r => r.Method.Equals(method, StringComparison.OrdinalIgnoreCase) && r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (route == null)
            {
                // If no route matches, send a 404 Not Found response
                await SendResponse(writer, 404, "Not Found");
                return;
            }

            try
            {
                // Call the handler for the route with the provided body and writer
                await route.Handler(body, writer);
            }
            catch (Exception ex)
            {
                // If there is an error processing the request, send a 500 Internal Server Error response
                Console.WriteLine($"Error handling request: {ex.Message}");
                await SendResponse(writer, 500, "Internal Server Error");
            }
        }

    }
    //tcp server mit users, sessions und router
    public class TcpServer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Router _router;

        private List<Package> packs;


        public TcpServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _router = new Router();
            // Retrieve UserService and Package from the service provider and pass them to InitRoutes
            var userService = _serviceProvider.GetRequiredService<UserService>();
            var packageService = _serviceProvider.GetRequiredService<Package>();
            packs = new List<Package>();
            InitRoutes(userService, packageService);
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

        private void InitRoutes(UserService _userService, Package _packageService)
        {
            _router.RegisterRoute("POST", "/sessions", async (body, writer) =>
            {
                var headers = new Dictionary<string, string>();
                await HandleLogin(body, headers, writer);
            });

            _router.RegisterRoute("POST", "/users", async (body, writer) =>
            {
                var headers = new Dictionary<string, string>();
                await HandleRegisterUser(body, headers, writer);
            });

            _router.RegisterRoute("POST", "/packages", async (body, writer) =>
            {
                // Parse the headers from the request (you need to implement this step)
                var headers = ParseHeadersFromRequest(writer);

                // Now check for the Authorization header
                string authorizationHeader = headers.ContainsKey("Authorization") ? headers["Authorization"] : string.Empty;

                Console.WriteLine($"Received Auth Header: {authorizationHeader}");

                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    Console.WriteLine("Authorization header is missing or invalid.");
                    SendResponsePackage((NetworkStream)writer.BaseStream, 401, "Unauthorized");
                    return;
                }


                // Deserialize the body into the correct type (List<Dictionary<string, object>>)
                var packagesData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(body);

                // Pass the deserialized 'packagesData' and 'authorizationHeader' to HandlePackages
                var result = await HandlePackages(packagesData, authorizationHeader, (NetworkStream)writer.BaseStream);
            });
        }

        private Dictionary<string, string> ParseHeadersFromRequest(StreamWriter writer)
        {
            var headers = new Dictionary<string, string>();

            // Assuming you're working with the incoming request stream (via writer's BaseStream)
            if (writer.BaseStream is NetworkStream networkStream)
            {
                using (StreamReader reader = new StreamReader(networkStream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null && line != "")
                    {
                        int separatorIndex = line.IndexOf(":");
                        if (separatorIndex > 0)
                        {
                            string key = line.Substring(0, separatorIndex).Trim();
                            string value = line.Substring(separatorIndex + 1).Trim();
                            headers[key] = value;
                        }
                    }
                }
            }

            return headers;
        }



        private NetworkStream GetNetworkStreamFromWriter(StreamWriter writer)
        {
            // This assumes the StreamWriter is wrapped around a NetworkStream.
            if (writer.BaseStream is NetworkStream networkStream)
            {
                return networkStream;
            }

            throw new InvalidOperationException("The StreamWriter is not associated with a NetworkStream.");
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
                    if (string.IsNullOrEmpty(requestLine)) return;

                    string[] requestParts = requestLine.Split(' ');
                    if (requestParts.Length < 3) return;

                    string method = requestParts[0];
                    string path = requestParts[1];
                    Dictionary<string, string> headers = await ParseHeaders(reader);
                    string body = await ReadRequestBody(reader, headers);

                    await _router.HandleRequest(method, path, body, headers, writer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, string>> ParseHeaders(StreamReader reader)
        {
            var headers = new Dictionary<string, string>();
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                int separatorIndex = line.IndexOf(":", StringComparison.Ordinal);
                if (separatorIndex > 0)
                {
                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();
                    headers[key] = value;
                }
            }
            return headers;
        }

        private async Task<string> ReadRequestBody(StreamReader reader, Dictionary<string, string> headers)
        {
            if (headers.TryGetValue("Content-Length", out string contentLengthValue) &&
                int.TryParse(contentLengthValue, out int contentLength))
            {
                char[] bodyChars = new char[contentLength];
                await reader.ReadAsync(bodyChars, 0, contentLength);
                return new string(bodyChars);
            }
            return string.Empty;
        }

        public async Task<int> HandlePackages(List<Dictionary<string, object>> receive, string Auth, NetworkStream stream)
        {
            var packagesINT = new Package().createPackage(Auth, receive);

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
            // Write headers, status code, and message using StreamWriter
            await writer.WriteLineAsync($"HTTP/1.1 {statusCode} OK");
            await writer.WriteLineAsync($"Content-Type: application/json");
            await writer.WriteLineAsync($"Content-Length: {message.Length}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }


        private string GetStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                201 => "Created",
                401 => "Unauthorized",
                403 => "Forbidden",
                500 => "Internal Server Error",
                _ => "OK"
            };
        }

    }

    public class UserLoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }
}