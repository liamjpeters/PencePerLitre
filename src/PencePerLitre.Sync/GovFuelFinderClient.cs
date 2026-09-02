using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using PencePerLitre.Shared;

namespace PencePerLitre.Sync;

public class GovFuelFinderClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly bool _ownsHttpClient;
    private string? _accessToken;
    private DateTime _tokenExpiryUtc = DateTime.MinValue;

    private const string DefaultBaseUrl = "https://www.fuel-finder.service.gov.uk";

    public GovFuelFinderClient(string clientId, string clientSecret, HttpClient? httpClient = null)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        if (httpClient is null)
        {
            var configuredBaseUrl = Environment.GetEnvironmentVariable("FUEL_FINDER_BASE_URL");
            var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
                ? DefaultBaseUrl
                : configuredBaseUrl;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        var proxyKey = Environment.GetEnvironmentVariable("FUEL_FINDER_PROXY_KEY");
        if (!string.IsNullOrWhiteSpace(proxyKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Proxy-Key", proxyKey);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PencePerLitre/1.0 (Mozilla/5.0; Windows NT 10.0; Win64; x64)");
    }

    public async Task EnsureAuthenticatedAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiryUtc.AddMinutes(-5))
        {
            return;
        }

        Console.WriteLine("Acquiring Gov.UK Fuel Finder OAuth access token...");
        var payload = new
        {
            client_id = _clientId,
            client_secret = _clientSecret
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/oauth/generate_access_token", payload);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"OAuth response status: {(int)response.StatusCode} {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OAuth token generation failed ({response.StatusCode}): {responseBody}");
            }

            var oauthResult = await response.Content.ReadFromJsonAsync<GovOAuthResponse>(SharedJsonOptions.Default);
            if (oauthResult?.Data == null || string.IsNullOrEmpty(oauthResult.Data.AccessToken))
            {
                throw new InvalidOperationException($"Invalid OAuth response payload: {oauthResult?.Message}");
            }

            _accessToken = oauthResult.Data.AccessToken;
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(oauthResult.Data.ExpiresIn);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            Console.WriteLine($"Token acquired successfully. Expires at {_tokenExpiryUtc:u} (in {oauthResult.Data.ExpiresIn}s)");
            return;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request exception during OAuth token generation: {ex.Message}");
            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
            throw;
        }
    }

    /// <summary>
    /// Fetches all forecourts across the UK, paginating batch by batch.
    /// </summary>
    public async Task<List<GovPfsStation>> FetchAllForecourtsAsync(string? effectiveStartTimestamp = null)
    {
        await EnsureAuthenticatedAsync();

        var allForecourts = new List<GovPfsStation>();
        int batchNumber = 1;
        bool hasMore = true;

        Console.WriteLine(string.IsNullOrEmpty(effectiveStartTimestamp)
            ? "Fetching full forecourt metadata (PFS)..."
            : $"Fetching incremental forecourt metadata since {effectiveStartTimestamp}...");

        while (hasMore)
        {
            var uri = string.IsNullOrEmpty(effectiveStartTimestamp)
                ? $"/api/v1/pfs?batch-number={batchNumber}"
                : $"/api/v1/pfs?effective-start-timestamp={effectiveStartTimestamp}&batch-number={batchNumber}";

            Console.Write($"  Fetching PFS batch #{batchNumber}... ");
            var response = await _httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                if (IsUnavailableBatch(response.StatusCode, err))
                {
                    Console.WriteLine("No batch available (end of data).");
                    hasMore = false;
                    continue;
                }

                throw new InvalidOperationException($"Failed to fetch PFS batch {batchNumber} ({response.StatusCode}): {err}");
            }

            var batch = await response.Content.ReadFromJsonAsync<List<GovPfsStation>>(SharedJsonOptions.Default);
            if (batch == null || batch.Count == 0)
            {
                Console.WriteLine("0 items returned (End of data).");
                hasMore = false;
            }
            else
            {
                Console.WriteLine($"{batch.Count} items.");
                allForecourts.AddRange(batch);
                if (batch.Count < 500)
                {
                    hasMore = false;
                }
                else
                {
                    batchNumber++;
                    // Small delay to be polite to the API
                    await Task.Delay(150);
                }
            }
        }

        Console.WriteLine($"Total forecourts retrieved: {allForecourts.Count}");
        return allForecourts;
    }

    /// <summary>
    /// Fetches fuel prices, paginating batch by batch.
    /// </summary>
    public async Task<List<GovFuelStationPrice>> FetchFuelPricesAsync(string? effectiveStartTimestamp = null)
    {
        await EnsureAuthenticatedAsync();

        var allPrices = new List<GovFuelStationPrice>();
        int batchNumber = 1;
        bool hasMore = true;

        Console.WriteLine(string.IsNullOrEmpty(effectiveStartTimestamp)
            ? "Fetching full fuel prices..."
            : $"Fetching incremental fuel prices since {effectiveStartTimestamp}...");

        while (hasMore)
        {
            var uri = string.IsNullOrEmpty(effectiveStartTimestamp)
                ? $"/api/v1/pfs/fuel-prices?batch-number={batchNumber}"
                : $"/api/v1/pfs/fuel-prices?effective-start-timestamp={effectiveStartTimestamp}&batch-number={batchNumber}";

            Console.Write($"  Fetching Fuel Prices batch #{batchNumber}... ");
            var response = await _httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                if (IsUnavailableBatch(response.StatusCode, err))
                {
                    Console.WriteLine("No batch available (end of data).");
                    hasMore = false;
                    continue;
                }

                throw new InvalidOperationException($"Failed to fetch Fuel Prices batch {batchNumber} ({response.StatusCode}): {err}");
            }

            var batch = await response.Content.ReadFromJsonAsync<List<GovFuelStationPrice>>(SharedJsonOptions.Default);
            if (batch == null || batch.Count == 0)
            {
                Console.WriteLine("0 items returned (End of data).");
                hasMore = false;
            }
            else
            {
                Console.WriteLine($"{batch.Count} items.");
                allPrices.AddRange(batch);
                if (batch.Count < 500)
                {
                    hasMore = false;
                }
                else
                {
                    batchNumber++;
                    // Small delay to be polite to the API
                    await Task.Delay(150);
                }
            }
        }

        Console.WriteLine($"Total fuel price station records retrieved: {allPrices.Count}");
        return allPrices;
    }

    private static bool IsUnavailableBatch(HttpStatusCode statusCode, string responseBody) =>
        statusCode == HttpStatusCode.NotFound &&
        responseBody.Contains("Requested batch", StringComparison.OrdinalIgnoreCase) &&
        responseBody.Contains("not available", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
