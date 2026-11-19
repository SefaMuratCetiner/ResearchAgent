using Research.Hosting.Composition;
using Research.Messaging;
using Research.Providers.RabbitMq;
using Research.SharedKernel.Logging;

namespace Research.Jobs.Host.CompositionRoot;

/// <summary>Every options section the Jobs service reads, bound and validated once at composition time.</summary>
internal sealed class JobsSettings
{
    public JobsSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Jobs = binder.Add(JobsOptions.SectionName, new JobsOptionsValidator(), new JobsOptionsConsistencyValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public JobsOptions Jobs { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
