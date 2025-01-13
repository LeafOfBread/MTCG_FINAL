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
        public int Count { get; set; }

        public Deck()
        {
            Cards = new List<Card>();
            Count = Count;
        }


        public async Task<Deck> GetDeckForUser(User user)   //hole das Deck fuer den User
        {
            string connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var deckQuery = "SELECT * FROM user_deck WHERE user_id = @userId";  //hole alles aus decks wo user_id = userId
                var deckCommand = new NpgsqlCommand(deckQuery, connection);
                deckCommand.Parameters.AddWithValue("@userId", user.id);

                using (var reader = await deckCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        int deckId = reader.GetInt32(reader.GetOrdinal("id"));
                        string deckName = reader.GetString(reader.GetOrdinal("name"));

                        var cards = new List<Card>();
                        var cardQuery = "SELECT * FROM cards WHERE deck_id = @deckId";  //hole alles aus cards wo deck_id = deckId
                        var cardCommand = new NpgsqlCommand(cardQuery, connection);
                        cardCommand.Parameters.AddWithValue("@deckId", deckId);

                        using (var cardReader = await cardCommand.ExecuteReaderAsync())
                        {
                            while (await cardReader.ReadAsync())    //solange es noch karten gibt
                            {
                                Guid cardId = cardReader.GetGuid(cardReader.GetOrdinal("id"));
                                string cardName = cardReader.GetString(cardReader.GetOrdinal("name"));
                                var card = new Card(connection) { id = cardId, name = cardName };

                                cards.Add(card);
                            }
                        }

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
                        return new Deck { Cards = new List<Card>() }; 
                    }
                }
            }
        }
    }

}