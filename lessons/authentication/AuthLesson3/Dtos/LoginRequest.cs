using System.ComponentModel.DataAnnotations;

namespace AuthLesson3.Dtos;

public record LoginRequest(
    [Required] [EmailAddress] string Email,
    [Required] string Password
);
