using Research.Jobs.Application;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Messaging;
using Research.Shared.Core.Logging;

namespace Research.Jobs.Worker.CompositionRoot;

/// <summary>Every options section the Jobs Worker reads, bound and validated once at composition time.</summary>
internal sealed class JobsWorkerSettings
{
    public JobsWorkerSettings(IServiceCollection services, IConfiguration configuration)
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
