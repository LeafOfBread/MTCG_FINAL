using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Deck
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public List<Card> Cards { get; set; }


        public async Task<Deck> GetDeckForUser(User user)
        {
            string connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";

            // Create a connection to your PostgreSQL database
            using (var connection = new NpgsqlConnection(connectionString)) // Ensure _connectionString is your connection string
            {
                await connection.OpenAsync();

                // Create the query to fetch the user's deck
                var deckQuery = "SELECT * FROM decks WHERE user_id = @userId";
                var deckCommand = new NpgsqlCommand(deckQuery, connection);
                deckCommand.Parameters.AddWithValue("@userId", user.id);

                // Execute the query to retrieve the deck
                using (var reader = await deckCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        // Retrieve deck information from the reader
                        int deckId = reader.GetInt32(reader.GetOrdinal("id"));
                        string deckName = reader.GetString(reader.GetOrdinal("name"));

                        // Fetch cards associated with the deck
                        var cards = new List<Card>();
                        var cardQuery = "SELECT * FROM cards WHERE deck_id = @deckId";
                        var cardCommand = new NpgsqlCommand(cardQuery, connection);
                        cardCommand.Parameters.AddWithValue("@deckId", deckId);

                        using (var cardReader = await cardCommand.ExecuteReaderAsync())
                        {
                            while (await cardReader.ReadAsync())
                            {
                                // Assuming the card has an id and name or other fields
                                Guid cardId = cardReader.GetGuid(cardReader.GetOrdinal("id"));
                                string cardName = cardReader.GetString(cardReader.GetOrdinal("name"));
                                var card = new Card(connection) { id = cardId, name = cardName };

                                cards.Add(card);
                            }
                        }

                        // Create a Deck object and return it
                        var deck = new Deck
                        {
                            Id = deckId,
                            Name = deckName,
                            Cards = cards
                        };

                        return deck;
                    }
                    else
                    {
                        // No deck found for the user, return an empty or null deck
                        return new Deck { Cards = new List<Card>() }; // Or handle as appropriate
                    }
                }
            }
        }
    }

}
