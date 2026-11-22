namespace Research.Jobs.Api.Common.Models;

/// <summary>Where to go next.</summary>
public sealed record ResearchJobLinks(string Self, string Report, string Events, string Cancel)
{
    public static ResearchJobLinks For(Guid id)
    {
        var self = "/research/" + id.ToString("D");
        return new ResearchJobLinks(self, self + "/report", self + "/events", self + "/cancel");
    }
}
