namespace Research.SharedKernel.Common;

/// <summary>An entity with creation and modification audit metadata.</summary>
public abstract class BaseAuditableEntity : BaseEntity
{
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
}
