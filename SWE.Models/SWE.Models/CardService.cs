using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class CardService
    {
        private List<Card> _cards;

        public CardService()
        {
            // Initialize an empty list or load from database
            _cards = new List<Card>();
        }

        // Adds a new card to the collection
        public void AddCard(Card card)
        {
            if (_cards.Any(c => c.id == card.id))
            {
                throw new InvalidOperationException("Card with this ID already exists.");
            }

            _cards.Add(card);
        }

        // Retrieves all cards
        public List<Card> GetAllCards()
        {
            return _cards;
        }

        // Retrieves a card by its ID
        public Card GetCardById(Guid cardId)
        {
            var card = _cards.FirstOrDefault(c => c.id == cardId);
            if (card == null)
            {
                throw new KeyNotFoundException("Card not found.");
            }
            return card;
        }

        // Updates an existing card
        public void UpdateCard(Card updatedCard)
        {
            var existingCard = _cards.FirstOrDefault(c => c.id == updatedCard.id);
            if (existingCard == null)
            {
                throw new KeyNotFoundException("Card not found.");
            }

            // Update fields as needed
            existingCard.name = updatedCard.name;
            existingCard.damage = updatedCard.damage;
        }

        // Deletes a card by ID
        public void DeleteCard(Guid cardId)
        {
            var card = _cards.FirstOrDefault(c => c.id == cardId);
            if (card == null)
            {
                throw new KeyNotFoundException("Card not found.");
            }

            _cards.Remove(card);
        }

        public async Task SaveCardsAsync(List<Card> cards)
        {
            // Example: simulate saving data with a delay
            await Task.Delay(500); // Simulating an async operation (e.g., saving to a database)

            // Assuming you are saving the cards to a persistent storage (e.g., a database or file system)
            // Here, just adding them to the list for demonstration
            foreach (var card in cards)
            {
                // Simulate saving each card (e.g., to a database)
                // In a real scenario, you would use an ORM or file system I/O here
                _cards.Add(card);
            }

            // Simulate success or error, for example, returning the count of saved cards
            Console.WriteLine($"{cards.Count} cards saved successfully!");
        }
    }
}
