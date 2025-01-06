using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Deck
    {
        public Guid Id { get; set; }
        public Deck(string name)
        {
            Name = name;
            PlayerStack = new List<Card>();
        }

        public string Name { get; set; }
        public List<Card> PlayerStack { get; set; }
    }
}
