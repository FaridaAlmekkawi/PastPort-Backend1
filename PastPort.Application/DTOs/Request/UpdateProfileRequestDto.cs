using System.ComponentModel.DataAnnotations;

namespace PastPort.Application.DTOs.Request;

public class UpdateProfileRequestDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? ProfileImageUrl { get; set; }

    public bool CameraEnabled { get; set; }

    public bool LocationEnabled { get; set; }

    public bool MicrophoneEnabled { get; set; }
}