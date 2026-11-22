using System.Security.Cryptography;
using System.Text;

namespace Research.Jobs.Domain;

/// <summary>Step keys: deterministic, so duplicates collapse on the (job id, step key) unique key.</summary>
public static class JobStepKeys
{
    public const string Plan = "plan";
    public const string Synthesize = "synthesize";

    public static string Search(int index) => "search:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One fetch step per canonical URL per job: the same URL from two sub-questions is fetched once.</summary>
    public static string Fetch(string canonicalUrl)
    {
        ArgumentNullException.ThrowIfNull(canonicalUrl);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl));
        return "fetch:" + Convert.ToHexStringLower(hash.AsSpan(0, 12));
    }

    public static StepKind KindOf(string stepKey)
    {
        ArgumentNullException.ThrowIfNull(stepKey);
        if (string.Equals(stepKey, Plan, StringComparison.Ordinal))
        {
            return StepKind.Plan;
        }

        if (string.Equals(stepKey, Synthesize, StringComparison.Ordinal))
        {
            return StepKind.Synthesize;
        }

        return stepKey.StartsWith("search:", StringComparison.Ordinal) ? StepKind.Search : StepKind.Fetch;
    }
}
