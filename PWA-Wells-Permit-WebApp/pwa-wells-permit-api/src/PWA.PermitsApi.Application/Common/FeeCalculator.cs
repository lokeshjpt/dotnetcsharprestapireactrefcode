using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Common;

/// <summary>
/// Authoritative permit-fee math, ported 1:1 from the legacy Java beans
/// (BeanAppWrk.getWorkCalcAmount / countWells / countSiteExtra and
/// ApplicationBean.getRecalculatedAppAmt). This is the single source of truth so the
/// stored auth_amount at submit and the amount charged at approval stay consistent.
///
/// Legacy rules:
///   wells       = (feeUnit == "site") ? 1 : Σ drill_count over non-CAN specs   (countWells)
///   workAmt     = feeRate × wells
///   siteExtra   = (feeUnit == "site" &amp;&amp; siteMax > 0) ? max(0, countWells - siteMax) × siteExtraRate : 0
///   workCalc    = round2(workAmt + siteExtra)
///   baseFee     = Σ workCalc over non-CAN works
///   total       = roundUp2(baseFee + serviceCharge + fine)
/// </summary>
public static class FeeCalculator
{
    private const string CancelledStatus = "CAN";

    /// <summary>Σ drill_count over non-cancelled work specs (legacy countWells). A missing
    /// drill_count counts as 1, matching the backend default applied at submit.</summary>
    public static int CountWells(ApplicationWork work)
    {
        var count = 0;
        foreach (var spec in work.Specs)
        {
            if (string.Equals(spec.StatusCode, CancelledStatus, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            count += spec.DrillCount ?? 1;
        }

        return count;
    }

    /// <summary>Fee for a single work row (legacy BeanAppWrk.getWorkCalcAmount).</summary>
    public static decimal WorkCalcAmount(ApplicationWork work)
    {
        var rate = work.WorkFeeRate ?? 0m;
        var isSite = string.Equals(work.WorkFeeUnit?.Trim(), "site", StringComparison.OrdinalIgnoreCase);
        var wells = isSite ? 1 : CountWells(work);
        var amount = rate * wells;

        var siteMax = work.WorkSiteMax ?? 0;
        if (isSite && siteMax > 0)
        {
            var extraWells = Math.Max(0, CountWells(work) - siteMax);
            amount += extraWells * (work.WorkSiteExtraRate ?? 0m);
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Σ of every non-cancelled work's calculated fee (the permit base fee that is stored
    /// in auth_amount). Excludes service charge and fine.</summary>
    public static decimal BaseFee(IEnumerable<ApplicationWork> works)
    {
        var total = 0m;
        foreach (var work in works)
        {
            if (string.Equals(work.StatusCode, CancelledStatus, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            total += WorkCalcAmount(work);
        }

        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Live recalculated total that is actually charged at approval
    /// (legacy ApplicationBean.getRecalculatedAppAmt): base works fee + service charge + fine,
    /// rounded UP to two decimals.</summary>
    public static decimal RecalculatedTotal(IEnumerable<ApplicationWork> works, decimal serviceCharge, decimal fine)
        => RoundUp2(BaseFee(works) + serviceCharge + fine);

    /// <summary>Round UP (away from zero) to two decimals — mirrors Java BigDecimal.ROUND_UP.</summary>
    public static decimal RoundUp2(decimal value)
    {
        var scaled = value * 100m;
        var rounded = value >= 0m ? Math.Ceiling(scaled) : Math.Floor(scaled);
        return rounded / 100m;
    }
}
