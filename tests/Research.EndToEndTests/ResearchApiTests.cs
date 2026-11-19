using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Research.EndToEndTests;

/// <summary>The public API through the real topology. None of these need Claude.</summary>
public sealed class ResearchApiTests(AppHostFixture app)
{
    [Fact]
    public async Task Create_WithoutQuestion_IsValidationProblem()
    {
        using var response = await app.Jobs.PostAsJsonAsync("/research", new { question = "" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await ReadAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("research.validation_failed");
        problem.GetProperty("errors").TryGetProperty("question", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_UnknownJob_IsNotFoundProblem()
    {
        using var response = await app.Jobs.GetAsync($"/research/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadAsync(response)).GetProperty("code").GetString().ShouldBe("jobs.job_not_found");
    }

    [Fact]
    public async Task Create_IsIdempotentPerKey()
    {
        var key = "e2e-" + Guid.NewGuid().ToString("N");
        var first = await CreateAsync("What is the capital of the Netherlands and why?", key);
        var again = await CreateAsync("What is the capital of the Netherlands and why?", key);

        again.GetProperty("id").GetGuid().ShouldBe(first.GetProperty("id").GetGuid());

        using var conflict = await PostWithKeyAsync("A different question for the same key?", key);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadAsync(conflict)).GetProperty("code").GetString().ShouldBe("jobs.idempotency_conflict");
    }

    [Fact]
    public async Task Cancel_ActiveJob_IsTerminal_AndHasNoReport()
    {
        var job = await CreateAsync("How do quorum queues in RabbitMQ replicate messages?", key: null);
        var id = job.GetProperty("id").GetGuid();

        using var cancel = await app.Jobs.PostAsync($"/research/{id}/cancel", content: null, TestContext.Current.CancellationToken);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(cancel)).GetProperty("status").GetString().ShouldBe("Cancelled");

        using var report = await app.Jobs.GetAsync($"/research/{id}/report", TestContext.Current.CancellationToken);
        report.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadAsync(report)).GetProperty("code").GetString().ShouldBe("jobs.report_not_available");

        using var again = await app.Jobs.PostAsync($"/research/{id}/cancel", content: null, TestContext.Current.CancellationToken);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Events_StreamsTheCurrentState()
    {
        var job = await CreateAsync("What limits the throughput of a Postgres outbox?", key: null);
        var id = job.GetProperty("id").GetGuid();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/research/{id}/events");
        using var response = await app.Jobs.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        string? line;
        while ((line = await reader.ReadLineAsync(timeout.Token)) is not null && !line.StartsWith("data:", StringComparison.Ordinal))
        {
        }

        line.ShouldNotBeNull();
        using var data = JsonDocument.Parse(line["data:".Length..]);
        data.RootElement.GetProperty("id").GetGuid().ShouldBe(id);
    }

    private async Task<JsonElement> CreateAsync(string question, string? key)
    {
        using var response = await PostWithKeyAsync(question, key);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location.ShouldNotBeNull();
        return await ReadAsync(response);
    }

    private Task<HttpResponseMessage> PostWithKeyAsync(string question, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/research") { Content = JsonContent.Create(new { question }) };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return app.Jobs.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }
}
