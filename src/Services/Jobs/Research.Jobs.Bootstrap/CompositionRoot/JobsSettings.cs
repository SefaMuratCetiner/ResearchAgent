using Research.Jobs.Application;
using Research.Shared.Core.Hosting;
using Research.Shared.Core.Logging;

namespace Research.Jobs.Bootstrap.CompositionRoot;

/// <summary>Every options section the Jobs API reads, bound and validated once at composition time.</summary>
internal sealed class JobsSettings
{
    public JobsSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Jobs = binder.Add(JobsOptions.SectionName, new JobsOptionsValidator(), new JobsOptionsConsistencyValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public JobsOptions Jobs { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
