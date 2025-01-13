using System;
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
using System.ComponentModel.Design;
using System.Reflection;


namespace SWE.Models
{

    //tcp server mit users, sessions und routes
    public class TcpServer
    {
        //dependency injections
        private readonly IServiceProvider _serviceProvider;
        private Queue<User> battleQueue = new();
        private static readonly object battleQueueLock = new object();
        private static string connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";


        private List<Package> packs;


        public TcpServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
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

        public class HttpException : Exception
        {
            public int StatusCode { get; }

            public HttpException(int statusCode, string message) : base(message)
            {
                StatusCode = statusCode;
            }
        }

        public class BadRequestException : HttpException
        {
            public BadRequestException(string message = "Bad Request") : base(400, message) { }
        }

        public class UnauthorizedException : HttpException
        {
            public UnauthorizedException(string message = "Unauthorized") : base(401, message) { }
        }

        public class NotFoundException : HttpException
        {
            public NotFoundException(string message = "Not Found") : base(404, message) { }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                using (StreamReader reader = new StreamReader(stream))
                {
                    string? requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(requestLine))
                        throw new BadRequestException();

                    string[] requestParts = requestLine.Split(' ');
                    if (requestParts.Length < 3)
                        throw new BadRequestException();

                    int contentLength = 0;
                    string? authHeader = null;
                    string? line;
                    bool headersEnd = false;
                    string requestBody = "";

                    // Parse request headers
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
                        else if (line == "")
                        {
                            headersEnd = true;
                        }
                    }

                    string method = requestParts[0];
                    string path = requestParts[1].Trim();

                    // Read request body
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        await reader.ReadAsync(buffer, 0, contentLength);
                        requestBody = new string(buffer);
                    }

