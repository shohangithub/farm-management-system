using System.Globalization;
using Farm360.Application.Intelligence.Services;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>
/// The "Sell or keep?" maths for one animal: pure, no database, so every number can be checked by
/// hand. <see cref="AnimalSellOrKeepAdvisor"/> gathers the inputs; the report, the animal page and the
/// livestock list all show what this returns.
/// </summary>
/// <remarks>
/// <para>For each day d = 0…<see cref="SellOrKeepAssumptions.HorizonDays"/>:</para>
/// <list type="bullet">
/// <item>gain(d) = recent gain/day × (1 − decline × min(d, decline days) ÷ decline days);</item>
/// <item>weight(d) = weight today + Σ gain(1…d);</item>
/// <item>daily cost(d) = feed/day × (weight(d) ÷ weight today)^0.75 + labour &amp; overhead/day + health/day;</item>
/// <item>cost(d) = invested today + Σ daily cost(1…d); value(d) = weight(d) × price/kg + per-head addition; profit(d) = value(d) − cost(d).</item>
/// </list>
/// <para>The best day is the first day with the highest profit; it is reached when one more day's gain is
/// worth less than that day's cost.</para>
/// </remarks>
public static class SellOrKeepCalculator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Least-squares slope (kg/day) of weight against date, with the fit quality R² (1 = a straight line).
    /// Null when there are fewer than two weighings or they are all on one day.
    /// </summary>
    public static (decimal? SlopeKgPerDay, decimal? R2) LeastSquaresSlope(IReadOnlyList<SellOrKeepWeighing> weighings)
    {
        ArgumentNullException.ThrowIfNull(weighings);
        if (weighings.Count < 2)
            return (null, null);

        var origin = weighings.Min(w => w.Date.DayNumber);
        var xs = weighings.Select(w => (double)(w.Date.DayNumber - origin)).ToArray();
        var ys = weighings.Select(w => (double)w.WeightKg).ToArray();
        var mx = xs.Average();
        var my = ys.Average();

        double sxx = 0, sxy = 0, syy = 0;
        for (var i = 0; i < xs.Length; i++)
        {
            sxx += (xs[i] - mx) * (xs[i] - mx);
            sxy += (xs[i] - mx) * (ys[i] - my);
            syy += (ys[i] - my) * (ys[i] - my);
        }

        if (sxx <= 0)
            return (null, null);

        var slope = sxy / sxx;
        double r2;
        if (syy <= 0)
        {
            r2 = 1;
        }
        else
        {
            var ssRes = 0d;
            for (var i = 0; i < xs.Length; i++)
            {
                var fitted = my + slope * (xs[i] - mx);
                ssRes += (ys[i] - fitted) * (ys[i] - fitted);
            }

            r2 = Math.Clamp(1 - ssRes / syy, 0, 1);
        }

        return (Math.Round((decimal)slope, 4), Math.Round((decimal)r2, 3));
    }

    /// <summary>Works out the advice for one animal.</summary>
    /// <param name="input">The animal's figures.</param>
    /// <param name="assumptions">The assumptions (defaults when null).</param>
    /// <param name="includeProjection">Return the day-by-day projection.</param>
    /// <param name="overrides">
    /// What-if changes (Fattening plan). Null or empty gives exactly the "Sell or keep?" advice. They change
    /// the projection only: today's weight, value and profit always come from the records.
    /// </param>
    public static SellOrKeepAdvice Advise(
        SellOrKeepInput input,
        SellOrKeepAssumptions? assumptions = null,
        bool includeProjection = true,
        SellOrKeepOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var a = assumptions ?? SellOrKeepAssumptions.Default;
        var o = overrides ?? new SellOrKeepOverrides();
        if (o.HorizonDays is { } horizon)
            a = a with { HorizonDays = Math.Clamp(horizon, 1, SellOrKeepOverrides.MaxHorizonDays) };
        if (o.GrowthSlowdownPercent is { } slowdown)
            a = a with { GrowthDeclineFraction = Math.Clamp(slowdown, 0m, 100m) / 100m };
        var today = input.Today;
        var daysOnFarm = Math.Max(0, today.DayNumber - input.AcquisitionDate.DayNumber);

        // ── 1. Today ──
        var weighings = input.Weighings
            .Where(w => w.Date <= today && w.WeightKg > 0m)
            .GroupBy(w => w.Date)
            .Select(g => g.Last())
            .OrderBy(w => w.Date)
            .ToList();
        var latest = weighings.Count > 0 ? weighings[^1] : null;

        // ── 2. Growth ──
        var recentFrom = today.AddDays(-a.RecentGrowthWindowDays);
        var recent = weighings.Where(w => w.Date >= recentFrom).ToList();
        decimal? adg = null;
        decimal? fit = null;
        var growthSource = SellOrKeepGrowthSource.None;
        var growthPoints = 0;
        var (recentSlope, recentFit) = LeastSquaresSlope(recent);
        if (recentSlope is not null)
        {
            adg = recentSlope;
            fit = recent.Count >= 3 ? recentFit : null;
            growthSource = SellOrKeepGrowthSource.Recent;
            growthPoints = recent.Count;
        }
        // Since the first weighing (the animal page header's ADG).
        decimal? lifetimeAdg = weighings.Count >= 2 && weighings[^1].Date > weighings[0].Date
            ? Math.Round((weighings[^1].WeightKg - weighings[0].WeightKg) / (weighings[^1].Date.DayNumber - weighings[0].Date.DayNumber), 4)
            : null;
        if (adg is null && lifetimeAdg is not null)
        {
            adg = lifetimeAdg;
            growthSource = SellOrKeepGrowthSource.Lifetime;
            growthPoints = weighings.Count;
        }

        var target = input.BreedTargetAdgKg is > 0m ? input.BreedTargetAdgKg : null;
        var vsTarget = adg is null || target is null ? SellOrKeepGrowthVsTarget.Unknown
            : adg < target * (1m - a.AtTargetBand) ? SellOrKeepGrowthVsTarget.Below
            : adg > target * (1m + a.AtTargetBand) ? SellOrKeepGrowthVsTarget.Above
            : SellOrKeepGrowthVsTarget.At;

        decimal? weightToday = null;
        if (latest is not null)
        {
            var gap = Math.Min(today.DayNumber - latest.Date.DayNumber, a.MaxExtrapolationDays);
            weightToday = Math.Max(0m, Math.Round(latest.WeightKg + (adg ?? 0m) * gap, 1));
        }

        // ── 3. Cost run-rate ──
        var feedDays = Math.Max(1, Math.Min(a.FeedWindowDays, daysOnFarm));
        var feedBasis = input.FeedCostInWindowBdt > 0m ? SellOrKeepCostBasis.RecentRecords
            : daysOnFarm > 0 && input.Cost.FeedBdt > 0m ? SellOrKeepCostBasis.LifetimeAverage
            : SellOrKeepCostBasis.None;
        var feedPerDay = feedBasis switch
        {
            SellOrKeepCostBasis.RecentRecords => input.FeedCostInWindowBdt / feedDays,
            SellOrKeepCostBasis.LifetimeAverage => input.Cost.FeedBdt / daysOnFarm,
            _ => 0m,
        };
        var labourBasis = input.LabourOverheadDaysCovered > 0 && input.LabourOverheadInWindowBdt > 0m ? SellOrKeepCostBasis.RecentRecords
            : daysOnFarm > 0 && input.Cost.LabourBdt + input.Cost.OverheadBdt > 0m ? SellOrKeepCostBasis.LifetimeAverage
            : SellOrKeepCostBasis.None;
        var labourPerDay = labourBasis switch
        {
            SellOrKeepCostBasis.RecentRecords => input.LabourOverheadInWindowBdt / input.LabourOverheadDaysCovered,
            SellOrKeepCostBasis.LifetimeAverage => (input.Cost.LabourBdt + input.Cost.OverheadBdt) / daysOnFarm,
            _ => 0m,
        };
        var healthDays = Math.Max(1, Math.Min(a.HealthWindowDays, daysOnFarm));
        var healthBasis = input.HealthCostInWindowBdt > 0m ? SellOrKeepCostBasis.RecentRecords : SellOrKeepCostBasis.None;
        var healthPerDay = Math.Max(0m, input.HealthCostInWindowBdt) / healthDays;
        feedPerDay = Math.Round(Math.Max(0m, feedPerDay), 2);
        labourPerDay = Math.Round(Math.Max(0m, labourPerDay), 2);
        healthPerDay = Math.Round(healthPerDay, 2);

        // What-if costs replace the recorded run-rate.
        if (o.FeedPerDayBdt is { } feedOverride)
        {
            feedPerDay = Math.Round(Math.Max(0m, feedOverride), 2);
            feedBasis = SellOrKeepCostBasis.Override;
        }

        if (o.LabourOverheadPerDayBdt is { } labourOverride)
        {
            labourPerDay = Math.Round(Math.Max(0m, labourOverride), 2);
            labourBasis = SellOrKeepCostBasis.Override;
        }

        if (o.HealthPerDayBdt is { } healthOverride)
        {
            healthPerDay = Math.Round(Math.Max(0m, healthOverride), 2);
            healthBasis = SellOrKeepCostBasis.Override;
        }

        // ── 6. Health risk ──
        var sick = input.TreatmentsInWindow >= a.FrequentlySickTreatments
                   || (feedPerDay > 0m && healthPerDay > feedPerDay * a.FrequentlySickHealthShareOfFeed);

        // ── 4. Price ──
        // Price per kg live weight, plus a fixed amount per head (by-products under the meat-yield rule).
        // A what-if price applies from today on ("what if the market paid X?"). Without one this is the
        // resolved price, so day 0 matches the "Profit if sold today" figure on the animal page.
        var priceOverridden = o.PricePerKgBdt is not null || o.PerHeadBdt is not null;
        var usedPrice = priceOverridden
            ? input.Price with
            {
                PricePerKgBdt = Math.Max(0m, o.PricePerKgBdt ?? input.Price.PricePerKgBdt),
                PerHeadBdt = Math.Max(0m, o.PerHeadBdt ?? input.Price.PerHeadBdt),
            }
            : input.Price;
        var price = usedPrice.PricePerKgBdt;
        var perHead = usedPrice.PerHeadBdt;
        var invested = input.Cost.TotalBdt;
        decimal? valueToday = weightToday is { } wt ? Math.Round(wt * price + perHead, 0) : null;
        decimal? profitToday = valueToday - invested;

        // ── 5. Projection ──
        // The measured gain, or the what-if one. Today's weight always uses the measured gain.
        var projectionAdg = o.AdgKgPerDay is { } adgOverride ? Math.Round(adgOverride, 4) : adg;
        var points = new List<SellOrKeepPoint>();
        decimal? endWeight = null;
        decimal? endCost = null;
        if (projectionAdg is { } g && weightToday is { } w0)
        {
            var weight = w0;
            var cost = invested;
            points.Add(new SellOrKeepPoint(0, today, Math.Round(weight, 1), g, 0m, Math.Round(cost, 0), Math.Round(weight * price + perHead, 0), Math.Round(weight * price + perHead - cost, 0)));
            for (var d = 1; d <= a.HorizonDays; d++)
            {
                var decline = a.GrowthDeclineDays > 0 ? a.GrowthDeclineFraction * Math.Min(d, a.GrowthDeclineDays) / a.GrowthDeclineDays : 0m;
                var gain = g * (1m - decline);
                weight = Math.Max(0m, weight + gain);
                var feedScale = w0 > 0m && weight > 0m ? (decimal)Math.Pow((double)(weight / w0), a.FeedScalingExponent) : 1m;
                var daily = feedPerDay * feedScale + labourPerDay + healthPerDay;
                cost += daily;
                var value = weight * price + perHead;
                points.Add(new SellOrKeepPoint(d, today.AddDays(d), Math.Round(weight, 1), Math.Round(gain, 3), Math.Round(daily, 2), Math.Round(cost, 0), Math.Round(value, 0), Math.Round(value - cost, 0)));
            }

            endWeight = weight;
            endCost = cost;
        }

        // Best day = the first day with the highest profit (in whole taka); break-even = the first day a
        // loss today turns into a profit.
        SellOrKeepPoint? best = null;
        SellOrKeepPoint? breakEven = null;
        foreach (var p in points)
        {
            if (best is null || p.ProfitBdt > best.ProfitBdt)
                best = p;
            if (breakEven is null && points[0].ProfitBdt < 0m && p.ProfitBdt >= 0m)
                breakEven = p;
        }

        decimal? valueOfGain = points.Count > 1 ? Math.Round(points[1].AdgKg * price, 0) : null;
        decimal? marginPerDay = points.Count > 1 ? Math.Round(points[1].AdgKg * price - points[1].DailyCostBdt, 0) : null;
        decimal? feedPerKgGain = projectionAdg is > 0m ? Math.Round(feedPerDay / projectionAdg.Value, 0) : null;
        // Total cost per kg gained: every taka spent per day ÷ kg gained per day.
        decimal? costPerKgGain = projectionAdg is > 0m ? Math.Round((feedPerDay + labourPerDay + healthPerDay) / projectionAdg.Value, 0) : null;
        decimal? roiAtBest = best is { CostBdt: > 0m } ? Math.Round(best.ProfitBdt / best.CostBdt * 100m, 1) : null;
        var last = points.Count > 0 ? points[^1] : null;
        // Price per kg live that makes value = cost on the last day: (cost − per head) ÷ weight.
        decimal? breakEvenPriceAtHorizon = endWeight is > 0m && endCost is { } ec
            ? Math.Round(Math.Max(0m, ec - perHead) / endWeight.Value, 2)
            : null;

        // ── 7. Verdict ──
        SellOrKeepVerdict verdict;
        if (best is null)
            verdict = SellOrKeepVerdict.WeighFirst;
        else if (best.ProfitBdt < 0m)
            verdict = SellOrKeepVerdict.LosingMoney;
        else if (best.Day <= a.SellNowWithinDays || marginPerDay < 0m || (sick && profitToday > 0m))
            verdict = SellOrKeepVerdict.SellNow;
        else
            verdict = SellOrKeepVerdict.Keep;

        var cause = SellOrKeepLossCause.None;
        if (verdict == SellOrKeepVerdict.LosingMoney || profitToday < 0m)
        {
            cause = sick ? SellOrKeepLossCause.Sickness
                : vsTarget == SellOrKeepGrowthVsTarget.Below || projectionAdg is <= 0m ? SellOrKeepLossCause.SlowGrowth
                : feedPerKgGain is { } fpk && fpk >= price ? SellOrKeepLossCause.FeedCost
                : SellOrKeepLossCause.PurchasePrice;
        }

        // Confidence: how many recent weighings and how straight they lie; sickness and an old
        // weighing each take it down one step.
        var confidence = SellOrKeepConfidence.Low;
        if (growthSource == SellOrKeepGrowthSource.Recent)
        {
            confidence = growthPoints >= 4 && fit >= a.HighConfidenceFit ? SellOrKeepConfidence.High
                : growthPoints == 2 || (growthPoints >= 3 && fit >= a.MediumConfidenceFit) ? SellOrKeepConfidence.Medium
                : SellOrKeepConfidence.Low;
        }

        if (sick && confidence > SellOrKeepConfidence.Low)
            confidence--;
        if (latest is not null && today.DayNumber - latest.Date.DayNumber > a.StaleWeighingDays && confidence > SellOrKeepConfidence.Low)
            confidence--;

        var advice = new SellOrKeepAdvice
        {
            AnimalId = input.AnimalId,
            TagId = input.TagId,
            BreedName = input.BreedName,
            BatchId = input.BatchId,
            BatchName = input.BatchName,
            Today = today,
            DaysOnFarm = daysOnFarm,
            Cost = input.Cost,
            Weighings = weighings.Count,
            LatestWeightKg = latest?.WeightKg,
            LatestWeightDate = latest?.Date,
            WeightTodayKg = weightToday,
            ValueTodayBdt = valueToday,
            ProfitTodayBdt = profitToday,
            AdgKgPerDay = adg,
            GrowthSource = growthSource,
            GrowthWeighings = growthPoints,
            GrowthFitR2 = fit,
            BreedTargetAdgKg = target,
            BreedTargetBasis = target is null ? SellOrKeepBreedTargetBasis.None : input.BreedTargetBasis,
            BreedTargetMinKg = target is null ? null : input.BreedTargetMinKg,
            BreedTargetMaxKg = target is null ? null : input.BreedTargetMaxKg,
            GrowthVsTarget = vsTarget,
            LifetimeAdgKgPerDay = lifetimeAdg,
            FirstWeighingDate = weighings.Count > 0 ? weighings[0].Date : null,
            RecentAdgKgPerDay = recentSlope,
            RecentGrowthWindowDays = a.RecentGrowthWindowDays,
            ProjectionAdgKgPerDay = projectionAdg,
            AdgOverridden = o.AdgKgPerDay is not null,
            GrowthSlowdownPercent = Math.Round(a.GrowthDeclineFraction * 100m, 1),
            GrowthDeclineDays = a.GrowthDeclineDays,
            FeedPerDayBdt = feedPerDay,
            LabourOverheadPerDayBdt = labourPerDay,
            HealthPerDayBdt = healthPerDay,
            FeedCostBasis = feedBasis,
            LabourOverheadCostBasis = labourBasis,
            HealthCostBasis = healthBasis,
            FeedWindowDays = a.FeedWindowDays,
            HealthWindowDays = a.HealthWindowDays,
            FeedCostPerKgGainBdt = feedPerKgGain,
            CostPerKgGainBdt = costPerKgGain,
            Price = usedPrice,
            PriceOverridden = priceOverridden,
            HorizonDays = a.HorizonDays,
            BestDay = best?.Day,
            BestDate = best?.Date,
            BestWeightKg = best?.WeightKg,
            ProfitAtBestDayBdt = best?.ProfitBdt,
            BreakEvenDay = breakEven?.Day,
            BreakEvenDate = breakEven?.Date,
            RoiAtBestDayPercent = roiAtBest,
            WeightAtHorizonKg = last?.WeightKg,
            CostAtHorizonBdt = last?.CostBdt,
            ProfitAtHorizonBdt = last?.ProfitBdt,
            BreakEvenPricePerKgAtHorizonBdt = breakEvenPriceAtHorizon,
            ValueOfDailyGainBdt = valueOfGain,
            MarginPerDayKeptBdt = marginPerDay,
            TreatmentsLast90Days = input.TreatmentsInWindow,
            HealthCostLast90DaysBdt = Math.Round(input.HealthCostInWindowBdt, 0),
            FrequentlySick = sick,
            BatchDeathsLast90Days = input.BatchDeathsInWindow,
            Verdict = verdict,
            LossCause = cause,
            Confidence = confidence,
            Projection = includeProjection ? points : [],
        };

        var (en, bn) = Reason(advice, a);
        return advice with { ReasonEn = en, ReasonBn = bn };
    }

    /// <summary>The one-line reason, in English and Bangla.</summary>
    public static (string En, string Bn) Reason(SellOrKeepAdvice x, SellOrKeepAssumptions? assumptions = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        var a = assumptions ?? SellOrKeepAssumptions.Default;
        var today = x.ProfitTodayBdt ?? 0m;

        switch (x.Verdict)
        {
            case SellOrKeepVerdict.WeighFirst:
                return x.Weighings == 0
                    ? ("Weigh it first: it has never been weighed, so its growth and value cannot be judged.",
                        "আগে ওজন নিন: কখনো ওজন নেওয়া হয়নি, তাই বৃদ্ধি ও দাম বোঝা যাচ্ছে না।")
                    : ("Weigh it first: at least two weighings on different days are needed to judge its growth.",
                        "আগে ওজন নিন: বৃদ্ধি বুঝতে আলাদা দিনে অন্তত দুবার ওজন দরকার।");

            case SellOrKeepVerdict.LosingMoney:
            {
                var shortBy = -(x.ProfitAtBestDayBdt ?? today);
                var (causeEn, causeBn) = CauseText(x);
                return ($"Losing money: even at its best it stays {Tk(shortBy)} short of its cost — {causeEn}.",
                    $"লোকসান হচ্ছে: সবচেয়ে ভালো সময়েও খরচের চেয়ে {Tk(shortBy)} কম থাকবে — {causeBn}।");
            }

            case SellOrKeepVerdict.SellNow:
            {
                var result = today >= 0m ? ("selling today makes " + Tk(today), "আজ বিক্রি করলে লাভ " + Tk(today))
                    : ("selling today loses " + Tk(-today), "আজ বিক্রি করলে লোকসান " + Tk(-today));
                if (x.FrequentlySick && today > 0m)
                {
                    return ($"Sell now: it is often sick ({x.TreatmentsLast90Days} treatments in {a.HealthWindowDays} days, {Tk(x.HealthCostLast90DaysBdt)}) and {result.Item1}.",
                        $"এখনই বিক্রি করুন: প্রায়ই অসুস্থ হয় ({a.HealthWindowDays} দিনে {x.TreatmentsLast90Days}টি চিকিৎসা, {Tk(x.HealthCostLast90DaysBdt)}); {result.Item2}।");
                }

                if (x.MarginPerDayKeptBdt < 0m)
                {
                    return ($"Sell now: one more day adds about {Tk(x.ValueOfDailyGainBdt ?? 0m)} of weight but costs {Tk((x.ValueOfDailyGainBdt ?? 0m) - x.MarginPerDayKeptBdt!.Value)}; {result.Item1}.",
                        $"এখনই বিক্রি করুন: আরেক দিন রাখলে ওজন বাড়ে প্রায় {Tk(x.ValueOfDailyGainBdt ?? 0m)}-এর, কিন্তু খরচ {Tk((x.ValueOfDailyGainBdt ?? 0m) - x.MarginPerDayKeptBdt!.Value)}; {result.Item2}।");
                }

                return ($"Sell now: profit peaks within {a.SellNowWithinDays} days at about {Tk(x.ProfitAtBestDayBdt ?? today)}; {result.Item1}.",
                    $"এখনই বিক্রি করুন: {a.SellNowWithinDays} দিনের মধ্যেই লাভ সবচেয়ে বেশি, প্রায় {Tk(x.ProfitAtBestDayBdt ?? today)}; {result.Item2}।");
            }

            default:
            {
                var days = x.BestDay ?? 0;
                var date = x.BestDate is { } d ? d.ToString("d MMM yyyy", Invariant) : string.Empty;
                var kg = (x.BestWeightKg ?? 0m).ToString("0", Invariant);
                var extra = (x.ProfitAtBestDayBdt ?? 0m) - today;
                return ($"Keep about {days} more days: sell around {date} at ~{kg} kg, expected profit {Tk(x.ProfitAtBestDayBdt ?? 0m)} ({Tk(extra)} more than today).",
                    $"আরও প্রায় {days} দিন রাখুন: {date} নাগাদ ~{kg} কেজিতে বিক্রি করুন, সম্ভাব্য লাভ {Tk(x.ProfitAtBestDayBdt ?? 0m)} (আজকের চেয়ে {Tk(extra)} বেশি)।");
            }
        }
    }

    /// <summary>The likely cause of a loss, as a short phrase.</summary>
    public static (string En, string Bn) CauseText(SellOrKeepAdvice x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return x.LossCause switch
        {
            SellOrKeepLossCause.Sickness => ("check its health: it is often sick and treatment is eating the profit", "স্বাস্থ্য দেখুন: প্রায়ই অসুস্থ, চিকিৎসা খরচে লাভ চলে যাচ্ছে"),
            SellOrKeepLossCause.SlowGrowth => x.AdgKgPerDay is { } g && x.BreedTargetAdgKg is { } t
                ? ($"check feed and health: it gains {g.ToString("0.00", Invariant)} kg/day against a breed target of {t.ToString("0.00", Invariant)}",
                    $"খাদ্য ও স্বাস্থ্য দেখুন: দৈনিক {g.ToString("0.00", Invariant)} কেজি বাড়ছে, জাতের লক্ষ্য {t.ToString("0.00", Invariant)}")
                : ("check feed and health: it is hardly gaining weight", "খাদ্য ও স্বাস্থ্য দেখুন: ওজন প্রায় বাড়ছেই না"),
            SellOrKeepLossCause.FeedCost => ($"check the ration: each kg gained costs {Tk(x.FeedCostPerKgGainBdt ?? 0m)} in feed but sells for {Tk(x.Price.PricePerKgBdt)}",
                $"খাদ্য তালিকা দেখুন: প্রতি কেজি বৃদ্ধিতে খাদ্য খরচ {Tk(x.FeedCostPerKgGainBdt ?? 0m)}, বিক্রি দাম {Tk(x.Price.PricePerKgBdt)}"),
            _ => ("it was bought for more than its weight is worth; sell when the price is right or cut daily costs",
                "ওজনের তুলনায় কেনা দাম বেশি ছিল; ভালো দাম পেলে বিক্রি করুন বা দৈনিক খরচ কমান"),
        };
    }

    private static string Tk(decimal amount) => "৳" + Math.Round(amount, 0).ToString("#,##0", Invariant);
}
