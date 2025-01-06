using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Card
    {
        private readonly NpgsqlConnection _connection;

        public Card(NpgsqlConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public Guid id { get; set; }
        public string name { get; set; }
        public double damage { get; set; }

        public async Task AddCardAsync(Card card, string connectionString)
        {
            const string insertPackageQuery = "INSERT INTO public.packages DEFAULT VALUES RETURNING package_id";
            const string insertCardQuery = "INSERT INTO public.cards (id, name, damage, package_id) VALUES (@id, @name, @damage, @packageId)"; // Include package_id

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Step 1: Insert a package and get the package_id
                    int packageId = 0;
                    using (var command = new NpgsqlCommand(insertPackageQuery, connection))
                    {
                        packageId = (int)await command.ExecuteScalarAsync();
                    }

                    // Step 2: Insert the card into the cards table with the package_id
                    using (var command = new NpgsqlCommand(insertCardQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", card.id);
                        command.Parameters.AddWithValue("@name", card.name);
                        command.Parameters.AddWithValue("@damage", card.damage);
                        command.Parameters.AddWithValue("@packageId", packageId); // Pass the package_id
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting card: {ex.Message}");
                throw;
            }
        }


        public async Task AddCardsToUserInventoryAsync(User user, List<Card> cards)
        {
            const string query = "INSERT INTO user_cards (user_id, card_id) VALUES (@userId, @cardId)";
            using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432"))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    foreach (var card in cards)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@userId", user.id);
                        command.Parameters.AddWithValue("@cardId", card.id);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }


    }

}
