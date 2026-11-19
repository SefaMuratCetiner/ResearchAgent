using Research.Hosting.Composition;
using Research.Messaging;
using Research.Providers.RabbitMq;
using Research.SharedKernel.Logging;

namespace Research.Knowledge.Host.CompositionRoot;

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
