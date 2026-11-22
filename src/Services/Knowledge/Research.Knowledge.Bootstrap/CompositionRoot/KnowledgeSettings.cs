using Research.Knowledge.Application;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Messaging;
using Research.Shared.Core.Logging;

namespace Research.Knowledge.Bootstrap.CompositionRoot;

/// <summary>Every options section the Knowledge service reads, bound and validated once at composition time.</summary>
internal sealed class KnowledgeSettings
{
    public KnowledgeSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Knowledge = binder.Add(KnowledgeOptions.SectionName, new KnowledgeOptionsValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public KnowledgeOptions Knowledge { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
