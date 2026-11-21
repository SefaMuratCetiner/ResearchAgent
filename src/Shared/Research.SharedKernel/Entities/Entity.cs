namespace Research.SharedKernel.Entities;

/// <summary>
/// A persisted domain entity with a UUID v7 identity and audit metadata. Persistence repositories populate
/// <see cref="CreatedAt"/>, <see cref="CreatedBy"/>, <see cref="UpdatedAt"/> and <see cref="UpdatedBy"/> automatically
/// from the active unit of work when not already set.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; }

    public DateTimeOffset CreatedAt { get; protected set; }

    public string CreatedBy { get; protected set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    public void StampCreated(DateTimeOffset at, string by)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        CreatedAt = at;
        CreatedBy = by;
    }

    public void StampUpdated(DateTimeOffset at, string by)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        UpdatedAt = at;
        UpdatedBy = by;
    }

    /// <summary>Restores audit metadata when rehydrating an existing row from persistence.</summary>
    public void RestoreAudit(DateTimeOffset createdAt, string createdBy, DateTimeOffset? updatedAt, string? updatedBy)
    {
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }
}
