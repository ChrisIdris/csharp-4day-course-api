namespace AuthLesson2.Dtos;

// What both POST /api/auth/register and POST /api/auth/login return on success.
// Clients store the AccessToken and send it on subsequent requests as
// "Authorization: Bearer <token>". ExpiresAt lets the client refresh proactively
// (refresh tokens are a later lesson).
public record AuthResponse(string AccessToken, DateTime ExpiresAt);
