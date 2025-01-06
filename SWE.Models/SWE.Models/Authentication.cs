using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;


namespace SWE.Models
{
    public class TokenService
    {
        public bool ValidateToken(string token1, string token2)
        {
            return token1 == token2;
        }

        public string GenerateToken(User user)
        {
            var claims = new[]
            {
            new Claim(ClaimTypes.Name, user.username),
            // Add other claims (e.g., roles) as needed
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your_secret_key"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "your_issuer",
                audience: "your_audience",
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class AuthenticationResult
    {
        public bool IsAuthenticated { get; set; }
        public User User { get; set; }
        public bool IsSuccess { get; set; }
        public string Token { get; set; }


    }


}
