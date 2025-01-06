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
            // Extract token from Authorization header
            string inputToken = Auth.Replace("Bearer ", "").Trim();

            if (inputToken == "admin-mtcgToken")
            {
                // Check if the number of cards is correct
                if (body.Count != 5)
                {
                    Console.WriteLine("Not enough Cards");
                    return (null, 400); // Return 400 for bad request
                }

                Package package = new Package();
                foreach (var bodyElement in body)
                {
                    Card card = new Card(connection) // Pass the connection here
                    {
                        id = Guid.Parse(bodyElement["Id"].ToString()),
                        name = bodyElement["Name"].ToString(),
                        damage = float.Parse(bodyElement["Damage"].ToString())
                    };

                    card.AddCardAsync(card, connectionString); // Add the card to the database
                    package.cardsInPack.Add(card); // Add the card to the package's list
                }
                foreach (Card card in package.cardsInPack)
                {
                    Console.WriteLine("ID: " + card.id + " Name: " + card.name + " Damage: " + card.damage);
                }

                return (package, 0); // Return 0 for success
            }
            return (null, 401); // Return 401 for unauthorized
        }

        public async Task AddCardToPackageAsync(Card card, int packageId, string connectionString)
        {
            const string insertCardQuery = "INSERT INTO public.cards (id, name, damage) VALUES (@id, @name, @damage)";
            const string insertPackageCardQuery = "INSERT INTO public.package_cards (package_id, card_id) VALUES (@packageId, @cardId)";

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Step 1: Insert the card into the cards table
                    using (var command = new NpgsqlCommand(insertCardQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", card.id);
                        command.Parameters.AddWithValue("@name", card.name);
                        command.Parameters.AddWithValue("@damage", card.damage);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Step 2: Insert the relationship into package_cards
                    using (var command = new NpgsqlCommand(insertPackageCardQuery, connection))
                    {
                        command.Parameters.AddWithValue("@packageId", packageId);
                        command.Parameters.AddWithValue("@cardId", card.id);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting card and package relationship: {ex.Message}");
                throw;
            }
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
