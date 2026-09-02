using System.Net;
using System.Text;
using PencePerLitre.Client.Services;

namespace PencePerLitre.Tests;

public class FuelDataServiceTests
{
    [Fact]
    public async Task EnsureLoadedAsync_RetriesAfterFailedDownload()
    {
      var stationAttempts = 0;
      var handler = new SequenceHandler((request, _) =>
      {
        if (request.RequestUri!.AbsolutePath.EndsWith("stations.json", StringComparison.Ordinal))
        {
          stationAttempts++;
          return stationAttempts == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : JsonResponse("[]");
        }

        return JsonResponse("{}");
      });
        var service = new FuelDataService(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => service.EnsureLoadedAsync());
        Assert.False(service.IsLoaded);

        await service.EnsureLoadedAsync();

        Assert.True(service.IsLoaded);
        Assert.Equal(6, handler.RequestCount);
    }

    [Fact]
    public async Task Search_Open24HoursOnly_RequiresEveryListedDayToBeOpenAllDay()
    {
        const string stationsJson = """
            [
              {
                "id": "always",
                "name": "Always Open",
                "lat": 51.5,
                "lon": -0.1,
                "opening": {
                  "usual_days": {
                    "monday": { "is_24_hours": true },
                    "tuesday": { "is_24_hours": true }
                  }
                }
              },
              {
                "id": "sometimes",
                "name": "Sometimes Open",
                "lat": 51.5,
                "lon": -0.1,
                "opening": {
                  "usual_days": {
                    "monday": { "is_24_hours": true },
                    "tuesday": { "is_24_hours": false }
                  }
                }
              }
            ]
            """;
        var handler = new SequenceHandler((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith("stations.json", StringComparison.Ordinal)
                ? JsonResponse(stationsJson)
                : JsonResponse("{}"));
        var service = new FuelDataService(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        });
        await service.EnsureLoadedAsync();

        var results = service.Search(51.5, -0.1, open24HoursOnly: true);

        var station = Assert.Single(results);
        Assert.Equal("always", station.Station.Id);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class SequenceHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request, RequestCount));
        }
    }
}