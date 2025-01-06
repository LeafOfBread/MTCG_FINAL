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
                        Console.WriteLine("authHeader: " + authHeader);
                        await HandleAcquirePackages(authHeader, stream);
                    }
                    else if (path == "/cards" && method == "GET")
                    {
                        Console.WriteLine("authHeader: " + authHeader);
                        await HandleCardListing(authHeader, writer);
                    }
                    else if(path == "/deck" && method == "GET")
                    {
                        await HandleListPlayingDeck(authHeader, writer);
                    }
                    else if (path == "/deck" && method == "PUT")
                    {
                        await HandleDeckUpdate(authHeader, body, writer);
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
        public async Task HandleDeckUpdate(string authToken, string body, StreamWriter writer)
        {
            // Remove the "Bearer " prefix if present
            string inputToken = authToken.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Verify user based on authToken
            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Parse the body into a list of card IDs (Guid format)
            List<Guid> cardIds = JsonConvert.DeserializeObject<List<Guid>>(body);

            // Ensure exactly 4 cards are provided
            if (cardIds.Count != 4)
            {
                await SendResponse(writer, 400, "Invalid number of cards. You must provide exactly 4 cards.");
                return;
            }

            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();

            var validCardIds = new List<Guid>();

            // Ensure that all provided cards belong to the user
            foreach (var cardGuid in cardIds)
            {
                var checkCardQuery = "SELECT 1 FROM cards WHERE id = @cardId AND user_id = @userId";
                using (var command = new NpgsqlCommand(checkCardQuery, connection))
                {
                    command.Parameters.AddWithValue("@cardId", cardGuid);
                    command.Parameters.AddWithValue("@userId", userId.Value);
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        validCardIds.Add(cardGuid);
                    }
                }
            }

            // Log valid card IDs for debugging
            Console.WriteLine("Valid Card IDs: " + string.Join(", ", validCardIds));

            // Ensure that exactly 4 valid cards were found
            if (validCardIds.Count != 4)
            {
                await SendResponse(writer, 400, "One or more cards do not belong to the user.");
                return;
            }

            // Clear the user's current deck
            var clearDeckQuery = "DELETE FROM user_deck WHERE user_id = @userId";
            using (var command = new NpgsqlCommand(clearDeckQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId.Value);
                await command.ExecuteNonQueryAsync();
            }

            // Insert the new cards into the user's deck
            foreach (var cardId in validCardIds)
            {
                var insertDeckQuery = "INSERT INTO user_deck (user_id, card_id) VALUES (@userId, @cardId)";
                using (var command = new NpgsqlCommand(insertDeckQuery, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId.Value);
                    command.Parameters.AddWithValue("@cardId", cardId);
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Respond with a success message
            await SendResponse(writer, 200, "Deck updated successfully");
        }




        public async Task HandleListPlayingDeck(string authToken, StreamWriter writer)
        {
            Console.WriteLine("authToken debug " + authToken);
            if (string.IsNullOrEmpty(authToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Remove the "Bearer " prefix if present
            string inputToken = authToken.Replace("Bearer ", "").Trim();

            // Verify user based on authToken
            var userService = _serviceProvider.GetRequiredService<UserService>();
            int userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Fetch the user's playing deck from the database
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();

            const string getUserDeckQuery = @"
    SELECT c.id, c.name, c.damage
    FROM cards c
    INNER JOIN user_deck ud ON c.id = ud.card_id
    WHERE ud.user_id = @userId";

            List<Card> userDeck = new List<Card>();

            using (var command = new NpgsqlCommand(getUserDeckQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userDeck.Add(new Card(connection)
                        {
                            id = reader.GetGuid(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            damage = reader.GetDouble(reader.GetOrdinal("damage"))
                        });
                    }
                }
            }

            // Check if the deck is empty
            if (userDeck.Count == 0)
            {
                // Return a 200 OK response with an empty list
                string response = JsonConvert.SerializeObject(new List<Card>());
                await SendResponse(writer, 200, response);
            }
            else
            {
                // Respond with the user's deck in JSON format
                string response = JsonConvert.SerializeObject(userDeck);
                await SendResponse(writer, 200, response);
            }
        }


        public async Task<int> HandleAcquirePackages(string authToken, NetworkStream stream)
        {
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();
            var userService = _serviceProvider.GetRequiredService<UserService>();

            // Verify user based on authToken
            int userId = await userService.GetUserIdByTokenAsync(inputToken);
            if (userId == null)
            {
                SendResponsePackage(stream, 401, "Unauthorized");
                return -1;
            }

            // Get the user's coin balance
            const string getUserCoinsQuery = @"
    SELECT coins
    FROM users
    WHERE id = @id";

            int userCoins = 0;

            using (var command = new NpgsqlCommand(getUserCoinsQuery, connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        userCoins = reader.GetInt32(reader.GetOrdinal("coins"));
                    }
                }
            }

            // Assume each package costs 5 coins
            const int packageCost = 5;

            if (userCoins < packageCost)
            {
                SendResponsePackage(stream, 400, "Not enough coins");
                return -1;
            }

            // Get a random package
            const string getPackageQuery = @"
SELECT p.package_id
FROM packages p
JOIN cards c ON c.package_id = p.package_id
WHERE c.user_id IS NULL
GROUP BY p.package_id
HAVING COUNT(c.id) = 5
ORDER BY RANDOM()
LIMIT 1";


            Guid? packageId = null;

            using (var command = new NpgsqlCommand(getPackageQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    packageId = reader.GetGuid(0);
                }
            }

            if (packageId == null)
            {
                SendResponsePackage(stream, 404, "No packages available");
                return -1;
            }

            // Deduct coins from the user
            const string updateUserCoinsQuery = @"
    UPDATE users
    SET coins = coins - @packageCost
    WHERE id = @id";

            using (var updateCommand = new NpgsqlCommand(updateUserCoinsQuery, connection))
            {
                updateCommand.Parameters.AddWithValue("@packageCost", packageCost);
                updateCommand.Parameters.AddWithValue("@id", userId);
                await updateCommand.ExecuteNonQueryAsync();
            }

            // Assign cards to the user
            const string updateCardsQuery = @"
    UPDATE cards
    SET user_id = @id
    WHERE package_id = @packageId";

            using (var updateCommand = new NpgsqlCommand(updateCardsQuery, connection))
            {
                updateCommand.Parameters.AddWithValue("@id", userId);
                updateCommand.Parameters.AddWithValue("@packageId", packageId);
                await updateCommand.ExecuteNonQueryAsync();
            }

            // Retrieve the assigned cards
            const string getCardsQuery = @"
    SELECT id, name, damage
    FROM cards
    WHERE package_id = @packageId";

            List<Card> assignedCards = new List<Card>();

            using (var getCardsCommand = new NpgsqlCommand(getCardsQuery, connection))
            {
                getCardsCommand.Parameters.AddWithValue("@packageId", packageId);
                using (var reader = await getCardsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        assignedCards.Add(new Card(connection)
                        {
                            id = reader.GetGuid(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            damage = reader.GetDouble(reader.GetOrdinal("damage"))
                        });
                    }
                }
            }

            // Respond to the user
            string response = string.Join(", ", assignedCards.Select(card => $"{card.name} (Damage: {card.damage})"));
            SendResponsePackage(stream, 200, $"You received: {response}");

            return 0;
        }

        public async Task HandleCardListing(string authToken, StreamWriter writer)
        {
            Console.WriteLine("authToken debug " + authToken);
            if (string.IsNullOrEmpty(authToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }
            // Remove the "Bearer " prefix if present
            string inputToken = authToken.Replace("Bearer ", "").Trim();

            // Verify user based on authToken
            var userService = _serviceProvider.GetRequiredService<UserService>();
            int userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Fetch the user's cards from the database
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();

            const string getUserCardsQuery = @"
    SELECT id, name, damage
    FROM cards
    WHERE user_id = @userId";

            List<Card> userCards = new List<Card>();

            using (var command = new NpgsqlCommand(getUserCardsQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userCards.Add(new Card(connection)
                        {
                            id = reader.GetGuid(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            damage = reader.GetDouble(reader.GetOrdinal("damage"))
                        });
                    }
                }
            }

            // Check if the list of cards is empty
            if (userCards.Count == 0)
            {
                // Return a 200 OK response with an empty list
                string response = JsonConvert.SerializeObject(new List<Card>());
                await SendResponse(writer, 200, response);
            }
            else
            {
                // Respond with the user's cards in JSON format
                string response = JsonConvert.SerializeObject(userCards);
                await SendResponse(writer, 200, response);
            }
        }


        public async Task<int> HandlePackages(List<Dictionary<string, object>> receive, string Auth, NetworkStream stream)
        {
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            var cardService = _serviceProvider.GetRequiredService<Card>();
            var userService = _serviceProvider.GetRequiredService<UserService>();

            var packagesINT = new Package(cardService, userService).createPackage(Auth, receive, connection);
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