using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Research.EndToEndTests;

/// <summary>
/// One real research job: Claude plans, searches the web and writes the report. Runs only with ANTHROPIC_API_KEY set
/// (it costs tokens and needs internet access), so CI runs it on demand, not on every push.
/// </summary>
public sealed class ResearchFlowTests(AppHostFixture app)
{
    private static readonly string[] Terminal = ["Completed", "CompletedWithWarnings", "Failed", "Cancelled", "Expired"];

    [Fact]
    public async Task Question_EndsWithACitedReport()
    {
        Assert.SkipWhen(AppHostFixture.AnthropicApiKey is null, "ANTHROPIC_API_KEY is not set.");
        var ct = TestContext.Current.CancellationToken;

        using var created = await app.Jobs.PostAsJsonAsync(
            "/research",
            new { question = "What are the trade-offs between HNSW and IVFFlat indexes in pgvector?" },
            ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var id = (await ReadAsync(created, ct)).GetProperty("id").GetGuid();

        // The job deadline is 5 minutes; allow a little more for the deadline worker.
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(6);
        JsonElement job;
        do
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            using var response = await app.Jobs.GetAsync($"/research/{id}", ct);
            job = await ReadAsync(response, ct);
        }
        while (!Terminal.Contains(job.GetProperty("status").GetString()) && DateTime.UtcNow < deadline);

        var status = job.GetProperty("status").GetString();
        status.ShouldBeOneOf(["Completed", "CompletedWithWarnings"], $"job ended {status} with {job}");
        job.GetProperty("plan").GetArrayLength().ShouldBeGreaterThan(0);

        using var reportResponse = await app.Jobs.GetAsync($"/research/{id}/report", ct);
        reportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await ReadAsync(reportResponse, ct);
        report.GetProperty("content").GetString().ShouldNotBeNullOrWhiteSpace();
        report.GetProperty("citations").GetArrayLength().ShouldBeGreaterThan(0);
        foreach (var citation in report.GetProperty("citations").EnumerateArray())
        {
            report.GetProperty("content").GetString()!.ShouldContain($"[{citation.GetProperty("number").GetInt32()}]");
        }
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }
}
