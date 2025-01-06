using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Sessions
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string token { get; set; }
        public DateTime? expires { get; set; }
        public DateTime created { get; set; }

        public User user { get; set; }
    }
}
