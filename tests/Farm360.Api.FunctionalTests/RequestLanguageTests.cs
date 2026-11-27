using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Farm360.Api.Configuration;
using Farm360.Api.Middleware;
using Farm360.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>
/// Accept-Language picks the language of error messages only: JSON numbers and dates, and the
/// formatting culture used by server code, are the same in Bangla and English.
/// </summary>
public sealed class RequestLanguageTests : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public RequestLanguageTests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFarm360Localization();
        _app = builder.Build();
        _app.UseFarm360RequestLocalization();
        _app.UseMiddleware<GlobalExceptionMiddleware>();

        _app.MapGet("/numbers", () => Results.Ok(new
        {
            Amount = 1234567.891m,
            Ratio = 0.75,
            Date = new DateOnly(2026, 11, 26),
            At = new DateTime(2026, 11, 26, 8, 30, 0, DateTimeKind.Utc),
            Formatted = 1234567.891m.ToString("N2", CultureInfo.CurrentCulture),
            FormattedDate = new DateTime(2026, 11, 26).ToString("dd MMM yyyy", CultureInfo.CurrentCulture),
        }));
        _app.MapGet("/shortage", IResult () =>
            throw new DomainException("Insufficient stock for 'Maize'. Required: 5 kg, Available: 1 kg."));
        _app.MapGet("/problem", () => Results.Problem(
            detail: "You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden));

        _app.StartAsync().GetAwaiter().GetResult();
        _client = _app.GetTestClient();
    }

    [Fact]
    public async Task NumbersAndDatesInJsonAreUnchangedInBangla()
    {
        var english = await GetAsync("/numbers", "en");
        var bangla = await GetAsync("/numbers", "bn");

        bangla.Body.Should().Be(english.Body);
        bangla.Body.Should().Contain("1234567.891").And.Contain("\"2026-11-26\"").And.Contain("0.75");
    }

    [Fact]
    public async Task DomainErrorIsBanglaWhenAskedFor()
    {
        var (status, body) = await GetAsync("/shortage", "bn");

        status.Should().Be(422);
        Detail(body).Should().Be("'Maize'-এর মজুদ যথেষ্ট নয়। প্রয়োজন: 5 kg, আছে: 1 kg।");
        Title(body).Should().Be("খামারের নিয়মে আটকে গেছে");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US,en;q=0.9")]
    [InlineData(null)]
    public async Task DomainErrorStaysEnglishOtherwise(string? language)
    {
        var (_, body) = await GetAsync("/shortage", language);

        Detail(body).Should().Be("Insufficient stock for 'Maize'. Required: 5 kg, Available: 1 kg.");
        Title(body).Should().Be("Business Rule Violated");
    }

    [Fact]
    public async Task ResultsProblemIsTranslatedToo()
    {
        var (status, body) = await GetAsync("/problem", "bn");

        status.Should().Be(403);
        Detail(body).Should().Be("এই খামারে আপনার প্রবেশাধিকার নেই।");
    }

    private async Task<(int Status, string Body)> GetAsync(string path, string? language)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (language is not null)
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
        using var response = await _client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string? Detail(string body) => JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString();

    private static string? Title(string body) => JsonDocument.Parse(body).RootElement.GetProperty("title").GetString();

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