                    // Route requests
                    if (method == "POST")
                    {
                        switch (path)
                        {
                            case "/sessions":
                                await HandleLogin(requestBody, new Dictionary<string, string>(), stream);
                                break;

                            case "/users":
                                await HandleRegisterUser(requestBody, new Dictionary<string, string>(), stream);
                                break;

                            case "/packages":
                                var parsedBody = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(requestBody);
                                if (parsedBody == null || authHeader == null)
                                    throw new BadRequestException("Invalid JSON in request body.");
                                await HandlePackages(parsedBody, authHeader, stream);
                                break;

                            case "/battles":
                                if (authHeader == null)
                                    throw new UnauthorizedException("Missing or invalid Authorization header.");

                                string authToken = authHeader.Replace("Bearer ", "").Trim();

                                
                                await HandleBattle(writer, authToken);
                                break;

                            case string _ when path.StartsWith("/transactions/packages"):
                                if (authHeader == null)
                                    throw new UnauthorizedException("Missing or invalid Authorization header.");
                                await HandleAcquirePackages(authHeader, stream);
                                break;

                            default:    
                                throw new NotFoundException();
                        }
                    }
                    else if (method == "GET")
                    {
                        bool isPlain;
                        switch (path)
                        {
                            case "/cards":
                                if (string.IsNullOrEmpty(authHeader))
                                    throw new UnauthorizedException("Missing or invalid Authorization header.");
                                await HandleCardListing(authHeader, stream);
                                break;

                            case "/deck":
                                if (authHeader == null)
                                    throw new UnauthorizedException("Missing or invalid Authorization header.");
                                isPlain = false;
                                await HandleListPlayingDeck(authHeader, stream, isPlain);
                                break;

                            case "/deck?format=plain":
                                if (authHeader == null)
                                    throw new UnauthorizedException("Missing or invalid Authorization header!");
                                isPlain = true;
                                await HandleListPlayingDeck(authHeader, stream, isPlain);
                                break;

                            case "/scoreboard":
                                await HandleScoreBoard(stream);
                                break;

                            case "/stats":
                                if (path.StartsWith("/stats"))
                                {
                                    if(authHeader == null)
                                        throw new UnauthorizedException("Missing or invalid Authorization header.");
                                    string authToken = authHeader.Replace("Bearer ", "").Trim();
                                    string usernameFromToken = authToken.Replace("-mtcgToken", "").Trim();

                                    bool justProfile = false;
                                    bool isStatsRequest = true;
                                    await HandleGetUser(usernameFromToken, authToken, writer, justProfile, isStatsRequest);
                                }
                                break;

                            case string _ when path.StartsWith("/users/"):
                                if (path.StartsWith("/users/"))
                                {
                                    string username = path.Substring(7);
                                    string authToken = authHeader.Replace("Bearer ", "").Trim();
                                    if (authHeader == null)
                                        throw new UnauthorizedException("Missing or invalid Authorization header.");

                                    bool justProfile = true;
                                    bool isStatsRequest = false;
                                    await HandleGetUser(username, authToken, writer, justProfile, isStatsRequest);
                                }
                                break;
                        }
                    }
                    else if (method == "PUT")
                    {
                        switch (path)
                        {
                            case "/deck":
                                if (authHeader == null)
                                    throw new UnauthorizedAccessException("Missing or invalid Authorization header.");
                                await HandleDeckUpdate(authHeader, requestBody, writer);        // deck update
                                break;

                            default:
                                if (path.StartsWith("/users/"))
                                {
                                    string username = path.Substring(7);
                                    string authToken = authHeader.Replace("Bearer ", "").Trim();

                                    if (username != authToken.Replace("-mtcgToken", "").Trim())
                                    {
                                        await SendResponse(stream, 403, "Forbidden: Token does not match the requested user");
                                    }
                                    else
                                    {
                                        await HandleEditProfile(username, authToken, writer, requestBody); // Profile edit
                                    }
                                }
                                else
                                {
                                    await SendResponse(stream, 404, "Not Found");
                                }
                                break;
                        }
                    }
                    else
                        throw new HttpException(405, "Method Not Allowed");
                }
            }
            catch (HttpException ex)
            {
                Console.WriteLine($"HTTP Error: {ex.StatusCode} - {ex.Message}");
                using (NetworkStream stream = client.GetStream())
                {
                    await SendResponse(stream, ex.StatusCode, ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Internal Server Error: {ex.Message}");
                using (NetworkStream stream = client.GetStream())
                {
                    await SendResponse(stream, 500, "Internal Server Error");
                }
            }
        }

        private async Task HandleBattle(StreamWriter writer, string authToken)
        {
            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(authToken);

            if (userId == null)
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }

            User currentPlayer = await userService.GetUserByIdAsync(userId);

            var deck = await userService.GetDeckForUser(currentPlayer);
            if (deck.Cards.Count < 4)
            {
                await SendResponse(writer.BaseStream, 400, "{\"message\": \"Build a valid Deck First\"}");
                return;
            }

            currentPlayer.Deck = deck; // Assign deck to the player

            User? player1 = null, player2 = null;

            lock (battleQueueLock)
            {
                battleQueue.Enqueue(currentPlayer);
                Console.WriteLine($"Player {currentPlayer.username} added to queue. Queue size: {battleQueue.Count}");

                if (battleQueue.Count >= 2)
                {
                    battleQueue.TryDequeue(out player1);
                    battleQueue.TryDequeue(out player2);
                }
            }

            if (player1 != null && player2 != null)
            {
                // assign decks for both players
                player1.Deck = await userService.GetDeckForUser(player1);
                player2.Deck = await userService.GetDeckForUser(player2);

                if (player1.Deck.Cards.Count < 4 || player2.Deck.Cards.Count < 4)
                {
                    await SendResponse(writer.BaseStream, 400, "{\"message\": \"One of the players has an invalid deck.\"}");
                    return;
                }

                List<string> battleLog = new List<string>();
                bool draw = SimulateBattle(player1, player2, battleLog);

                Console.WriteLine("Battle log content:");
                battleLog.ForEach(Console.WriteLine);

                if (!draw)
                {
                    User winner = player1.isWinner ? player1 : player2;
                    User loser = player1.isWinner ? player2 : player1;

                    winner.wins++;
                    winner.elo += 3;
                    loser.losses++;
                    loser.elo -= 5;

                    // Use StringBuilder to build a readable battle log
                    StringBuilder battleLogBuilder = new StringBuilder();

                    foreach (var logEntry in battleLog)
                    {
                        battleLogBuilder.AppendLine(logEntry);
                    }

                    // Convert StringBuilder to string and send the formatted log
                    string formattedBattleLog = battleLogBuilder.ToString();

                    var response = new
                    {
                        message = "Battle completed",
                        log = formattedBattleLog,  // Send the formatted log as a string
                        winner = $"Winner: {winner.username}"
                    };

                    await userService.UpdateUserAsync(player1);
                    await userService.UpdateUserAsync(player2);

                    string jsonResponse = JsonConvert.SerializeObject(response);
                    await SendResponse(writer.BaseStream, 200, jsonResponse);
                }
                else
                {
                    await SendResponse(writer.BaseStream, 200, "Battle resulted in a draw!");
                }
            }
            else
            {
                await SendResponse(writer.BaseStream, 200, "{\"message\": \"Waiting for an opponent...\"}");
            }
        }


        private bool SimulateBattle(User player1, User player2, List<string> battleLog)
        {
            var deck1 = player1.Deck;
            var deck2 = player2.Deck;

            Random random = new Random();
            int rounds = 0;
            const int maxRounds = 100;

            while (rounds < maxRounds && deck1.Cards.Count > 0 && deck2.Cards.Count > 0)
            {
                var card1 = deck1.Cards[random.Next(deck1.Cards.Count)];
                var card2 = deck2.Cards[random.Next(deck2.Cards.Count)];

                double damage1 = CalculateDamage(card1, card2, battleLog);
                double damage2 = CalculateDamage(card2, card1, battleLog);

                if (damage1 > damage2)
                {
                    deck1.Cards.Add(card2);
                    deck2.Cards.Remove(card2);
                    battleLog.Add($"Round {rounds + 1}: {player1.username}'s {card1.name} defeated {player2.username}'s {card2.name}\n");
                }
                else if (damage2 > damage1)
                {
                    deck2.Cards.Add(card1);
                    deck1.Cards.Add(card1);
                    battleLog.Add($"Round {rounds + 1}: {player2.username}'s {card2.name} defeated {player1.username}'s {card1.name}\n");
                }
                else
                {
                    battleLog.Add($"Round {rounds + 1}: {card1.name} and {card2.name} resulted in a draw.\n");
                }

                rounds++;
            }

            if (rounds >= maxRounds)
            {
                battleLog.Add("Battle ended in a draw after 100 rounds.\n");
                return true; // draw
            }

            player1.isWinner = deck2.Count == 0;
            player2.isWinner = deck1.Count == 0;
            return false; // battle had a winner
        }

        private double CalculateDamage(Card attacker, Card defender, List<string> battleLog)
        {
            // Increase the attacker's damage based on win streak
            double damageBoost = 1 + (attacker.winStreak * 0.2);
            double damage = attacker.damage * damageBoost;
            
            if (damageBoost != 1)
                Console.WriteLine("Card's Damageboost: " + damageBoost);

            // Elemental effectiveness
            if (attacker.Type == CardType.Spell && defender.Type == CardType.Monster)
            {
                if ((attacker.Element == ElementType.Water && defender.Element == ElementType.Fire) ||
                    (attacker.Element == ElementType.Fire && defender.Element == ElementType.Normal) ||
                    (attacker.Element == ElementType.Normal && defender.Element == ElementType.Water))
                {
                    damage *= 2;
                    battleLog.Add($"{attacker.name}'s attack was effective against {defender.name}\n");
                }
                else if ((attacker.Element == ElementType.Fire && defender.Element == ElementType.Water) ||
                         (attacker.Element == ElementType.Normal && defender.Element == ElementType.Fire) ||
                         (attacker.Element == ElementType.Water && defender.Element == ElementType.Normal))
                {
                    damage /= 2;
                    battleLog.Add($"{attacker.name}'s attack was not effective against {defender.name}\n");
                }
            }

            if (attacker.name.Contains("Goblin") && defender.name == "Dragon")
            {
                damage = 0; // goblins fear dragons
                battleLog.Add($"{attacker.name} was too afraid to attack {defender.name}\n");
            }
            else if (attacker.name.Contains("Wizzard") && defender.name.Contains("Ork"))
            {
                damage = 500; // wizzards control orks
                battleLog.Add($"{attacker.name} controlled {defender.name}, preventing damage\n");
            }
            else if (attacker.name == "Knight" && defender.Element == ElementType.Water)
            {
                damage = 0; //knight drowns
                battleLog.Add($"{attacker.name} drowned instantly from {defender.name}'s water spell\n");
            }
            else if (attacker.name == "Kraken" && defender.Type == CardType.Spell)
            {
                damage = 500; // kraken is immune to spells
                battleLog.Add($"{attacker.name} is immune to spells like {defender.name}\n");
            }
            else if (attacker.name == "FireElf" && defender.name == "Dragon")
            {
                damage = 500; // fireelves evade dragons
                battleLog.Add($"{attacker.name} evaded {defender.name}'s attack\n");
            }

            // critical strike check
            Random rand = new Random();
            double criticalChance = rand.NextDouble();
            if (criticalChance < attacker.criticalStrikeChance)
            {
                damage *= 2;
                battleLog.Add($"{attacker.name} landed a critical hit on {defender.name}, boosting damage!\n");
            }

                
                double randomFactor = new Random().NextDouble() * 5;
                if (damage < defender.damage)
                {
                    damage += randomFactor;
                    battleLog.Add($"{attacker.name}'s attack was very close to {defender.name}'s defense, but random factor gave the edge to {attacker.name}\n");
                }
                else
                {
                    damage -= randomFactor;
                    battleLog.Add($"{defender.name}'s defense was very close to {attacker.name}'s attack, but random factor gave the edge to {defender.name}\n");
                }

            Console.WriteLine("Player1's damage: " + damage + " Player2's damage: " + defender.damage);
            return damage;
        }

        public async Task HandleScoreBoard(Stream writer)
        {
            try
            {
                var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                string getScoreboardQuery = @"
        SELECT username, elo FROM users ORDER BY elo DESC"; //query um alle user nach elo zu sortieren

                List<User> players = new List<User>();

                using (var command = new NpgsqlCommand(getScoreboardQuery, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        players.Add(new User
                        {
                            username = reader.GetString(0),
                            elo = reader.GetInt32(1)
                        });
                    }
                }

                if (players.Count == 0)
                {
                    await SendResponse(writer, 404, "No players found");
                    return;
                }
                //response besser anzeigen
                StringBuilder responseBuilder = new StringBuilder();
                responseBuilder.AppendLine("Scoreboard:");

                foreach (var player in players)
                {
                    responseBuilder.AppendLine($"{player.username} - ELO: {player.elo}");
                }

                await SendResponse(writer, 200, responseBuilder.ToString());
            }
            catch (Exception ex)
            {
                await SendResponse(writer, 500, $"Internal Server Error: {ex.Message}");
                Console.WriteLine($"Error in HandleScoreBoard: {ex.Message}");
            }
        }


        public async Task HandleEditProfile(string username, string authToken, StreamWriter writer, string requestBody)
        {
            // Extract token and trim
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            Console.WriteLine("Username: " + username);
            Console.WriteLine("Auth Token: " + inputToken);

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }

            string usernameFromToken = inputToken.Replace("-mtcgToken", "").Trim();

            if (username != usernameFromToken)
            {
                await SendResponse(writer.BaseStream, 403, "Forbidden: Token does not match the requested user");
                return;
            }

            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(inputToken);  // Use inputToken directly

            if (userId == null)
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }
            Console.WriteLine("User ID: " + userId);

            UserProfileUpdateRequest? updateRequest = JsonConvert.DeserializeObject<UserProfileUpdateRequest>(requestBody);

            if (updateRequest == null || string.IsNullOrEmpty(updateRequest.Name))
            {
                await SendResponse(writer.BaseStream, 400, "Bad Request: Invalid input");
                return;
            }

            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            string updateUserQuery = @"UPDATE users 
                               SET username = @Name, bio = @Bio, image = @Image 
                               WHERE id = @userId";

            using (var command = new NpgsqlCommand(updateUserQuery, connection))
            {
                command.Parameters.AddWithValue("@Name", updateRequest.Name);
                command.Parameters.AddWithValue("@Bio", updateRequest.Bio ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Image", updateRequest.Image ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@userId", userId.Value);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // Successfully updated user profile
                    await SendResponse(writer.BaseStream, 200, "Profile updated successfully");
                }
                else
                {
                    await SendResponse(writer.BaseStream, 404, "User not found");
                }
            }
        }


        public async Task HandleGetUser(string username, string authToken, StreamWriter writer, bool justProfile, bool isStatsRequest)
        {
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            var tokenParts = inputToken.Split('-');
            Console.WriteLine("username: " + username);

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }

            string usernameWithToken = username + "-mtcgToken";
            Console.WriteLine("usernameWithToken: " + usernameWithToken);

            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(usernameWithToken);

            if (userId == null)
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }
            Console.WriteLine("User ID: " + userId);

            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            string getUserQuery;

            if (isStatsRequest)     //abhaengig davon, ob route /stats oder /users/<username> aufgerufen wird - vermeidet redundanz einer zweiten methode, die eigentlich das gleiche machen wuerde
                getUserQuery = "SELECT username, bio, image, ingame_name, elo, coins, wins, losses FROM users WHERE id = @userId";
            else
                getUserQuery = "SELECT username, bio, image, ingame_name, elo FROM users WHERE id = @userId";

            User? user = null;

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
                            bio = reader.IsDBNull(1) ? null : reader.GetString(1),
                            image = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ingameName = reader.GetString(3),
                            elo = reader.GetInt32(4)
                        };

                        if (isStatsRequest) // Only for stats request
                        {
                            user.coins = reader.GetInt32(5);
                            user.wins = reader.GetInt32(6);
                            user.losses = reader.GetInt32(7);
                        }
                    }
                }
            }
            if (user == null)
            {
                await SendResponse(writer.BaseStream, 404, "User not found");
                return;
            }
            else if (user.ingameName != tokenParts[0] && !isStatsRequest)
            {
                Console.WriteLine(user.username);
                await SendResponse(writer.BaseStream, 400, "Username and Token do not match up");
                return;
            }
            Console.WriteLine("username: " + user.username + " user token: " + inputToken + "-mtcgToken");
            StringBuilder responseBuilder = new StringBuilder();

            if (isStatsRequest)  // Stats response
            {
                responseBuilder.AppendLine($"ELO: {user.elo}");
                responseBuilder.AppendLine($"Coins: {user.coins}");
                responseBuilder.AppendLine($"Wins: {user.wins}");
                responseBuilder.AppendLine($"Losses: {user.losses}");
            }
            else  // Profile response
            {
                responseBuilder.AppendLine($"Username: {user.username}");
                responseBuilder.AppendLine($"Bio: {user.bio}");
                responseBuilder.AppendLine($"Image: {user.image}");
            }

            await SendResponse(writer.BaseStream, 200, responseBuilder.ToString());
        }


        public async Task HandleDeckUpdate(string authToken, string body, StreamWriter writer)
        {

            string inputToken = authToken.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(inputToken))
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }

            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer.BaseStream, 401, "Unauthorized");
                return;
            }

            List<Guid>? cardIds = JsonConvert.DeserializeObject<List<Guid>>(body);

            if (cardIds == null)
            {
                await SendResponse(writer.BaseStream, 400, "Invalid request body");
                return;
            }
            if (cardIds.Count != 4)
            {
                await SendResponse(writer.BaseStream, 400, "Invalid number of cards. You must provide exactly 4 cards.");
                return;
            }

            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var validCardIds = new List<Guid>();

            // sicherstellen dass alle karten dem nutzer gehören
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

            //weniger als 4 karten == error
            if (validCardIds.Count != 4)
            {
                await SendResponse(writer.BaseStream, 400, "One or more cards do not belong to the user or the number of valid cards is incorrect.");
                return;
            }

            // Clear user deck
            var clearDeckQuery = "DELETE FROM user_deck WHERE user_id = @userId";
            using (var command = new NpgsqlCommand(clearDeckQuery, connection))
            {
                command.Parameters.AddWithValue("@userId", userId.Value);
                await command.ExecuteNonQueryAsync();
            }

            // Insert cards
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

            // Fetch updated
            var getDeckQuery = "SELECT c.id, c.name, c.damage FROM cards c JOIN user_deck ud ON c.id = ud.card_id WHERE ud.user_id = @userId";
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
                            id = reader.GetGuid(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            damage = reader.GetDouble(reader.GetOrdinal("damage")),
                            Type = Card.GetCardType(reader.GetString(reader.GetOrdinal("name"))),
                            Element = Card.GetElementalType(reader.GetString(reader.GetOrdinal("name")))
                        });
                    }
                }
            }

            while (userDeck.Count < 4)
            {
                userDeck.Add(new Card(connection) { name = "InvalidCard", damage = 0 });
            }

            await SendResponse(writer.BaseStream, 200, "User Deck successfully updated!");
        }

        public async Task HandleListPlayingDeck(string authToken, Stream writer, bool isPlain)
        {
            Console.WriteLine("authToken debug " + authToken);
            if (string.IsNullOrEmpty(authToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }
            string inputToken = authToken.Replace("Bearer ", "").Trim();

            var userService = _serviceProvider.GetRequiredService<UserService>();
            int? userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Fetch user's deck from the database
            var connection = new NpgsqlConnection(connectionString);
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
                            damage = reader.GetDouble(reader.GetOrdinal("damage")),
                            Type = Card.GetCardType(reader.GetString(reader.GetOrdinal("name"))),
                            Element = Card.GetElementalType(reader.GetString(reader.GetOrdinal("name")))
                        });
                    }
                }
            }

            if (userDeck.Count == 0)
            {
                string response = JsonConvert.SerializeObject(new List<Card>());
                await SendResponse(writer, 200, response);
            }
            else if (!isPlain)
            {
                StringBuilder responseBuilder = new StringBuilder();

                foreach (var card in userDeck)
                {
                    responseBuilder.AppendLine($"Name: {card.name}");
                    responseBuilder.AppendLine($"Damage: {card.damage}");
                    responseBuilder.AppendLine($"Type: {card.Type}");
                    responseBuilder.AppendLine($"Element: {card.Element}");
                    responseBuilder.AppendLine();
                }

                string response = responseBuilder.ToString();
                await SendResponse(writer, 200, response);
            }
            else
            {
                var jsonResponse = JsonConvert.SerializeObject(userDeck, Formatting.Indented);
                await SendResponse(writer, 200, jsonResponse);
            }
        }


        public async Task<int> HandleAcquirePackages(string authToken, Stream stream)
        {
            string inputToken = authToken.Replace("Bearer ", "").Trim();
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var userService = _serviceProvider.GetRequiredService<UserService>();

            int? userId = await userService.GetUserIdByTokenAsync(inputToken);
            if (userId == null)
            {
                await SendResponse(stream, 401, "Unauthorized");
                Console.WriteLine("Error giving packages: User = NULL");
                return -1;
            }
            //user coin balance
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

            const int packageCost = 5;

            if (userCoins < packageCost)
            {
                await SendResponse(stream, 400, "Not enough coins");
                Console.WriteLine("Error giving packages: User does not have enough coins!");
                return -1;
            }
            //bekomme das erste package mit 5 karten
            const string getPackageQuery = @"
SELECT p.package_id
FROM packages p
JOIN cards c ON c.package_id = p.package_id
WHERE c.user_id IS NULL
GROUP BY p.package_id
HAVING COUNT(c.id) = 5
ORDER BY p.number ASC
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
                await SendResponse(stream, 404, "No packages available");
                Console.WriteLine("Error giving packages: No packages available");
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
            await SendResponse(stream, 200, $"You received: {response}");
            Console.WriteLine("User received: " + response);

            return 0;
        }

        public async Task HandleCardListing(string authToken, Stream writer)
        {
            Console.WriteLine("authToken debug " + authToken);
            if (string.IsNullOrEmpty(authToken))
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }
            string inputToken = authToken.Replace("Bearer ", "").Trim();

            var userService = _serviceProvider.GetRequiredService<UserService>();
            int ?userId = await userService.GetUserIdByTokenAsync(inputToken);

            if (userId == null)
            {
                await SendResponse(writer, 401, "Unauthorized");
                return;
            }

            // Fetch user's cards 
            var connection = new NpgsqlConnection(connectionString);
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

            if (userCards.Count == 0)
            {
                string response = JsonConvert.SerializeObject(new List<Card>());
                await SendResponse(writer, 200, response);
            }
            else
            {
                string response = JsonConvert.SerializeObject(userCards);
                await SendResponse(writer, 200, response);
            }
        }

        public async Task<int> HandlePackages(List<Dictionary<string, object>> receive, string Auth, NetworkStream stream)
        {
            var connection = new NpgsqlConnection(connectionString);
            var cardService = _serviceProvider.GetRequiredService<Card>();
            var userService = _serviceProvider.GetRequiredService<UserService>();

            var packagesINT = new Package(cardService, userService).createPackage(Auth, receive, connection);
            if (packagesINT.Item2 == 0)
            {
                Console.WriteLine("Packages created by Admin");
                await SendResponse(stream, 201, "");
                return 0;
            }
            await SendResponse(stream, 404, "not Authorized");
            return -1;
        }
        private async Task HandleLogin(string body, Dictionary<string, string> headers, Stream writer)
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var request = JsonConvert.DeserializeObject<UserLoginRequest>(body);

            if (request == null || request.username == null || request.password == null)
            {
                await SendResponse(writer, 400, "Request is NULL");
                return;
            }

            if(string.IsNullOrEmpty(request.username) || string.IsNullOrEmpty(request.password))
            {
                await SendResponse(writer, 400, "Invalid username or password");
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

        private async Task HandleRegisterUser(string body, Dictionary<string, string> headers, Stream writer)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<UserService>();  //dependency injection der class UserService
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
                    await SendResponse(writer, 401, "User already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleRegisterUser: {ex.Message}");
                await SendResponse(writer, 500, "Internal Server Error");
            }
        }

        private async Task SendResponse(Stream stream, int statusCode, string message)
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

            string response = $"HTTP/1.1 {statusCode} {statusDescription}\r\n" +
                              "Content-Type: application/json\r\n" +
                              $"Content-Length: {Encoding.UTF8.GetByteCount(message)}\r\n" +
                              "Connection: close\r\n" +
                              "\r\n" +
                              message;
            try {

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}");
            }

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