using PencePerLitre.Shared;
using PencePerLitre.Sync;

namespace PencePerLitre.Tests;

public class ConfigurationAndPostcodeTests
{
    [Fact]
    public void GovFuelFinderClient_EmptyConfiguredBaseUrl_UsesDefault()
    {
        var originalValue = Environment.GetEnvironmentVariable("FUEL_FINDER_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("FUEL_FINDER_BASE_URL", string.Empty);
            using var client = new GovFuelFinderClient("client", "secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FUEL_FINDER_BASE_URL", originalValue);
        }
    }

    [Fact]
    public void Lookup_FindsKnownPostcodeInShippedPack()
    {
        var engine = new PostcodeLookupEngine();
        engine.LoadPack(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "postcodes.pack")));

        var result = engine.Lookup("PO14 3LG");

        Assert.True(result.Found);
        Assert.Equal("PO14 3LG", result.CanonicalPostcode);
    }
}