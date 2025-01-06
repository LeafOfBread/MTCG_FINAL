using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Package
    {
        private readonly Card _cardService;
        private readonly UserService _userService;

        public Package(Card cardService, UserService userService)
        {
            _cardService = cardService;
            _userService = userService;
            cardsInPack = new List<Card>();
        }

        public string id { get; set; }
        public string name { get; set; }
        public double damage { get; set; }
        public List<Card> cardsInPack { get; set; } = new List<Card>();

        private static readonly string host = "localhost";
        private static readonly string username = "postgres";
        private static readonly string password = "fhtw";
        private static readonly string database = "mtcg";

        public static readonly string connectionString = $"Host={host};Username={username};Password={password};Database={database};Port=5432";

        public (Package, int) createPackage(string Auth, List<Dictionary<string, object>> body, NpgsqlConnection connection)
        {
            string inputToken = Auth.Replace("Bearer ", "").Trim();

            if (inputToken == "admin-mtcgToken")
            {
                if (body.Count != 5)
                {
                    Console.WriteLine("Not enough Cards");
                    return (null, 400); // Return 400 for bad request
                }

                // Generate a new package ID
                var packageId = Guid.NewGuid();
                string insertPackageQuery = "INSERT INTO packages (package_id) VALUES (@packageId)";

                using (var packageCommand = new NpgsqlCommand(insertPackageQuery, connection))
                {
                    packageCommand.Parameters.AddWithValue("@packageId", packageId);
                    connection.Open();
                    packageCommand.ExecuteNonQuery();
                }

                Package package = new Package(_cardService, _userService)
                {
                    id = packageId.ToString(),
                };

                foreach (var bodyElement in body)
                {
                    var card = new Card(connection)
                    {
                        id = Guid.Parse(bodyElement["Id"].ToString()),
                        name = bodyElement["Name"].ToString(),
                        damage = float.Parse(bodyElement["Damage"].ToString())
                    };

                    string insertCardQuery = @"
                INSERT INTO cards (id, package_id, name, damage)
                VALUES (@id, @packageId, @name, @damage)";

                    using (var cardCommand = new NpgsqlCommand(insertCardQuery, connection))
                    {
                        cardCommand.Parameters.AddWithValue("@id", card.id);
                        cardCommand.Parameters.AddWithValue("@packageId", packageId);
                        cardCommand.Parameters.AddWithValue("@name", card.name);
                        cardCommand.Parameters.AddWithValue("@damage", card.damage);
                        cardCommand.ExecuteNonQuery();
                    }

                    package.cardsInPack.Add(card);
                }

                foreach (var card in package.cardsInPack)
                {
                    Console.WriteLine($"ID: {card.id}, Name: {card.name}, Damage: {card.damage}");
                }

                return (package, 0); // Success
            }

            return (null, 401); // Unauthorized
        }


        public async Task<List<Card>> GetPackageCardsAsync(int packageId)
        {
            List<Card> packageCards = new List<Card>();

            const string query = @"
    SELECT c.id, c.name, c.damage 
    FROM cards c
    JOIN package_cards pc ON c.id = pc.card_id
    WHERE pc.package_id = @packageId";

            using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432"))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@packageId", packageId); // Pass the correct packageId
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            packageCards.Add(new Card(connection)
                            {
                                id = reader.GetGuid(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("name")),
                                damage = reader.GetDouble(reader.GetOrdinal("damage"))
                            });
                        }
                    }
                }
            }

            return packageCards;
        }

    }

    public class CreatePackageRequest
    {
        public List<Package> Cards { get; set; }
    }

}
