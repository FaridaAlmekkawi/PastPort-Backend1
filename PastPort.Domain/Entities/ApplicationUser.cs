using Microsoft.AspNetCore.Identity;

namespace PastPort.Domain.Entities;

/// <summary>
/// Represents an authenticated user in the PastPort platform.
/// Extends <see cref="IdentityUser"/> with profile metadata and
/// navigation properties for subscriptions and conversation history.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Gets or sets the user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the user's last login, if any.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Gets or sets whether the user's email address has been verified via a confirmation code.</summary>
    public bool IsEmailVerified { get; set; } = false; 

    /// <summary>Gets or sets the UTC timestamp when email verification was completed.</summary>
    public DateTime? EmailVerifiedAt { get; set; }     

    // Navigation Properties

    /// <summary>Gets or sets the collection of subscriptions associated with this user.</summary>
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    /// <summary>Gets or sets the collection of NPC conversations this user has participated in.</summary>
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}