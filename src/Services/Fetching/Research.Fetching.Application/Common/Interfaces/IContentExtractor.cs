namespace Research.Fetching.Application.Common.Interfaces;

/// <summary>The readable text of a page and its title.</summary>
public sealed record ExtractedContent(string Title, string Text);

/// <summary>Extracts readable text and title from a downloaded HTML or plain-text document.</summary>
public interface IContentExtractor
{
    ExtractedContent Extract(byte[] body, string mediaType, string? charset, string fallbackTitle);
}
