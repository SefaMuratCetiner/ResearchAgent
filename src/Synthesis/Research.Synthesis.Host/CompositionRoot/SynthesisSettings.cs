using Research.Hosting.Composition;
using Research.Messaging;
using Research.Providers.Anthropic;
using Research.Providers.RabbitMq;
using Research.SharedKernel.Logging;

namespace Research.Synthesis.Host.CompositionRoot;

/// <summary>Every options section the Synthesis service reads, bound and validated once at composition time.</summary>
internal sealed class SynthesisSettings
{
    public SynthesisSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Synthesis = binder.Add(SynthesisOptions.SectionName, new SynthesisOptionsValidator());
        Anthropic = binder.Add(AnthropicOptions.SectionName, new AnthropicOptionsValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public SynthesisOptions Synthesis { get; }

    public AnthropicOptions Anthropic { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
