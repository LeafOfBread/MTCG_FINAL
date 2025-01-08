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
            _cards = new List<Card>();
        }

        public void AddCard(Card card)
        {
            if (_cards.Any(c => c.id == card.id))
            {
                throw new InvalidOperationException("Card with this ID already exists.");
            }

            _cards.Add(card);
        }

        public List<Card> GetAllCards()
        {
            return _cards;
        }

        // Retrieves card by ID
        public Card GetCardById(Guid cardId)
        {
            var card = _cards.FirstOrDefault(c => c.id == cardId);
            if (card == null)
            {
                throw new KeyNotFoundException("Card not found.");
            }
            return card;
        }

        // Updates existing card
        public void UpdateCard(Card updatedCard)
        {
            var existingCard = _cards.FirstOrDefault(c => c.id == updatedCard.id);
            if (existingCard == null)
            {
                throw new KeyNotFoundException("Card not found.");
            }

            // Update fields
            existingCard.name = updatedCard.name;
            existingCard.damage = updatedCard.damage;
        }
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
            await Task.Delay(500);


            foreach (var card in cards)
            {
                _cards.Add(card);
            }
            Console.WriteLine($"{cards.Count} cards saved successfully!");
        }
    }
}