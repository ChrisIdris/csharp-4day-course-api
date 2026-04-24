using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthLesson2.Models;
using Microsoft.IdentityModel.Tokens;

namespace AuthLesson2.Services;

// Builds and signs a JWT for an authenticated user. Takes IConfiguration through DI
// so the issuer/audience/signing-key/expiry all come from the same appsettings.json
// section the JwtBearer middleware reads — which is what makes the tokens we issue
// here pass validation on the next request.
//
// Stateless and thread-safe, so it's registered as a Singleton in Program.cs.
public class JwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public (string token, DateTime expiresAt) Generate(ApplicationUser user)
    {
        // 1) Build the signing key from the shared secret. HS256 = HMAC with SHA-256:
        // symmetric, one key signs AND validates. For multi-service setups you'd use
        // asymmetric RS256 (private key signs, public key validates).
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // 2) Claims — the facts about the user that travel in the token body.
        //    sub   : subject — who this token identifies. We use the Identity user Id.
        //    email : convenience, so controllers can read it without hitting the DB.
        //    jti   : token-unique id. Useful later if we add revocation; today, a
        //            fresh Guid per token.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var expires = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60"));

        // 3) Assemble and serialise. JwtSecurityTokenHandler.WriteToken returns the
        //    three base64-encoded pieces joined by "." — header.payload.signature.
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
