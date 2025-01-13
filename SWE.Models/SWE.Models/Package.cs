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
        //dependency injection
        private readonly Card _cardService;
        private readonly UserService _userService;

        public Package(Card cardService, UserService userService)
        {
            _cardService = cardService;
            _userService = userService;
            cardsInPack = new List<Card>();
        }

        public string id { get; set; }
        public List<Card> cardsInPack { get; set; } = new List<Card>(); //liste aller karten im pack

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
                    return (null, 400);
                }

                // Generate package ID
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
                        damage = float.Parse(bodyElement["Damage"].ToString()),
                        criticalStrikeChance = new Random().NextDouble() * 0.5
                    };

                    // Set card type and element based on the card's name
                    card.SetCardTypeAndElement();

                    string insertCardQuery = @"
            INSERT INTO cards (id, package_id, name, damage, type, element, criticalstrikechance)
            VALUES (@id, @packageId, @name, @damage, @type, @element, @criticalstrikechance)";

                    using (var cardCommand = new NpgsqlCommand(insertCardQuery, connection))
                    {
                        cardCommand.Parameters.AddWithValue("@id", card.id);
                        cardCommand.Parameters.AddWithValue("@packageId", packageId);
                        cardCommand.Parameters.AddWithValue("@name", card.name);
                        cardCommand.Parameters.AddWithValue("@damage", card.damage);
                        cardCommand.Parameters.AddWithValue("@type", card.Type.ToString());   // Insert card type
                        cardCommand.Parameters.AddWithValue("@element", card.Element.ToString()); // Insert card element
                        cardCommand.Parameters.AddWithValue("@criticalstrikechance", card.criticalStrikeChance);
                        cardCommand.ExecuteNonQuery();
                    }

                    package.cardsInPack.Add(card);
                }

                foreach (var card in package.cardsInPack)
                {
                    Console.WriteLine($"ID: {card.id}, Name: {card.name}, Damage: {card.damage}, Type: {card.Type}, Element: {card.Element}, Crit Chance: {card.criticalStrikeChance}");
                }

                return (package, 0); // Success
            }

            return (null, 401); // Unauthorized
        }
    }
}