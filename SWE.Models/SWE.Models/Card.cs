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
        public double criticalStrikeChance { get; set; }
        public int winStreak { get; set; }

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
    }
}