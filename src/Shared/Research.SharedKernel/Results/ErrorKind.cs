namespace Research.SharedKernel.Results;

/// <summary>What kind of failure an error is. Modules know no HTTP; the Problems library turns the kind into a status.</summary>
public enum ErrorKind
{
    Invalid,
    NotFound,
    Conflict,
    Forbidden,
    Rule,

    /// <summary>The system is at capacity; the caller may retry later.</summary>
    Unavailable,
    Unexpected,
}
