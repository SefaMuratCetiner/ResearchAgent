namespace Research.Jobs.Application.Common.Models;

/// <summary>One numbered source cited by a finished report.</summary>
public sealed record ReportCitationDto(int Number, string Url, string Title);
