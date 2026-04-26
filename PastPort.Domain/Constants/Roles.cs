namespace PastPort.Domain.Constants;

/// <summary>
/// Defines the authorization role constants used throughout the PastPort platform.
/// Roles are seeded into ASP.NET Core Identity during application startup and
/// used in <c>[Authorize(Roles = "...")]</c> attributes for endpoint protection.
/// </summary>
public static class Roles
{
    /// <summary>Platform administrator with full system access.</summary>
    public const string Admin = "Admin";

    /// <summary>Educational institution account with scene access.</summary>
    public const string School = "School";

    /// <summary>Museum or cultural institution account.</summary>
    public const string Museum = "Museum";

    /// <summary>Enterprise or corporate account with premium features.</summary>
    public const string Enterprise = "Enterprise";

    /// <summary>Individual consumer account.</summary>
    public const string Individual = "Individual";

    /// <summary>Read-only list of all defined roles, used for seeding and validation.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Admin, School, Museum, Enterprise, Individual };
}
