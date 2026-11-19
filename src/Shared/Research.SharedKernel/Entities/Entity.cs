namespace Research.SharedKernel.Entities;

/// <summary>Anything stored with its own identity. Ids are UUID v7, so they sort by creation time.</summary>
public abstract record Entity
{
    public required Guid Id { get; init; }
}
