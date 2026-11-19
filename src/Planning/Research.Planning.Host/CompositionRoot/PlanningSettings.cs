using Research.Hosting.Composition;
using Research.Messaging;
using Research.Providers.Anthropic;
using Research.Providers.RabbitMq;
using Research.SharedKernel.Logging;

namespace Research.Planning.Host.CompositionRoot;

/// <summary>Every options section the Planning service reads, bound and validated once at composition time.</summary>
internal sealed class PlanningSettings
{
    public PlanningSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Planning = binder.Add(PlanningOptions.SectionName, new PlanningOptionsValidator());
        Anthropic = binder.Add(AnthropicOptions.SectionName, new AnthropicOptionsValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public PlanningOptions Planning { get; }

    public AnthropicOptions Anthropic { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
