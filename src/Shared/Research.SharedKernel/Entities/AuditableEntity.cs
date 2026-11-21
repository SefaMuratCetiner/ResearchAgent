namespace Research.SharedKernel.Entities;

/// <summary>
/// An entity with its audit trail. Persistence repositories populate <see cref="CreatedAt"/>, <see cref="CreatedBy"/>,
/// <see cref="UpdatedAt"/> and <see cref="UpdatedBy"/> automatically from the active unit of work when not already set.
/// </summary>
public abstract record AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}
