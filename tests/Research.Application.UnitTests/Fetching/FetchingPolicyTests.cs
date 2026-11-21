using Research.Fetching;

namespace Research.Application.UnitTests.Fetching;

public sealed class FetchingOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid() =>
        new FetchingOptionsValidator().Validate(null, new FetchingOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void MinAboveMax_Fails() =>
        new FetchingOptionsValidator().Validate(null, new FetchingOptions { MinContentChars = 10, MaxContentChars = 5 })
            .Failed.ShouldBeTrue();
}
