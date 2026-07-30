using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Results.AspNetCore.Tests;

public sealed class ResultHttpExtensionsTests
{
    [Test]
    [Arguments(ErrorCategory.Invalid, 400)]
    [Arguments(ErrorCategory.Unauthorized, 401)]
    [Arguments(ErrorCategory.Forbidden, 403)]
    [Arguments(ErrorCategory.NotFound, 404)]
    [Arguments(ErrorCategory.Conflict, 409)]
    [Arguments(ErrorCategory.Unavailable, 503)]
    public async Task Each_Category_Maps_To_Its_Status_Code(ErrorCategory category, int expected)
    {
        await Assert.That(category.ToStatusCode()).IsEqualTo(expected);
    }

    [Test]
    public async Task Every_Category_Is_Mapped()
    {
        // Guards against the set growing without this being updated: the mapping throws rather than
        // falling back to 500, so an unmapped category would surface here instead of in production.
        foreach (ErrorCategory category in Enum.GetValues<ErrorCategory>())
        {
            await Assert.That(() => category.ToStatusCode()).ThrowsNothing();
        }
    }

    [Test]
    public async Task An_Unknown_Category_Is_Refused_Rather_Than_Defaulted()
    {
        await Assert.That(() => ((ErrorCategory)99).ToStatusCode())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task An_Error_Becomes_A_Problem_Carrying_Its_Code()
    {
        Error error = Errors.Conflict("orders.already_shipped", "The order has already shipped.");

        ProblemDetails problem = error.ToProblemDetails();

        await Assert.That(problem.Status).IsEqualTo(409);
        await Assert.That(problem.Title).IsEqualTo("Conflict");
        await Assert.That(problem.Detail).IsEqualTo("The order has already shipped.");
        await Assert.That(problem.Extensions[ResultHttpExtensions.CodeMember]).IsEqualTo("orders.already_shipped");
    }

    [Test]
    public async Task A_Problem_Can_Be_Changed_Before_Being_Returned()
    {
        // This is the escape hatch that replaces an options type.
        ProblemDetails problem = Errors.NotFound("orders.not_found", "No such order.").ToProblemDetails();

        problem.Title = "Nowhere to be found";
        problem.Extensions["shard"] = 7;

        await Assert.That(problem.Title).IsEqualTo("Nowhere to be found");
        await Assert.That(problem.Status).IsEqualTo(404);
    }

    [Test]
    public async Task A_Successful_Outcome_With_No_Value_Answers_No_Content()
    {
        HttpResponseCapture response = await Execute(Result.Success.ToHttpResult());

        await Assert.That(response.StatusCode).IsEqualTo(204);
    }

    [Test]
    public async Task A_Successful_Outcome_With_A_Value_Answers_The_Value()
    {
        Result<string> result = "loom";

        HttpResponseCapture response = await Execute(result.ToHttpResult());

        await Assert.That(response.StatusCode).IsEqualTo(200);
        await Assert.That(response.Body).IsEqualTo("\"loom\"");
    }

    [Test]
    public async Task A_Successful_Outcome_Can_Answer_Something_Other_Than_Two_Hundred()
    {
        Result<string> result = "loom";

        HttpResponseCapture response = await Execute(
            result.ToHttpResult(value => TypedResults.Created($"/things/{value}", value)));

        await Assert.That(response.StatusCode).IsEqualTo(201);
    }

    [Test]
    public async Task A_Failure_Answers_A_Problem()
    {
        Result<string> result = Errors.NotFound("orders.not_found", "No such order.");

        HttpResponseCapture response = await Execute(result.ToHttpResult());

        await Assert.That(response.StatusCode).IsEqualTo(404);

        JsonElement body = JsonDocument.Parse(response.Body).RootElement;
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo("orders.not_found");
        await Assert.That(body.GetProperty("title").GetString()).IsEqualTo("Not found");
    }

    [Test]
    public async Task A_Failure_With_No_Value_Answers_A_Problem()
    {
        Result result = Errors.Unavailable("carrier.down", "The carrier is unreachable.");

        HttpResponseCapture response = await Execute(result.ToHttpResult());

        await Assert.That(response.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task A_Validation_Failure_Answers_Its_Field_Messages()
    {
        Result<string> result = new ValidationError(
            "request.invalid",
            "The request is invalid.",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Sku"] = ["Required.", "Too long."],
                ["Amount"] = ["Must be positive."],
            });

        HttpResponseCapture response = await Execute(result.ToHttpResult());

        await Assert.That(response.StatusCode).IsEqualTo(400);

        JsonElement body = JsonDocument.Parse(response.Body).RootElement;
        JsonElement errors = body.GetProperty("errors");

        // The metadata shape is already the errors shape, so nothing translated it.
        await Assert.That(errors.GetProperty("Sku").GetArrayLength()).IsEqualTo(2);
        await Assert.That(errors.GetProperty("Amount").GetArrayLength()).IsEqualTo(1);
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo("request.invalid");
    }

    [Test]
    public async Task The_Frameworks_Own_Customisation_Hook_Applies()
    {
        // Settled by test rather than assumption: if this runs, a trace identifier and anything else a
        // consumer wants comes from the framework's hook and this package should stay out of it.
        HttpResponseCapture response = await Execute(
            Result.Failure(Errors.NotFound("a.b", "Gone.")).ToHttpResult(),
            services => services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                    context.ProblemDetails.Extensions["traceId"] = "trace-1"));

        JsonElement body = JsonDocument.Parse(response.Body).RootElement;

        await Assert.That(body.TryGetProperty("traceId", out JsonElement trace)).IsTrue();
        await Assert.That(trace.GetString()).IsEqualTo("trace-1");
    }

    private static async Task<HttpResponseCapture> Execute(
        IResult result,
        Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        configure?.Invoke(services);

        await using ServiceProvider provider = services.BuildServiceProvider();

        MemoryStream body = new();
        DefaultHttpContext context = new() { RequestServices = provider };
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        using StreamReader reader = new(body);

        return new HttpResponseCapture(context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed record HttpResponseCapture(int StatusCode, string Body);
}
