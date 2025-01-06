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
    public class User
    {
        public int id { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string token { get; set; }
        public string? image { get; set; }
        public int coins { get; set; }
        public int wins { get; set; }
        public int losses { get; set; }
        public string? bio { get; set; }
        public int elo { get; set; }
    }
}
