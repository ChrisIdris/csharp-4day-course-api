using System.ComponentModel.DataAnnotations;

namespace AuthLesson2.Dtos;

// Login doesn't enforce length rules on the password. The rules are for *creating*
// a password, not for *comparing* one — and we don't want a bad-shape login to
// reveal password policy by looking different from an unknown-email login.
public record LoginRequest(
    [Required] [EmailAddress] string Email,
    [Required] string Password
);
