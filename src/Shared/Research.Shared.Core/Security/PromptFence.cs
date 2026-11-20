using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Research.Shared.Core.Security;

/// <summary>
/// Puts untrusted text into a prompt inside an element whose name carries a random nonce, for example
/// <c>&lt;untrusted-3f9a1c0b7e2d kind="source" n="2"&gt;…&lt;/untrusted-3f9a1c0b7e2d&gt;</c> (OWASP ASI01, LLM01).
/// The text cannot close the element early, because it cannot know the nonce, and any copy of the element name inside
/// it is removed anyway. The system prompts tell the model that everything inside such an element is data, never
/// instructions. A fence is used for one request only.
/// </summary>
public sealed class PromptFence
{
    /// <summary>What the system prompts call the elements: the name without the nonce.</summary>
    public const string ElementPrefix = "untrusted-";

    public PromptFence()
        : this(RandomNumberGenerator.GetHexString(12, lowercase: true))
    {
    }

    /// <summary>A fixed nonce, for tests.</summary>
    public PromptFence(string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        Element = ElementPrefix + nonce;
    }

    /// <summary>The element name of this request, nonce included.</summary>
    public string Element { get; }

    /// <summary>
    /// Wraps <paramref name="content"/> (cleaned with <see cref="UntrustedText.Clean"/>). Attribute values are made
    /// single-line and stripped of quotes and angle brackets.
    /// </summary>
    public string Wrap(string kind, string? content, params (string Name, string? Value)[] attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(attributes);

        var builder = new StringBuilder();
        builder.Append('<').Append(Element).Append(" kind=\"").Append(Attribute(kind)).Append('"');
        foreach (var (name, value) in attributes)
        {
            builder.Append(' ').Append(name).Append("=\"").Append(Attribute(value)).Append('"');
        }

        builder.Append(">\n")
            .Append(UntrustedText.Clean(content).Replace(Element, ElementPrefix, StringComparison.OrdinalIgnoreCase).Trim())
            .Append("\n</").Append(Element).Append('>');
        return builder.ToString();
    }

    /// <summary>The integer form used for <c>n</c> attributes.</summary>
    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Attribute(string? value)
    {
        var line = UntrustedText.SingleLine(value);
        var builder = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            if (c is not ('"' or '<' or '>'))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
