using Microsoft.AspNetCore.Identity;

namespace AuthLesson1.Models;

// Our user type, inheriting everything Identity already gives us:
//   Id (string GUID), UserName, NormalizedUserName, Email, NormalizedEmail,
//   EmailConfirmed, PasswordHash, SecurityStamp, PhoneNumber, LockoutEnd,
//   AccessFailedCount, ...etc.
//
// Empty body means we're not adding custom fields yet. Any per-user property
// (display name, avatar URL, timezone, …) goes here in a future lesson.
public class ApplicationUser : IdentityUser
{
}
