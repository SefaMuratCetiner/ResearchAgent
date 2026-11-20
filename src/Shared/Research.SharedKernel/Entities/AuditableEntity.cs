namespace Research.SharedKernel.Entities;

/// <summary>
/// An entity with its audit trail. Nothing fills these fields automatically: the handler sets them from
/// <see cref="TimeProvider"/> and the current actor, and the SQL stores them as ordinary columns.
/// </summary>
public abstract record AuditableEntity : Entity
{
    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatedBy { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}
