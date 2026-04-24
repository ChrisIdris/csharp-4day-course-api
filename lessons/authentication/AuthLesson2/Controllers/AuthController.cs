using AuthLesson2.Dtos;
using AuthLesson2.Models;
using AuthLesson2.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthLesson2.Controllers;

// No [Authorize] at the class level — you can't require a token to *get* a token.
// This controller is anonymous by design.
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    // POST /api/auth/register
    //
    // Creates the user, hashes the password via UserManager, auto-logs-in by
    // returning a freshly-issued JWT. Returning the token saves the client a
    // second round-trip — common API convention; email-verification flows
    // would change this.
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Pre-check for email uniqueness so we can return a friendly 409 instead of
        // letting UserManager surface "DuplicateUserName" / "DuplicateEmail" errors.
        // (Identity's RequireUniqueEmail = true would also catch this at CreateAsync.)
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Problem(
                detail: "An account with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        // CreateAsync does three things: hashes the password via PasswordHasher,
        // validates against the password rules configured in Program.cs, and writes
        // the AspNetUsers row. An IdentityResult carries any rule violations back.
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Funnel Identity errors into the same 400 shape our data-annotation
            // failures produce in Extra Lesson 2 — clients parse one error format.
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(err.Code, err.Description);
            }
            return ValidationProblem(ModelState);
        }

        var (token, expiresAt) = _tokenGenerator.Generate(user);
        return new AuthResponse(token, expiresAt);
    }

    // POST /api/auth/login
    //
    // Two-step verification: find the user, check the password. Both failure
    // paths return the SAME 401 with the SAME message. Distinguishing "no such
    // email" from "wrong password" would let an attacker enumerate which
    // addresses are registered.
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // CheckPasswordAsync hashes the provided password with the same algorithm
        // and salt and compares constant-time. We never touch the hash directly.
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = _tokenGenerator.Generate(user);
        return new AuthResponse(token, expiresAt);
    }
}
