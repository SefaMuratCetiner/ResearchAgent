namespace Research.SharedKernel.Logging;

/// <summary>
/// Logger categories that carry content: questions, prompts, LLM output, search results, page text, reports and API
/// bodies. Content is logged in full (up to <see cref="ContentLoggingOptions.MaxChars"/>); these categories exist so
/// the volume can be tuned per category in configuration. Credentials never reach any logger.
/// </summary>
public static class ContentLogCategories
{
    public const string Http = "Research.Content.Http";
    public const string Llm = "Research.Content.Llm";
    public const string Search = "Research.Content.Search";
    public const string Documents = "Research.Content.Documents";
    public const string Reports = "Research.Content.Reports";
}
