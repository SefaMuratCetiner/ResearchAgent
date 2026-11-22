using System.Text.RegularExpressions;

namespace Research.Shared.Core.Security;

/// <summary>
/// Heuristics that flag text which looks written to steer a model: "ignore previous instructions", chat role markers,
/// text addressed to an AI, and Markdown images that carry query data (a common exfiltration trick). They are signals
/// for logs, metrics and an optional reject switch, not a defence on their own: the defence is
/// <see cref="PromptFence"/> plus output checks. False positives are expected (a page about prompt injection matches).
/// </summary>
public static partial class PromptInjectionSignals
{
    public const string IgnoreInstructions = "ignore_instructions";
    public const string RoleMarker = "role_marker";
    public const string AddressedToModel = "addressed_to_model";
    public const string ImageExfiltration = "image_exfiltration";

    /// <summary>The names of the signals found in <paramref name="text"/>, in a fixed order; empty when none.</summary>
    public static IReadOnlyList<string> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var found = new List<string>(4);
        Check(found, IgnoreInstructions, IgnoreInstructionsPattern(), text);
        Check(found, RoleMarker, RoleMarkerPattern(), text);
        Check(found, AddressedToModel, AddressedToModelPattern(), text);
        Check(found, ImageExfiltration, ImageExfiltrationPattern(), text);
        return found;
    }

    private static void Check(List<string> found, string name, Regex pattern, string text)
    {
        try
        {
            if (pattern.IsMatch(text))
            {
                found.Add(name);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A pathological page: no signal rather than a stuck consumer.
        }
    }

    [GeneratedRegex(
        @"\b(ignore|disregard|forget|override|bypass)\b[^.\n]{0,40}\b(previous|prior|above|earlier|all|any|the|your|system)\b[^.\n]{0,40}\b(instructions?|prompts?|rules|guidelines|directions)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex IgnoreInstructionsPattern();

    [GeneratedRegex(
        @"(^|\n)\s*(system|assistant|developer)\s*:|<\|?(im_start|im_end|system|endoftext)\|?>|\[/?INST\]|</?(system|instructions?)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex RoleMarkerPattern();

    [GeneratedRegex(
        @"\b(if you are|you are now|attention|note to|message to|instructions? (for|to))\b[^.\n]{0,20}\b(an? )?(ai|llm|language model|assistant|chatbot|agent|claude|gpt)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex AddressedToModelPattern();

    [GeneratedRegex(
        @"!\[[^\]\n]{0,200}\]\(\s*https?://[^)\s]{0,500}[?&][^)\s=]{1,50}=",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex ImageExfiltrationPattern();
}
