using System.ComponentModel.DataAnnotations;

namespace AuthLesson3.Dtos;

public record RegisterRequest(
    [Required] [EmailAddress] string Email,
    [Required] [StringLength(100, MinimumLength = 6)] string Password
);
