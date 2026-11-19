using Microsoft.Extensions.Options;
using Research.Hosting.Composition;
using Research.Messaging;
using Research.Providers.Anthropic;
using Research.Providers.OpenAI;
using Research.Providers.RabbitMq;
using Research.SharedKernel.Llm;
using Research.SharedKernel.Logging;

namespace Research.Synthesis.Host.CompositionRoot;

/// <summary>Every options section the Synthesis service reads, bound and validated once at composition time.</summary>
internal sealed class SynthesisSettings
{
    public SynthesisSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Synthesis = binder.Add(SynthesisOptions.SectionName, new SynthesisOptionsValidator());
        Llm = binder.Add<LlmOptions>(LlmOptions.SectionName);

        // Only the selected provider's section is bound, so the other one's API key is not required.
        if (Llm.Provider == LlmProvider.OpenAI)
        {
            OpenAI = binder.Add(OpenAIOptions.SectionName, new OpenAIOptionsValidator());
            if (Synthesis.Model.StartsWith("claude-", StringComparison.Ordinal))
            {
                throw new OptionsValidationException(
                    SynthesisOptions.SectionName,
                    typeof(SynthesisOptions),
                    ["Synthesis:Model names a Claude model, but Llm:Provider is OpenAI: set an OpenAI model."]);
            }
        }
        else
        {
            Anthropic = binder.Add(AnthropicOptions.SectionName, new AnthropicOptionsValidator());
        }

        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public SynthesisOptions Synthesis { get; }

    public LlmOptions Llm { get; }

    /// <summary>Bound when <see cref="Llm"/> selects Anthropic.</summary>
    public AnthropicOptions? Anthropic { get; }

    /// <summary>Bound when <see cref="Llm"/> selects OpenAI.</summary>
    public OpenAIOptions? OpenAI { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
