using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;



namespace SWE.Models
{
    public class TokenService
    {
        public bool ValidateToken(string token1, string token2)
        {
            return token1 == token2;
        }

    }

    public class AuthenticationResult
    {
        public bool IsAuthenticated { get; set; }
        public User? User { get; set; }
        public bool IsSuccess { get; set; }
        public string? Token { get; set; }


    }


}