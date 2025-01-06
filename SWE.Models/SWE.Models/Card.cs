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
            const string insertCardQuery = "INSERT INTO public.cards (id, name, damage, package_id) VALUES (@id, @name, @damage, @packageId)";

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Insert package and get package_id
                            int packageId = 0;
                            using (var command = new NpgsqlCommand(insertPackageQuery, connection, transaction))
                            {
                                packageId = (int)await command.ExecuteScalarAsync();
                            }

                            // Insert card with the package_id
                            using (var command = new NpgsqlCommand(insertCardQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@id", card.id);
                                command.Parameters.AddWithValue("@name", card.name);
                                command.Parameters.AddWithValue("@damage", card.damage);
                                command.Parameters.AddWithValue("@packageId", packageId);
                                await command.ExecuteNonQueryAsync();
                            }

                            // Commit the transaction
                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            // Rollback if any exception occurs
                            await transaction.RollbackAsync();
                            Console.WriteLine($"Error inserting card and package: {ex.Message}");
                            throw;
                        }
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
