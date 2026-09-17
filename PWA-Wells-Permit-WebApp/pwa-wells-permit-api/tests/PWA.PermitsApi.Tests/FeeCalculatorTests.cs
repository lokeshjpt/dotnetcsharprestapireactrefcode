using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Tests;

public sealed class FeeCalculatorTests
{
    private static ApplicationWork Work(decimal rate, string unit, int siteMax, decimal extraRate, params (int drill, string status)[] specs)
    {
        var work = new ApplicationWork
        {
            WorkFeeRate = rate,
            WorkFeeUnit = unit,
            WorkSiteMax = siteMax,
            WorkSiteExtraRate = extraRate,
            StatusCode = "PEND"
        };
        var id = 1;
        foreach (var (drill, status) in specs)
        {
            work.Specs.Add(new ApplicationWorkSpec { WorkSpecsId = id++, DrillCount = drill, StatusCode = status });
        }
        return work;
    }

    [Fact]
    public void CountWells_SumsDrillCountExcludingCancelled()
    {
        var work = Work(660m, "well", 0, 0m, (1, "PEND"), (2, "PEND"), (5, "CAN"));
        Assert.Equal(3, FeeCalculator.CountWells(work));
    }

    [Fact]
    public void CountWells_TreatsMissingDrillCountAsOne()
    {
        var work = new ApplicationWork { WorkFeeRate = 100m, WorkFeeUnit = "well" };
        work.Specs.Add(new ApplicationWorkSpec { DrillCount = null, StatusCode = "PEND" });
        work.Specs.Add(new ApplicationWorkSpec { DrillCount = null, StatusCode = "PEND" });
        Assert.Equal(2, FeeCalculator.CountWells(work));
    }

    [Fact]
    public void WorkCalcAmount_MultipliesFeeByWellsForNonSiteUnit()
    {
        // The bug fixed here: "well" unit was previously charged 1× the fee regardless of well count.
        var work = Work(660m, "well", 0, 0m, (1, "PEND"), (1, "PEND"));
        Assert.Equal(1320m, FeeCalculator.WorkCalcAmount(work));
    }

    [Fact]
    public void WorkCalcAmount_SiteUnitIsFlatFeePlusExtraBeyondMax()
    {
        // Site fee: flat 1× rate covering up to siteMax wells, plus extra rate per well beyond max.
        // 4 wells, max 2, rate 500, extra 100 => 500 + (4-2)*100 = 700.
        var work = Work(500m, "site", 2, 100m, (2, "PEND"), (2, "PEND"));
        Assert.Equal(700m, FeeCalculator.WorkCalcAmount(work));
    }

    [Fact]
    public void RecalculatedTotal_AddsServiceChargeAndFineRoundedUp()
    {
        var work = Work(660m, "well", 0, 0m, (1, "PEND"), (1, "PEND"));
        Assert.Equal(1370m, FeeCalculator.RecalculatedTotal(new[] { work }, 0m, 50m));
    }

    [Fact]
    public void BaseFee_ExcludesCancelledWorks()
    {
        var live = Work(660m, "well", 0, 0m, (1, "PEND"));
        var cancelled = Work(660m, "well", 0, 0m, (1, "PEND"));
        cancelled.StatusCode = "CAN";
        Assert.Equal(660m, FeeCalculator.BaseFee(new[] { live, cancelled }));
    }

    [Fact]
    public void RoundUp2_RoundsAwayFromZero()
    {
        Assert.Equal(10.01m, FeeCalculator.RoundUp2(10.001m));
        Assert.Equal(10.00m, FeeCalculator.RoundUp2(10.00m));
    }
}
