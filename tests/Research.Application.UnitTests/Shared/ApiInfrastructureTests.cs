using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Research.ServiceDefaults.Endpoints;
using Research.ServiceDefaults.Exceptions;
using Research.ServiceDefaults.Problems;
using Research.SharedKernel.Results;

namespace Research.Application.UnitTests.Shared;

public sealed class ApiInfrastructureTests
{
    [Fact]
    public void ProblemResults_From_ReturnsStandardProblemDetailsWithCodeAndTraceId()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Request.Path = "/research/42";
        var error = new Error("jobs.job_not_found", ErrorKind.NotFound, "Not found.");

        var result = ProblemResults.From(error, context, "Missing job");

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Type.ShouldBe("https://docs.research.local/errors/jobs.job_not_found");
        problem.ProblemDetails.Title.ShouldBe("Not found.");
        problem.ProblemDetails.Detail.ShouldBe("Missing job");
        problem.ProblemDetails.Instance.ShouldBe("/research/42");
        problem.ProblemDetails.Extensions["code"].ShouldBe("jobs.job_not_found");
        problem.ProblemDetails.Extensions["traceId"].ShouldNotBeNull();
    }

    [Fact]
    public void ProblemResults_Validation_ReturnsValidationProblemDetailsWithFieldErrors()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-456" };
        context.Request.Path = "/research";
        var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["question"] = ["The question is required."],
        };

        var result = ProblemResults.Validation(fieldErrors, context);

        var problem = result.ShouldBeOfType<ValidationProblem>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Errors["question"].ShouldBe(["The question is required."]);
        problem.ProblemDetails.Extensions["code"].ShouldBe("research.validation_failed");
    }

    [Fact]
    public async Task CustomExceptionHandler_Writes500ProblemDetails()
    {
        var writer = new RecordingProblemDetailsService();
        var handler = new CustomExceptionHandler(writer, NullLogger<CustomExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/research/boom";

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        writer.Last.ShouldNotBeNull();
        writer.Last!.ProblemDetails.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        writer.Last.ProblemDetails.Type.ShouldBe("https://docs.research.local/errors/research.unexpected");
        writer.Last.ProblemDetails.Extensions["code"].ShouldBe("research.unexpected");
    }

    [Fact]
    public async Task CustomExceptionHandler_IgnoresClientDisconnect()
    {
        var writer = new RecordingProblemDetailsService();
        var handler = new CustomExceptionHandler(writer, NullLogger<CustomExceptionHandler>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(cts.Token), cts.Token);

        handled.ShouldBeTrue();
        writer.Last.ShouldBeNull();
    }

    [Fact]
    public void EndpointGroupExtensions_DiscoversAndMapsIEndpointGroup()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapEndpoints<SampleEndpointGroup>();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static e => e.RoutePattern.RawText)
            .ToList();
        routes.ShouldContain("/sample/ping");
    }

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Last { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Last = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Last = context;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class SampleEndpointGroup : IEndpointGroup
    {
        public static string RoutePrefix => "/sample";

        public static string Tag => "Sample";

        public static void Map(RouteGroupBuilder group) =>
            group.MapGet("/ping", static () => TypedResults.Ok("pong"));
    }
}
