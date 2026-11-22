using Research.Shared.Core.Logging;

namespace Research.Application.UnitTests.Shared;

public sealed class ContentTextTests
{
    [Fact]
    public void ShortValue_IsUnchanged() => LogContentTruncator.Truncate("abc", 10).ShouldBe("abc");

    [Fact]
    public void Null_IsEmpty() => LogContentTruncator.Truncate(null, 10).ShouldBe(string.Empty);

    [Fact]
    public void LongValue_IsCutWithMarker() => LogContentTruncator.Truncate("abcdefghij", 4).ShouldBe("abcd…[truncated 6 chars]");

    [Fact]
    public void NonPositiveLimit_MeansNoLimit() => LogContentTruncator.Truncate("abcdef", 0).ShouldBe("abcdef");
}
