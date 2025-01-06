﻿using System;
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
                    string requestBody = null; // Declare requestBody here to make it accessible throughout the method

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
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        await reader.ReadAsync(buffer, 0, contentLength);
                        requestBody = new string(buffer); // Assign the request body here
                    }

                    Console.WriteLine($"Request Body: {requestBody}");

                    // Check if the path is correctly matched to "/users" or "/sessions"
                    if (path == "/sessions" && method == "POST")
                    {
                        await HandleLogin(requestBody, new Dictionary<string, string>(), writer);
                    }
                    else if (path == "/users" && method == "POST")
                    {
                        await HandleRegisterUser(requestBody, new Dictionary<string, string>(), writer);
                    }
                    else if (path == "/packages" && method == "POST")
                    {
                        List<Dictionary<string, object>> parsedBody = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(requestBody);
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
                    else if (path == "/deck" && method == "GET")
                    {
                        await HandleListPlayingDeck(authHeader, writer);
                    }
                    else if (path == "/deck" && method == "PUT")
                    {
                        await HandleDeckUpdate(authHeader, requestBody, writer);
                    }
                    else if (path.StartsWith("/users/") && method == "GET")
                    {
                        string username = path.Substring(7); // Extract the username (e.g., /users/kienboec => "kienboec")
                        string authToken = authHeader.Replace("Bearer ", "").Trim();  // Remove "Bearer " part
                        string usernameFromToken = authToken.Replace("-mtcgToken", "").Trim();
                        bool justProfile = true;
                        if (username != usernameFromToken)
                        {
                            await SendResponse(writer, 403, "Forbidden: Token does not match the requested user");
                            return;
                        }
                        // Proceed to handle the user data request
                        await HandleGetUser(username, authToken, writer, justProfile);
                    }
                    else if (path.StartsWith("/users/") && method == "PUT")
                    {
                        string username = path.Substring(7); // Extract the username (e.g., /users/kienboec => "kienboec")
                        string authToken = authHeader.Replace("Bearer ", "").Trim();  // Remove "Bearer " part
                        string usernameFromToken = authToken.Replace("-mtcgToken", "").Trim();
                        if (username != usernameFromToken)
                        {
                            await SendResponse(writer, 403, "Forbidden: Token does not match the requested user");
                            return;
                        }

                        // Proceed to handle the user data request
                        await HandleEditProfile(username, authToken, writer, requestBody);
                    }
                    else if (path == "/stats" && method == "GET")
                    {
                        string authToken = authHeader.Replace("Bearer ", "").Trim();  // Remove "Bearer " part
                        string usernameFromToken = authToken.Replace("-mtcgToken", "").Trim();
                        bool justProfile = false;
                        string username = "";
                        // Proceed to handle the user stats request
                        await HandleGetUser(username, authToken, writer, justProfile);
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
        public async Task HandleEditProfile(string username, string authToken, StreamWriter writer, string requestBody)
        {
            // Extract the token (remove "Bearer " prefix) from the Authorization header
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            Console.WriteLine("Username: " + username);
            Console.WriteLine("Auth Token: " + inputToken);

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Verify user based on authToken
            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(username + "-mtcgToken");

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }
            Console.WriteLine("User ID: " + userId);

            // Deserialize the request body
            UserProfileUpdateRequest updateRequest = JsonConvert.DeserializeObject<UserProfileUpdateRequest>(requestBody);

            // Validate the update request (optional but recommended)
            if (updateRequest == null || string.IsNullOrEmpty(updateRequest.Name))
            {
                await SendResponse(writer, 400, "Bad Request: Invalid input");
                return;
            }

            // Update the user's profile in the database
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();

            string updateUserQuery = @"
        UPDATE users 
        SET username = @Name, bio = @Bio, image = @Image 
        WHERE id = @userId";

            using (var command = new NpgsqlCommand(updateUserQuery, connection))
            {
                command.Parameters.AddWithValue("@Name", updateRequest.Name);
                command.Parameters.AddWithValue("@Bio", updateRequest.Bio ?? (object)DBNull.Value);  // Handle nullable fields
                command.Parameters.AddWithValue("@Image", updateRequest.Image ?? (object)DBNull.Value); // Handle nullable fields
                command.Parameters.AddWithValue("@userId", userId.Value);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // Successfully updated user profile
                    await SendResponse(writer, 200, "Profile updated successfully");
                }
                else
                {
                    // Something went wrong or user profile not found
                    await SendResponse(writer, 404, "User not found");
                }
            }
        }

        public async Task HandleGetUser(string username, string authToken, StreamWriter writer, bool justProfile)
        {
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            Console.WriteLine("username " + username);

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Verify user based on authToken
            var userService = _serviceProvider.GetRequiredService<UserService>();
            //string userToken = username + "-mtcg"
            int? userId = await userService.GetUserIdByTokenAsync(username +"-mtcgToken");

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }
            Console.WriteLine("User ID: " + userId);

            // Retrieve the user's data from the database
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
            await connection.OpenAsync();

            // Query user data
            var getUserQuery = "SELECT username, bio, image FROM users WHERE id = @userId";
            User user = null;

            using (var command = new NpgsqlCommand(getUserQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId.Value);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        user = new User
                        {
                            username = reader.GetString(0),
                            bio = reader.IsDBNull(1) ? null : reader.GetString(1),  // Handle nullable bio
                            image = reader.IsDBNull(2) ? null : reader.GetString(2)
                        };
                    }
                }
            }

            if (user == null)
            {
                await SendResponse(writer, 404, "User not found");
                return;
            }

            // Format and send the user data as the response
            StringBuilder responseBuilder = new StringBuilder();
            responseBuilder.AppendLine($"Username: {user.username}");
            responseBuilder.AppendLine($"Bio: {user.bio}");
            responseBuilder.AppendLine($"Image: {user.image}");
            responseBuilder.AppendLine($"ELO: {user.elo}");
            responseBuilder.AppendLine($"Coins: {user.coins}");
            responseBuilder.AppendLine($"Wins: {user.wins}");
            responseBuilder.AppendLine($"Losses: {user.losses}");

            await SendResponse(writer, 200, responseBuilder.ToString());
        }



        public async Task HandleDeckUpdate(string authToken, string body, StreamWriter writer)
        {
            // Strip "Bearer " from token
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

            // Parse the body into a list of card IDs (UUIDs)
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
                var checkCardQuery = "SELECT 1 FROM cards c JOIN users u ON c.user_id = u.id WHERE c.id = @cardId AND c.user_id = @userId";
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

            // If fewer than 4 valid cards, return a 400 error
            if (validCardIds.Count != 4)
            {
                await SendResponse(writer, 400, "One or more cards do not belong to the user or the number of valid cards is incorrect.");
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

            // Fetch the updated deck and send it as a formatted response
            var getDeckQuery = "SELECT c.name, c.damage FROM cards c JOIN user_deck ud ON c.id = ud.card_id WHERE ud.user_id = @userId";
            var userDeck = new List<Card>();

            using (var command = new NpgsqlCommand(getDeckQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId.Value);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userDeck.Add(new Card(connection)
                        {
                            name = reader.GetString(0),
                            damage = reader.GetInt32(1)
                        });
                    }
                }
            }

            // Pad the response with placeholders if the deck has fewer than 4 cards
            while (userDeck.Count < 4)
            {
                userDeck.Add(new Card(connection) { name = "InvalidCard", damage = 0 });
            }

            // Build the response with a user-friendly format: "Name: ___ Damage: ___"
            StringBuilder responseBuilder = new StringBuilder();
            foreach (var card in userDeck)
            {
                responseBuilder.AppendLine($"Name: {card.name} Damage: {card.damage}");
            }

            // Send the response with the formatted deck
            await SendResponse(writer, 200, responseBuilder.ToString());
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
                // Build the response in the required format
                StringBuilder responseBuilder = new StringBuilder();

                foreach (var card in userDeck)
                {
                    // Append the card details in the format "Name: <card_name>\nDamage: <card_damage>"
                    responseBuilder.AppendLine($"Name: {card.name}");
                    responseBuilder.AppendLine($"Damage: {card.damage}");
                    responseBuilder.AppendLine();  // Add an empty line between each card
                }

                // Convert the StringBuilder to string and send the response
                string response = responseBuilder.ToString();
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
    public class UserProfileUpdateRequest
    {
        public string Name { get; set; }
        public string Bio { get; set; }
        public string Image { get; set; }
    }
}