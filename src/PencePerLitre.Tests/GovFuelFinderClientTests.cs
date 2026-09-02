using System.Net;
using System.Text;
using PencePerLitre.Sync;

namespace PencePerLitre.Tests;

public class GovFuelFinderClientTests
{
    [Fact]
    public async Task FetchAllForecourtsAsync_UnavailableBatch_ReturnsEmptyResult()
    {
        using var httpClient = CreateHttpClient();
        using var client = new GovFuelFinderClient("client", "secret", httpClient);

        var result = await client.FetchAllForecourtsAsync("2026-09-02");

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchFuelPricesAsync_UnavailableBatch_ReturnsEmptyResult()
    {
        using var httpClient = CreateHttpClient();
        using var client = new GovFuelFinderClient("client", "secret", httpClient);

        var result = await client.FetchFuelPricesAsync("2026-09-02");

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchFuelPricesAsync_UnrelatedNotFound_Throws()
    {
        using var httpClient = new HttpClient(new SequenceHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("generate_access_token", StringComparison.Ordinal)
                ? JsonResponse("{\"success\":true,\"data\":{\"access_token\":\"token\",\"expires_in\":3600}}")
                : new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"message\":\"route missing\"}", Encoding.UTF8, "application/json")
                }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        using var client = new GovFuelFinderClient("client", "secret", httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchFuelPricesAsync("2026-09-02"));
    }

    private static HttpClient CreateHttpClient() => new(new SequenceHandler(request =>
        request.RequestUri!.AbsolutePath.EndsWith("generate_access_token", StringComparison.Ordinal)
            ? JsonResponse("{\"success\":true,\"data\":{\"access_token\":\"token\",\"expires_in\":3600}}")
            : new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    "{\"success\":false,\"data\":{\"message\":\"Requested batch 1 is not available\"}}",
                    Encoding.UTF8,
                    "application/json")
            }))
    {
        BaseAddress = new Uri("https://example.test/")
    };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class SequenceHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}