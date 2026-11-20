using Research.ServiceDefaults.Composition;
using Research.Shared.Adapters.Messaging;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Logging;

namespace Research.Fetching.Bootstrap.CompositionRoot;

/// <summary>Every options section the Fetching service reads, bound and validated once at composition time.</summary>
internal sealed class FetchingSettings
{
    public FetchingSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Fetching = binder.Add(FetchingOptions.SectionName, new FetchingOptionsValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public FetchingOptions Fetching { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
