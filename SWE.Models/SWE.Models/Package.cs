using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public class Package
    {
        public string id { get; set; }
        public string name { get; set; }
        public double damage { get; set; }

        private static readonly string host = "localhost";
        private static readonly string username = "postgres";
        private static readonly string password = "fhtw";
        private static readonly string database = "mtcg";

        private static readonly string connectionString = $"Host={host};Username={username};Password={password};Database={database};Port=5432";
        public (Package, int) createPackage(string Auth, List<Dictionary<string, object>> body)
        {
            // Extract token from Authorization header
            string inputToken = Auth.Replace("Bearer ", "").Trim();

            if (inputToken == "admin-mtcgToken")
            {
                // Check if the number of cards is correct
                if (body.Count != 5)
                {
                    Console.WriteLine("Not enough Cards");
                    return (null, 400); // Return 400 for bad request
                }

                Package package = new Package();
                foreach (var bodyElement in body)
                {
                    Card card = new Card();
                    // Here you would create and add the card to the package
                    // card = card.createCard(bodyElement["Id"].ToString(), bodyElement["Name"].ToString(),
                    //     float.Parse(bodyElement["Damage"].ToString(), CultureInfo.InvariantCulture));
                    // package.cardsInPack.Add(card);
                }
                return (package, 0); // Return 0 for success
            }
            return (null, 401); // Return 401 for unauthorized
        }




    }

    public class CreatePackageRequest
    {
        public List<Package> Cards { get; set; }
    }

}
