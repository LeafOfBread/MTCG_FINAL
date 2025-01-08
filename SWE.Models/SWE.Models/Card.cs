using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public enum CardType
    {
        Monster,
        Spell
    }

    public enum ElementType
    {
        Fire,
        Water,
        Normal
    }

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
        public CardType Type { get; set; }
        public ElementType Element { get; set; }
        public Guid package_id { get; set; }

        public Card(string name)
        {
            this.name = name;
            SetCardTypeAndElement();
        }

        public static CardType GetCardType(string cardName)
        {
            if (cardName.Contains("Monster"))
            {
                return CardType.Monster;
            }
            else if (cardName.Contains("Spell"))
            {
                return CardType.Spell;
            }
            return CardType.Monster;
        }

        public static ElementType GetElementalType(string cardName)
        {
            if (cardName.Contains("Fire"))
            {
                return ElementType.Fire;
            }
            else if (cardName.Contains("Water"))
            {
                return ElementType.Water;
            }
            return ElementType.Normal;
        }

        public void SetCardTypeAndElement()
        {
            if (name.Contains("Spell", StringComparison.OrdinalIgnoreCase))
            {
                Type = CardType.Spell;
            }
            else if (name.Contains("Elf", StringComparison.OrdinalIgnoreCase) || name.Contains("Dragon", StringComparison.OrdinalIgnoreCase))
            {
                Type = CardType.Monster;
            }
            else
            {
                Type = CardType.Monster;
            }

            if (name.Contains("Fire", StringComparison.OrdinalIgnoreCase))
            {
                Element = ElementType.Fire;
            }
            else if (name.Contains("Water", StringComparison.OrdinalIgnoreCase))
            {
                Element = ElementType.Water;
            }
            else
            {
                Element = ElementType.Normal;
            }
        }


        //add card to the database
        public async Task AddCardAsync(Card card, string connectionString)
        {
            const string insertPackageQuery = "INSERT INTO public.packages DEFAULT VALUES RETURNING package_id";
            const string insertCardQuery = "INSERT INTO public.cards (id, name, damage, package_id, type, element) VALUES (@id, @name, @damage, @packageId, @type, @element)";

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Insert package  get package_id
                            Guid packageId = Guid.Empty;
                            using (var command = new NpgsqlCommand(insertPackageQuery, connection, transaction))
                            {
                                packageId = (Guid)await command.ExecuteScalarAsync();
                            }

                            // Insert card with package_id, type, and element
                            using (var command = new NpgsqlCommand(insertCardQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@id", card.id);
                                command.Parameters.AddWithValue("@name", card.name);
                                command.Parameters.AddWithValue("@damage", card.damage);
                                command.Parameters.AddWithValue("@packageId", packageId);
                                command.Parameters.AddWithValue("@type", card.Type.ToString());
                                command.Parameters.AddWithValue("@element", card.Element.ToString());
                                await command.ExecuteNonQueryAsync();
                            }

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
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

        //add cards to user inventory
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