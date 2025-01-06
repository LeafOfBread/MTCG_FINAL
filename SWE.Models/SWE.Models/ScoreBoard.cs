using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    internal class ScoreBoard
    {
        public ScoreBoard(int totalPlayers, int totalBattles, List<User> users)
        {
            TotalPlayers = totalPlayers;
            TotalBattles = totalBattles;
            Users = users;
        }

        public int TotalPlayers { get; set; }
        public int TotalBattles { get; set; }
        public List<User> Users { get; set; }
    }
}
