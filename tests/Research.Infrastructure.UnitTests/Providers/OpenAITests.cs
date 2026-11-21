using OpenAI.Chat;
using Research.Providers.OpenAI;
using Research.Shared.Adapters.Llm;
using Research.Shared.Core.Llm;

// ChatReasoningEffortLevel is marked evaluation-only by the SDK (see Research.Providers.OpenAI.csproj).
#pragma warning disable OPENAI001

namespace Research.Infrastructure.UnitTests.Providers;

public sealed class OpenAIStopReasonTests
{
    [Theory]
    [InlineData(ChatFinishReason.Stop, LlmStopReason.Completed)]
    [InlineData(ChatFinishReason.Length, LlmStopReason.MaxTokens)]
    [InlineData(ChatFinishReason.ContentFilter, LlmStopReason.Refused)]
    [InlineData(ChatFinishReason.ToolCalls, LlmStopReason.Other)]
    public void FinishReason_IsMapped(ChatFinishReason finishReason, LlmStopReason expected) =>
        OpenAILlmClient.MapStopReason(finishReason, refusal: null).ShouldBe(expected);

    [Fact]
    public void RefusalText_IsARefusal_EvenWhenTheModelStopped() =>
        OpenAILlmClient.MapStopReason(ChatFinishReason.Stop, "I can't help with that.").ShouldBe(LlmStopReason.Refused);
}

public sealed class OpenAIRequestTests
{
    [Theory]
    [InlineData(LlmEffort.Low, "low")]
    [InlineData(LlmEffort.Medium, "medium")]
    [InlineData(LlmEffort.High, "high")]
    [InlineData(LlmEffort.Max, "high")]
    public void Effort_IsMapped(LlmEffort effort, string expected) =>
        OpenAILlmClient.MapEffort(effort).ToString().ShouldBe(expected);

    [Fact]
    public void Options_CarryTokensEffortAndSchema()
    {
        var options = OpenAILlmClient.CreateOptions(Request(schema: """{ "type": "object" }"""), useReasoningEffort: true);

        options.MaxOutputTokenCount.ShouldBe(4000);
        options.ReasoningEffortLevel.ShouldBe(ChatReasoningEffortLevel.Medium);
        options.ResponseFormat.ShouldNotBeNull();
    }

    [Fact]
    public void Options_WithoutReasoningEffortOrSchema_LeaveThemUnset()
    {
        var options = OpenAILlmClient.CreateOptions(Request(schema: null), useReasoningEffort: false);

        options.ReasoningEffortLevel.ShouldBeNull();
        options.ResponseFormat.ShouldBeNull();
    }

    private static LlmRequest Request(string? schema) =>
        new("plan", "gpt-5", LlmEffort.Medium, "system", "user", 4000, schema);
}

public sealed class OpenAIOptionsValidatorTests
{
    [Fact]
    public void MissingKey_Fails() =>
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions()).Failed.ShouldBeTrue();

    [Fact]
    public void Failure_NeverContainsTheKey()
    {
        var result = new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { ApiKey = "sk-secret", MaxConcurrentRequests = 0 });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotContain("sk-secret");
    }

    [Fact]
    public void KeyAndLimit_Pass() =>
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { ApiKey = "sk-test" }).Succeeded.ShouldBeTrue();
}
