using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Research.Fetching.Application;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Application.FetchPage;
using Research.Fetching.Infrastructure;
using Research.Jobs.Application;
using Research.Jobs.Application.CancelResearchJob;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Application.CreateResearchJob;
using Research.Jobs.Application.EnforceJobDeadlines;
using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Application.GetResearchReport;
using Research.Jobs.Application.RecoverStuckJobs;
using Research.Jobs.Domain.Events;
using Research.Knowledge.Application;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Application.IndexDocument;
using Research.Knowledge.Application.SearchPassages;
using Research.Knowledge.Infrastructure;
using Research.Planning.Application;
using Research.Planning.Application.CreatePlan;
using Research.Providers.AnthropicWebSearch;
using Research.Providers.MockEmbeddings;
using Research.Providers.OpenAI;
using Research.Providers.Postgres;
using Research.Search.Application;
using Research.Search.Application.RunSearch;
using Research.Search.Infrastructure;
using Research.Shared.Adapters.Composition;
using Research.Shared.Core.Events;
using Research.Shared.Core.Mediation;
using Research.Shared.Core.Transactions;
using Research.Shared.Messaging;
using Research.Shared.Ports.Embeddings;
using Research.Shared.Ports.Llm;
using Research.Shared.Ports.Search;
using Research.Shared.Kernel.Results;
using Research.Synthesis.Application;
using Research.Synthesis.Application.WriteReport;

namespace Research.Infrastructure.UnitTests.Composition;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void Providers_RegisterExpectedServicesAndHealthChecks()
    {
        var services = new ServiceCollection();
        services.AddPostgresProvider("test-schema", ["outbox_messages", "inbox_messages"]);
        services.AddMockEmbeddings();
        services.AddOpenAILlm();
        services.AddAnthropicWebSearch();

        services.ShouldContain(static d => d.ServiceType == typeof(IUnitOfWork) && d.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(static d => d.ServiceType == typeof(IOutbox) && d.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(static d => d.ServiceType == typeof(IInbox) && d.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(static d => d.ServiceType == typeof(IOutboxStore) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(static d => d.ServiceType == typeof(IEmbeddingGenerator) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(static d => d.ServiceType == typeof(ILlmClient) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(static d => d.ServiceType == typeof(ISearchProvider) && d.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        healthOptions.Registrations.ShouldContain(static r => r.Name == "test-schema" && r.Tags.Contains("ready"));
    }

    [Fact]
    public void JobsApplicationExtensions_RegisterApiAndWorkerSlicesIndependently()
    {
        var apiServices = new ServiceCollection();
        apiServices.AddJobsApiApplication();

        apiServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<CreateResearchJobCommand, Result<CreateResearchJobResult>>));
        apiServices.ShouldContain(static d => d.ServiceType == typeof(IRequestValidator<CreateResearchJobCommand>));
        apiServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<GetResearchJobQuery, Result<ResearchJobView>>));
        apiServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<GetResearchReportQuery, Result<ResearchReportDto>>));
        apiServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<CancelResearchJobCommand, Result<ResearchJobDto>>));
        apiServices.ShouldContain(static d => d.ServiceType == typeof(IDomainEventHandler<JobCreatedDomainEvent>));
        apiServices.ShouldNotContain(static d => d.ServiceType == typeof(StepReplyProcessor));
        apiServices.ShouldNotContain(static d => d.ServiceType == typeof(GatheringPhaseCompleter));

        var workerServices = new ServiceCollection();
        workerServices.AddJobsWorkerApplication();

        workerServices.ShouldContain(static d => d.ServiceType == typeof(StepReplyProcessor));
        workerServices.ShouldContain(static d => d.ServiceType == typeof(GatheringPhaseCompleter));
        workerServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<RecoverStuckJobsCommand, int>));
        workerServices.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<EnforceJobDeadlinesCommand, int>));
        workerServices.ShouldContain(static d => d.ServiceType == typeof(IDomainEventHandler<JobCompletedDomainEvent>));
        workerServices.ShouldNotContain(static d => d.ServiceType == typeof(IRequestHandler<CreateResearchJobCommand, Result<CreateResearchJobResult>>));
    }

    [Fact]
    public void ServiceLayerExtensions_RegisterHandlersAndInfrastructurePorts()
    {
        var services = new ServiceCollection();
        services.AddFetchingApplication();
        services.AddFetchingInfrastructure();
        services.AddKnowledgeApplication();
        services.AddKnowledgeInfrastructure();
        services.AddPlanningApplication();
        services.AddSynthesisApplication();
        services.AddAnthropicWebSearch();
        services.AddSearchApplication();
        services.AddSearchInfrastructure();

        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<FetchPageCommand, Result<FetchedPage>>));
        services.ShouldContain(static d => d.ServiceType == typeof(IPageDownloader));
        services.ShouldContain(static d => d.ServiceType == typeof(IContentExtractor));
        services.ShouldContain(static d => d.ServiceType == typeof(IFetchedDocumentRepository));

        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<PrepareDocumentCommand, Result<PreparedDocument>>));
        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<SearchPassagesQuery, Result<IReadOnlyList<PassageMatchDto>>>));
        services.ShouldContain(static d => d.ServiceType == typeof(IRequestValidator<SearchPassagesQuery>));
        services.ShouldContain(static d => d.ServiceType == typeof(IKnowledgeRepository));

        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<CreatePlanCommand, Result<ResearchPlan>>));
        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<RunSearchCommand, Result<SearchResponse>>));
        services.ShouldContain(static d => d.ServiceType == typeof(IRequestHandler<WriteReportCommand, Result<WrittenReport>>));
    }

    [Fact]
    public void Decorate_WrapsExistingRegistrationAndPreservesLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISampleService, SampleService>();
        services.Decorate<ISampleService>(static (_, inner) => new DecoratingSampleService(inner));

        using var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<ISampleService>();
        instance.Value.ShouldBe("decorated:base");
    }

    private interface ISampleService
    {
        string Value { get; }
    }

    private sealed class SampleService : ISampleService
    {
        public string Value => "base";
    }

    private sealed class DecoratingSampleService(ISampleService inner) : ISampleService
    {
        public string Value => $"decorated:{inner.Value}";
    }
}
