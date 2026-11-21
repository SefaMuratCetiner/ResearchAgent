using Research.Providers.AnthropicWebSearch;
using Research.Providers.RabbitMq;
using Research.Search.Application;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Core.Logging;

namespace Research.Search.Bootstrap.CompositionRoot;

/// <summary>Every options section the Search service reads, bound and validated once at composition time.</summary>
internal sealed class SearchSettings
{
    public SearchSettings(IServiceCollection services, IConfiguration configuration)
    {
        var binder = new SettingsBinder(services, configuration);
        Search = binder.Add(SearchOptions.SectionName, new SearchOptionsValidator());
        WebSearch = binder.Add(AnthropicWebSearchOptions.SectionName, new AnthropicWebSearchOptionsValidator());
        Messaging = binder.Add(MessagingOptions.SectionName, new MessagingOptionsValidator());
        RabbitMq = binder.Add(RabbitMqOptions.SectionName, new RabbitMqOptionsValidator());
        ContentLogging = binder.Add<ContentLoggingOptions>(ContentLoggingOptions.SectionName);
    }

    public SearchOptions Search { get; }

    public AnthropicWebSearchOptions WebSearch { get; }

    public MessagingOptions Messaging { get; }

    public RabbitMqOptions RabbitMq { get; }

    public ContentLoggingOptions ContentLogging { get; }
}
