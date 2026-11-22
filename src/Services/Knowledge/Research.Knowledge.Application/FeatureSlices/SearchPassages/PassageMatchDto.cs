using Research.Knowledge.Domain;

namespace Research.Knowledge.Application.SearchPassages;

/// <summary>Immutable projection of a matching passage returned across the Application boundary.</summary>
public sealed record PassageMatchDto(Guid PassageId, Guid DocumentId, string Url, string Title, string Text, double Score)
{
    public static PassageMatchDto From(PassageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return new PassageMatchDto(match.PassageId, match.DocumentId, match.Url, match.Title, match.Text, match.Score);
    }
}
