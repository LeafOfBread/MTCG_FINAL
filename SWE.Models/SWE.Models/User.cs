using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SWE.Models;

namespace SWE.Models
{
    /*public class User
    {
        public User() { }

        public User(string userName, string password, Guid id, int? elo, int? coins, int packages, Deck stack, Deck playingDeck, int wins, int losses, string token, Guid PlayingDeckId, bool isAdmin)
        {
            UserName = userName;
            password = password;
            id = id;
            elo = elo;
            coins = coins;
            packages = packages;
            Stack = stack;
            PlayingDeck = playingDeck;
            PlayingDeckId = PlayingDeckId;
            wins = wins;
            losses = losses;
            Token = token;
            isAdmin = isAdmin;
        }

        public string UserName { get; set; }
        public string password { get; set; }
        public Guid userid { get; set; }
        public int? elo { get; set; }
        public int? coins { get; set; }
        public int packages { get; set; }
        public Deck Stack { get; set; }
        public Deck PlayingDeck { get; set; }
        public int wins { get; set; }
        public int losses { get; set; }
        public string Token { get; set; }
        public Guid PlayingDeckId { get; set; }
        public bool isAdmin { get; set; }

        // Relationship: A User can have many owned Packages
        public ICollection<Package> OwnedPackages { get; set; }

        // TradeCard method as is
        public void TradeCard(User userOne, User userTwo, int userCardOne, int userCardTwo)
        {
            var cardFromUserTwo = userTwo.PlayingDeck.PlayerStack[userCardTwo];
            var cardFromUserOne = userOne.Stack.PlayerStack[userCardOne];

            userOne.Stack.PlayerStack.Add(cardFromUserTwo);
            userOne.Stack.PlayerStack.Remove(cardFromUserOne);

            userTwo.Stack.PlayerStack.Add(cardFromUserOne);
            userTwo.Stack.PlayerStack.Remove(cardFromUserTwo);
        }

        public bool isAdminUser()
        {
            return isAdmin;
        }
    }*/
}

