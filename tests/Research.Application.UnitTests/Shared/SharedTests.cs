using Research.Shared.Core.Logging;

namespace Research.Application.UnitTests.Shared;

public sealed class ContentTextTests
{
    [Fact]
    public void ShortValue_IsUnchanged() => ContentText.Truncate("abc", 10).ShouldBe("abc");

    [Fact]
    public void Null_IsEmpty() => ContentText.Truncate(null, 10).ShouldBe(string.Empty);

    [Fact]
    public void LongValue_IsCutWithMarker() => ContentText.Truncate("abcdefghij", 4).ShouldBe("abcd…[truncated 6 chars]");

    [Fact]
    public void NonPositiveLimit_MeansNoLimit() => ContentText.Truncate("abcdef", 0).ShouldBe("abcdef");
}
