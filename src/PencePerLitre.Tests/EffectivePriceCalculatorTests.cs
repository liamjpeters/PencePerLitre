using PencePerLitre.Shared;

namespace PencePerLitre.Tests;

public class EffectivePriceCalculatorTests
{
    [Fact]
    public void Calculate_UkMpg_IncludesReturnJourneyFuelCost()
    {
        var settings = new VehicleSettings
        {
            TankCapacityLitres = 50,
            Efficiency = 40,
            UseMetric = false
        };

        var result = EffectivePriceCalculator.Calculate(145, 10, settings);

        Assert.NotNull(result);
        Assert.Equal(151.5918, result.Value, precision: 4);
    }

    [Fact]
    public void Calculate_MetricEfficiency_IncludesReturnJourneyFuelCost()
    {
        var settings = new VehicleSettings
        {
            TankCapacityLitres = 50,
            Efficiency = 7.1,
            UseMetric = true
        };

        var result = EffectivePriceCalculator.Calculate(145, 10, settings);

        Assert.NotNull(result);
        Assert.Equal(151.6273, result.Value, precision: 4);
    }

    [Fact]
    public void Calculate_InvalidSettings_ReturnsNull()
    {
        var settings = new VehicleSettings
        {
            TankCapacityLitres = 0,
            Efficiency = 40
        };

        var result = EffectivePriceCalculator.Calculate(145, 10, settings);

        Assert.Null(result);
    }
}
