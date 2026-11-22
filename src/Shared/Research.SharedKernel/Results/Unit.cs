using System.Runtime.InteropServices;

namespace Research.SharedKernel.Results;

/// <summary>Represents a void response for commands that do not return a value.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Unit
{
    public static readonly Unit Value;
}
