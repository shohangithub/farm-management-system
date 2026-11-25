using System.Globalization;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Demo;

// ══════════════════════════════════════════════════════════════════════════════
// What the demo farm contains. Pure data built from "today": every date is relative to it, so a
// rebuild always looks fresh. No randomness (a fixed hash), so the same day gives the same farm.
// DemoSeeder turns this plan into real MediatR commands.
// ══════════════════════════════════════════════════════════════════════════════

public sealed record DemoBreedSpec(
    string Key, string Name, string Category, string Origin, string MainPurpose, string BestFor,
    decimal AdgPoor, decimal AdgAverage, decimal AdgGood, decimal AdgIntensive,
    decimal StandardAdgMin, decimal StandardAdgMax, decimal FcrMin, decimal FcrMax);

public sealed record DemoShedSpec(string Number, string Name, int Capacity, string AnimalType);

public sealed record DemoPenSpec(string ShedNumber, string Number, string Name, int Capacity, string AnimalGroup);

public sealed record DemoBatchSpec(string Key, string Name, string Notes);

public sealed record DemoAnimalSpec(
    string Tag,
    AnimalSpecies Species,
    string BreedKey,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    DateOnly PurchaseDate,
    decimal PriceBdt,
    decimal InitialWeightKg,
    decimal AdgKgPerDay,
    string ShedNumber,
    string PenNumber,
    string BatchKey,
    bool SlowGrower);

public sealed record DemoWeighingSpec(string Tag, DateOnly Date, decimal WeightKg);

public sealed record DemoSupplierSpec(string Key, string Name, string ContactPerson, string Phone, string Address);

public sealed record DemoNutrition(
    FeedCategory Category, decimal DryMatterPct, decimal CrudeProteinPct, decimal MeMjPerKg,
    decimal CrudeFiberPct, decimal CalciumPct, decimal PhosphorusPct);

public sealed record DemoItemSpec(
    string Key, string Name, InventoryCategory Category, string Unit, decimal ReorderThreshold,
    decimal UnitCostBdt, DemoNutrition? Nutrition);

public sealed record DemoPaymentSpec(DateOnly Date, decimal AmountBdt);

public sealed record DemoPurchaseSpec(
    DateOnly Date,
    string ItemKey,
    decimal Quantity,
    decimal UnitCostBdt,
    string SupplierKey,
    string InvoiceNumber,
    decimal? CreditPaidNowBdt,
    IReadOnlyList<DemoPaymentSpec> LaterPayments)
{
    public bool OnCredit => CreditPaidNowBdt is not null;
    public decimal TotalBdt => Quantity * UnitCostBdt;
}

public sealed record DemoRecipeIngredient(string ItemKey, decimal Percent);

public sealed record DemoRecipeSpec(
    string Key, string Title, TargetAnimalType Species, string Stage, string Description,
    IReadOnlyList<DemoRecipeIngredient> Ingredients);

/// <summary>One weight band of a feed chart: kg of each recipe a day.</summary>
public sealed record DemoChartBand(decimal MinKg, decimal MaxKg, decimal ConcentrateKg, decimal RoughageKg);

public sealed record DemoChartSpec(string Key, string Name, TargetAnimalType Species, IReadOnlyList<DemoChartBand> Bands)
{
    public DemoChartBand BandFor(decimal weightKg) =>
        Bands.FirstOrDefault(b => weightKg >= b.MinKg && weightKg < b.MaxKg)
        ?? (weightKg < Bands[0].MinKg ? Bands[0] : Bands[^1]);
}

/// <summary>
/// A pen's group ration (kg per head a day) used before the farm moved every animal onto its own
/// weight-based chart. Group plans keep the history fast to build: one entry per pen and recipe a
/// day, split between the pen's animals by the farm's feed allocation rule.
/// </summary>
public sealed record DemoPenRationSpec(string Key, string Name, TargetAnimalType Species, decimal ConcentrateKgPerHead, decimal RoughageKgPerHead, IReadOnlyList<string> PenNumbers);

public sealed record DemoProtocolStep(string StepName, int Day, string VaccineName, string ItemKey, decimal Dose);

public sealed record DemoTreatmentSpec(
    string Tag, DateOnly Date, string Diagnosis, string ItemKey, decimal Dose, string DoseUnit,
    int MeatWithdrawalDays, decimal CostBdt, string Veterinarian, string Notes,
    TreatmentStatus Outcome, DateOnly OutcomeDate);

public sealed record DemoIncidentSpec(
    DateOnly Date, string DiseaseName, IncidentSeverity Severity, string ShedNumber, string Symptoms,
    int AffectedAnimals, DateOnly ResolvedOn);

public sealed record DemoVetVisitSpec(
    DateOnly Date, string VetName, VetVisitType VisitType, string Purpose, string Findings,
    string Recommendations, decimal CostBdt, DateOnly? NextVisitDate);

public sealed record DemoDeathSpec(string Tag, DateOnly Date, CauseOfDeath Cause, string DiseaseName, string Notes);

public sealed record DemoSaleSpec(
    string Tag,
    DateOnly Date,
    decimal PriceBdt,
    string BuyerName,
    decimal SaleWeightKg,
    decimal? CreditPaidNowBdt,
    string? BuyerPhone,
    IReadOnlyList<DemoPaymentSpec> Collections)
{
    public bool OnCredit => CreditPaidNowBdt is not null;
}

public sealed record DemoExpenseSpec(DateOnly Date, string Category, decimal AmountBdt, string Description, string Reference);

public sealed record DemoPeriod(DateOnly From, DateOnly To);

public sealed record DemoLoanSpec(
    string Lender, decimal PrincipalBdt, decimal InterestRatePercent, DateOnly Date, string Schedule, string Notes,
    IReadOnlyList<DemoPaymentSpec> Repayments);

public sealed record DemoInvestorSpec(string Name, decimal AmountBdt, DateOnly Date, decimal ProfitSharePercent, string Phone, string Notes);

public enum DemoFeedingAction
{
    Confirm = 0,
    Adjust = 1,
    Skip = 2,
}

/// <summary>What happens to one feeding entry: confirmed as planned, adjusted (a share left in the trough) or skipped.</summary>
public sealed record DemoFeedingOutcome(DemoFeedingAction Action, decimal Factor, string? Reason);

public sealed class DemoSeedPlan
{
    public const string ConcentrateRecipeKey = "concentrate";
    public const string RoughageRecipeKey = "roughage";
    public const string CattleChartKey = "cattle";
    public const string GoatChartKey = "goat";

    /// <summary>How long ago the farm "started" (first purchases).</summary>
    public const int FarmAgeDays = 150;

    /// <summary>Feeding entries are generated and confirmed for this many days before today.</summary>
    public const int FeedingHistoryDays = 60;

    /// <summary>
    /// Every animal has had its own plan on the weight-based chart for this many days; before that
    /// each pen was fed on a group ration (<see cref="PenRations"/>).
    /// </summary>
    public const int IndividualChartDays = 10;

    public DateOnly Today { get; private init; }
    public DateOnly StartDate { get; private init; }
    public DateOnly BooksStartDate { get; private init; }
    public DateOnly FeedingStartDate { get; private init; }
    public DateOnly IndividualPlansStartDate { get; private init; }
    public int FiscalYearStartMonth { get; private init; } = 7;
    public decimal OpeningCashBdt { get; private init; }
    public decimal OpeningOwnerCapitalBdt { get; private init; }

    public IReadOnlyList<DemoBreedSpec> Breeds { get; private init; } = [];
    public IReadOnlyList<DemoShedSpec> Sheds { get; private init; } = [];
    public IReadOnlyList<DemoPenSpec> Pens { get; private init; } = [];
    public IReadOnlyList<DemoBatchSpec> Batches { get; private init; } = [];
    public IReadOnlyList<DemoAnimalSpec> Animals { get; private init; } = [];
    public IReadOnlyList<DemoWeighingSpec> Weighings { get; private init; } = [];
    public IReadOnlyList<DemoSupplierSpec> Suppliers { get; private init; } = [];
    public IReadOnlyList<DemoItemSpec> Items { get; private init; } = [];
    public IReadOnlyList<DemoPurchaseSpec> Purchases { get; private init; } = [];
    public IReadOnlyList<DemoRecipeSpec> Recipes { get; private init; } = [];
    public IReadOnlyList<DemoChartSpec> Charts { get; private init; } = [];
    public IReadOnlyList<DemoPenRationSpec> PenRations { get; private init; } = [];
    public string ProtocolTitle { get; private init; } = string.Empty;
    public IReadOnlyList<DemoProtocolStep> ProtocolSteps { get; private init; } = [];

    /// <summary>Days after purchase the vaccination protocol starts for each cattle.</summary>
    public int ProtocolStartAfterPurchaseDays { get; private init; } = 2;

    /// <summary>(Tag, step) doses deliberately never given, so compliance shows something overdue.</summary>
    public IReadOnlyList<(string Tag, string StepName)> MissedVaccinations { get; private init; } = [];
    public IReadOnlyList<DemoTreatmentSpec> Treatments { get; private init; } = [];
    public DemoIncidentSpec Incident { get; private init; } = null!;
    public DemoVetVisitSpec VetVisit { get; private init; } = null!;
    public DemoDeathSpec Death { get; private init; } = null!;
    public IReadOnlyList<DemoSaleSpec> Sales { get; private init; } = [];
    public IReadOnlyList<DemoExpenseSpec> Expenses { get; private init; } = [];
    public IReadOnlyList<DemoPeriod> OverheadPeriods { get; private init; } = [];
    public DemoLoanSpec Loan { get; private init; } = null!;
    public DemoInvestorSpec Investor { get; private init; } = null!;

    /// <summary>The day the animal left the farm (sale or death), if it did.</summary>
    public DateOnly? ExitDate(string tag) =>
        Sales.FirstOrDefault(s => s.Tag == tag)?.Date ?? (Death.Tag == tag ? Death.Date : null);

    /// <summary>True while the animal is on the farm on <paramref name="day"/> (bought on or before it, not yet gone).</summary>
    public bool IsOnFarm(DemoAnimalSpec animal, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return animal.PurchaseDate <= day && (ExitDate(animal.Tag) is not { } exit || day < exit);
    }

    public DemoAnimalSpec Animal(string tag) => Animals.First(a => a.Tag == tag);

    public DemoChartSpec ChartFor(DemoAnimalSpec animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return Charts.First(c => c.Key == (animal.Species == AnimalSpecies.Goat ? GoatChartKey : CattleChartKey));
    }

    /// <summary>Expected live weight on a day: initial weight plus daily gain, with a little scale noise.</summary>
    public static decimal WeightOn(DemoAnimalSpec animal, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var days = day.DayNumber - animal.PurchaseDate.DayNumber;
        var isGoat = animal.Species == AnimalSpecies.Goat;
        var noiseRange = isGoat ? 0.3m : 2.5m;
        var noise = days == 0 ? 0m : (Hash(animal.Tag, days) % 1001 / 1000m * 2m - 1m) * noiseRange;
        var raw = animal.InitialWeightKg + animal.AdgKgPerDay * days + noise;
        return isGoat ? Math.Round(raw, 1) : Math.Round(raw * 2m, MidpointRounding.AwayFromZero) / 2m;
    }

    /// <summary>
    /// The outcome of one feeding entry. About 1 in 25 is adjusted (some feed left over) and about
    /// 1 in 90 skipped, so the feeding screens show the ordinary mix of a real farm.
    /// </summary>
    public DemoFeedingOutcome FeedingOutcomeFor(DateOnly day, string tag, int line)
    {
        var h = Hash(tag + "#" + line.ToString(CultureInfo.InvariantCulture), day.DayNumber);
        if (h % 90 == 7)
            return new DemoFeedingOutcome(DemoFeedingAction.Skip, 0m, "Animal off feed, vet advised rest");
        if (h % 25 == 3)
            return new DemoFeedingOutcome(DemoFeedingAction.Adjust, 0.85m + h % 10 / 100m, "Some feed left in the trough");
        return new DemoFeedingOutcome(DemoFeedingAction.Confirm, 1m, null);
    }

    /// <summary>Builds the demo farm for <paramref name="today"/>.</summary>
    public static DemoSeedPlan Create(DateOnly today)
    {
        DateOnly D(int offset) => today.AddDays(offset);
        var start = D(-FarmAgeDays);
        var feedStart = D(-FeedingHistoryDays);

        // ── Breeds (ADG targets in kg/day) ────────────────────────────────────
        var breeds = new List<DemoBreedSpec>
        {
            new("deshi", "Local (Deshi)", "Cattle", "Bangladesh", "Beef", "Low-input fattening, hardy in local conditions",
                0.30m, 0.45m, 0.60m, 0.70m, 0.45m, 0.65m, 7.5m, 9.5m),
            new("sahiwal", "Sahiwal Cross", "Cattle", "Pakistan / India (crossbred locally)", "Dual Purpose", "Eid fattening, good feed conversion",
                0.40m, 0.60m, 0.75m, 0.90m, 0.60m, 0.85m, 6.5m, 8.5m),
            new("brahman", "Brahman Cross", "Cattle", "USA (crossbred locally)", "Beef", "Heavy Eid bulls, heat tolerant",
                0.45m, 0.65m, 0.85m, 1.00m, 0.70m, 0.95m, 6.0m, 8.0m),
            new("blackbengal", "Black Bengal Goat", "Goat", "Bangladesh", "Meat", "Meat and skin, prolific",
                0.03m, 0.05m, 0.07m, 0.09m, 0.05m, 0.09m, 7.0m, 9.0m),
        };

        var sheds = new List<DemoShedSpec>
        {
            new("SH-A", "Fattening Shed A", 16, "Cattle"),
            new("SH-B", "Fattening Shed B", 22, "Mixed"),
        };

        var pens = new List<DemoPenSpec>
        {
            new("SH-A", "A1", "Pen A1 (Heavy bulls)", 8, "Fattening bulls"),
            new("SH-A", "A2", "Pen A2 (Eid bulls)", 8, "Fattening bulls"),
            new("SH-B", "B1", "Pen B1 (Winter cattle)", 12, "Growing cattle"),
            new("SH-B", "B2", "Pen B2 (Goats)", 10, "Goats"),
        };

        var batches = new List<DemoBatchSpec>
        {
            new("eid", "Eid-ul-Adha 2027 Fattening", "Heavy Sahiwal and Brahman cross bulls for the Eid-ul-Adha market."),
            new("winter", "Winter Batch", "Local and Sahiwal cross cattle plus Black Bengal goats bought for winter fattening."),
        };

        // ── Animals: (tag, breed, sex, age in months, bought N days ago, price, weight, ADG, pen) ──
        var animals = new List<DemoAnimalSpec>();
        void Cattle(string tag, string breed, AnimalSex sex, int ageMonths, int boughtDaysAgo, decimal price, decimal weight, decimal adg, string pen, string batch)
        {
            var bought = D(-boughtDaysAgo);
            animals.Add(new DemoAnimalSpec(
                tag, AnimalSpecies.CattleBeef, breed, sex, bought.AddMonths(-ageMonths), bought, price, weight, adg,
                pen.StartsWith('A') ? "SH-A" : "SH-B", pen, batch, adg < 0.45m));
        }

        Cattle("SAF-C01", "brahman", AnimalSex.Male, 26, 150, 92_000m, 258m, 0.86m, "A1", "eid");
        Cattle("SAF-C02", "brahman", AnimalSex.Male, 24, 150, 88_000m, 247m, 0.82m, "A1", "eid");
        Cattle("SAF-C03", "brahman", AnimalSex.Male, 27, 149, 95_000m, 265m, 0.88m, "A1", "eid");
        Cattle("SAF-C04", "sahiwal", AnimalSex.Male, 24, 149, 78_000m, 218m, 0.74m, "A1", "eid");
        Cattle("SAF-C05", "sahiwal", AnimalSex.Male, 22, 148, 74_000m, 207m, 0.71m, "A1", "eid");
        Cattle("SAF-C06", "sahiwal", AnimalSex.Male, 25, 148, 82_000m, 226m, 0.77m, "A1", "eid");
        Cattle("SAF-C07", "brahman", AnimalSex.Male, 25, 147, 90_000m, 252m, 0.84m, "A1", "eid");
        Cattle("SAF-C08", "brahman", AnimalSex.Male, 26, 146, 93_000m, 261m, 0.80m, "A2", "eid");
        Cattle("SAF-C09", "sahiwal", AnimalSex.Male, 23, 146, 76_000m, 212m, 0.69m, "A2", "eid");
        Cattle("SAF-C10", "sahiwal", AnimalSex.Male, 24, 145, 80_000m, 221m, 0.75m, "A2", "eid");
        Cattle("SAF-C11", "brahman", AnimalSex.Male, 24, 144, 86_000m, 241m, 0.79m, "A2", "eid");
        Cattle("SAF-C12", "sahiwal", AnimalSex.Male, 26, 143, 88_000m, 236m, 0.33m, "A2", "eid");
        Cattle("SAF-C13", "brahman", AnimalSex.Male, 25, 142, 91_000m, 255m, 0.85m, "A2", "eid");
        Cattle("SAF-C14", "sahiwal", AnimalSex.Male, 22, 140, 72_000m, 203m, 0.36m, "A2", "eid");

        Cattle("SAF-C15", "deshi", AnimalSex.Male, 20, 135, 58_000m, 164m, 0.58m, "B1", "winter");
        Cattle("SAF-C16", "deshi", AnimalSex.Male, 22, 134, 62_000m, 172m, 0.62m, "B1", "winter");
        Cattle("SAF-C17", "deshi", AnimalSex.Male, 19, 133, 55_000m, 158m, 0.55m, "B1", "winter");
        Cattle("SAF-C18", "sahiwal", AnimalSex.Male, 21, 132, 70_000m, 196m, 0.70m, "B1", "winter");
        Cattle("SAF-C19", "deshi", AnimalSex.Male, 20, 130, 60_000m, 168m, 0.57m, "B1", "winter");
        Cattle("SAF-C20", "deshi", AnimalSex.Female, 24, 129, 56_000m, 160m, 0.52m, "B1", "winter");
        Cattle("SAF-C21", "sahiwal", AnimalSex.Male, 20, 127, 68_000m, 191m, 0.68m, "B1", "winter");
        Cattle("SAF-C22", "deshi", AnimalSex.Male, 21, 125, 63_000m, 175m, 0.31m, "B1", "winter");
        Cattle("SAF-C23", "deshi", AnimalSex.Male, 18, 123, 57_000m, 162m, 0.60m, "B1", "winter");
        Cattle("SAF-C24", "sahiwal", AnimalSex.Male, 22, 120, 72_000m, 199m, 0.73m, "B1", "winter");

        void Goat(string tag, AnimalSex sex, int ageMonths, int boughtDaysAgo, decimal price, decimal weight, decimal adg)
        {
            var bought = D(-boughtDaysAgo);
            animals.Add(new DemoAnimalSpec(
                tag, AnimalSpecies.Goat, "blackbengal", sex, bought.AddMonths(-ageMonths), bought, price, weight, adg,
                "SH-B", "B2", "winter", false));
        }

        Goat("SAF-G01", AnimalSex.Male, 9, 128, 12_500m, 16.5m, 0.075m);
        Goat("SAF-G02", AnimalSex.Male, 10, 128, 13_500m, 17.8m, 0.080m);
        Goat("SAF-G03", AnimalSex.Female, 8, 126, 9_500m, 13.2m, 0.060m);
        Goat("SAF-G04", AnimalSex.Male, 9, 126, 11_000m, 15.0m, 0.070m);

        // ── Exits: 3 sales at a profit, 1 at a loss (one on credit), 1 death ────
        var sales = new List<DemoSaleSpec>();
        void Sale(string tag, int daysAgo, decimal pricePerKg, string buyer, decimal? paidNow = null, string? phone = null, params DemoPaymentSpec[] collections)
        {
            var animal = animals.First(a => a.Tag == tag);
            var date = D(-daysAgo);
            var weight = WeightOn(animal, date);
            var price = Math.Round(weight * pricePerKg / 500m, MidpointRounding.AwayFromZero) * 500m;
            sales.Add(new DemoSaleSpec(tag, date, price, buyer, weight, paidNow, phone, collections));
        }

        Sale("SAF-C03", 26, 430m, "Abdul Hakim (Gabtoli Haat)");
        Sale("SAF-C05", 18, 425m, "Mizanur Rahman, Mirpur");
        Sale("SAF-C08", 11, 430m, "Rahim Meat Traders", 100_000m, "+8801711000201", new DemoPaymentSpec(D(-4), 35_000m));
        // Slow grower sold early at a loss: kept eating, barely gained, price below what was paid.
        var slow = animals.First(a => a.Tag == "SAF-C12");
        var slowDate = D(-5);
        sales.Add(new DemoSaleSpec("SAF-C12", slowDate, 82_000m, "Local butcher, Savar Bazar", WeightOn(slow, slowDate), null, null, []));

        var death = new DemoDeathSpec("SAF-C19", D(-40), CauseOfDeath.Disease, "Haemorrhagic Septicaemia (HS)",
            "Sudden high fever and throat swelling; died despite antibiotic treatment.");

        DateOnly? Exit(string tag) => sales.FirstOrDefault(s => s.Tag == tag)?.Date ?? (death.Tag == tag ? death.Date : null);

        // ── Weighings every 14 days from purchase, plus a weighing on the sale day ──
        var weighings = new List<DemoWeighingSpec>();
        foreach (var animal in animals)
        {
            var exit = Exit(animal.Tag);
            for (var day = animal.PurchaseDate; day <= today && (exit is null || day < exit); day = day.AddDays(14))
            {
                weighings.Add(new DemoWeighingSpec(animal.Tag, day, WeightOn(animal, day)));
            }

            var sale = sales.FirstOrDefault(s => s.Tag == animal.Tag);
            if (sale is not null)
                weighings.Add(new DemoWeighingSpec(animal.Tag, sale.Date, sale.SaleWeightKg));
        }

        // ── Inventory ─────────────────────────────────────────────────────────
        var suppliers = new List<DemoSupplierSpec>
        {
            new("rupali", "Rupali Feed Traders", "Md. Shafiqul Islam", "+8801712000101", "Savar Bazar Road, Savar, Dhaka"),
            new("bhaibhai", "Bhai Bhai Agro Supplies", "Abul Kashem", "+8801812000102", "Baipail, Ashulia, Dhaka"),
            new("rahmanvet", "Rahman Vet Pharma", "Dr. Habibur Rahman", "+8801912000103", "Hemayetpur, Savar, Dhaka"),
        };

        var items = new List<DemoItemSpec>
        {
            new("wheatbran", "Wheat Bran (Gomer Bhushi)", InventoryCategory.Feed, "kg", 300m, 52m, new(FeedCategory.Byproduct, 89m, 15.5m, 10.5m, 10m, 0.13m, 1.10m)),
            new("ricepolish", "Rice Polish (Kura)", InventoryCategory.Feed, "kg", 200m, 38m, new(FeedCategory.Byproduct, 90m, 12.5m, 11.5m, 12m, 0.10m, 1.40m)),
            new("khesari", "Khesari Bran", InventoryCategory.Feed, "kg", 100m, 45m, new(FeedCategory.Concentrate, 89m, 18m, 10.8m, 14m, 0.20m, 0.40m)),
            new("moc", "Mustard Oil Cake (Khoil)", InventoryCategory.Feed, "kg", 200m, 58m, new(FeedCategory.Concentrate, 90m, 33m, 11.8m, 9m, 0.60m, 1.00m)),
            new("maize", "Maize Crushed", InventoryCategory.Feed, "kg", 200m, 42m, new(FeedCategory.Concentrate, 88m, 9m, 13.2m, 2.5m, 0.03m, 0.28m)),
            new("straw", "Rice Straw", InventoryCategory.Feed, "kg", 1_000m, 9m, new(FeedCategory.Forage, 90m, 3.5m, 6.0m, 33m, 0.20m, 0.08m)),
            new("grass", "Green Grass (Napier)", InventoryCategory.Feed, "kg", 1_500m, 4.5m, new(FeedCategory.Forage, 20m, 9m, 8.5m, 30m, 0.45m, 0.30m)),
            new("molasses", "Molasses (Chitagur)", InventoryCategory.Feed, "kg", 40m, 35m, new(FeedCategory.Additive, 75m, 4m, 11.0m, 0m, 0.80m, 0.08m)),
            new("salt", "Salt", InventoryCategory.Feed, "kg", 15m, 15m, new(FeedCategory.Mineral, 99m, 0m, 0m, 0m, 0m, 0m)),
            new("dcp", "DCP (Di-Calcium Phosphate)", InventoryCategory.Feed, "kg", 15m, 110m, new(FeedCategory.Mineral, 98m, 0m, 0m, 0m, 23m, 18m)),
            new("oxytet", "Oxytetracycline 20% LA Injection", InventoryCategory.Medicine, "ml", 100m, 3.6m, null),
            new("ivermectin", "Ivermectin 1% Injection", InventoryCategory.Medicine, "ml", 40m, 6.2m, null),
            new("fmd", "FMD Vaccine (Trivalent)", InventoryCategory.Vaccine, "dose", 10m, 65m, null),
            new("lsd", "LSD Vaccine (Lumpivax)", InventoryCategory.Vaccine, "dose", 10m, 85m, null),
        };

        var recipes = new List<DemoRecipeSpec>
        {
            new(ConcentrateRecipeKey, "Fattening Concentrate Mix", TargetAnimalType.Cattle, "Fattening",
                "Bran, oil cake and maize based concentrate fed twice a day.",
                [
                    new("wheatbran", 30m), new("ricepolish", 20m), new("khesari", 8m), new("moc", 20m),
                    new("maize", 18m), new("molasses", 2m), new("salt", 1m), new("dcp", 1m),
                ]),
            new(RoughageRecipeKey, "Roughage Mix (Straw + Napier)", TargetAnimalType.Cattle, "Fattening",
                "Chopped rice straw mixed with fresh Napier grass.",
                [new("straw", 35m), new("grass", 65m)]),
        };

        var charts = new List<DemoChartSpec>
        {
            new(CattleChartKey, "Cattle Fattening Chart (by weight)", TargetAnimalType.Cattle,
            [
                new(100m, 200m, 2.0m, 10.0m),
                new(200m, 250m, 2.5m, 12.0m),
                new(250m, 300m, 3.0m, 13.5m),
                new(300m, 600m, 3.5m, 15.0m),
            ]),
            new(GoatChartKey, "Goat Fattening Chart (by weight)", TargetAnimalType.Goat,
            [
                new(5m, 20m, 0.25m, 1.5m),
                new(20m, 60m, 0.35m, 2.2m),
            ]),
        };

        var penRations = new List<DemoPenRationSpec>
        {
            new("pen-eid", "Pen Ration - Eid Bulls (per head)", TargetAnimalType.Cattle, 3.0m, 13.0m, ["A1", "A2"]),
            new("pen-winter", "Pen Ration - Winter Cattle (per head)", TargetAnimalType.Cattle, 2.2m, 10.5m, ["B1"]),
            new("pen-goat", "Pen Ration - Goats (per head)", TargetAnimalType.Goat, 0.3m, 1.8m, ["B2"]),
        };

        // Purchases: medicines up front; feed in three rounds while feed is recorded, each sized
        // from the heaviest ration with a margin so stock never runs short; fresh grass every 10 days.
        var cattleCount = animals.Count(a => a.Species != AnimalSpecies.Goat);
        var goatCount = animals.Count(a => a.Species == AnimalSpecies.Goat);
        var dailyConcentrate = cattleCount * charts[0].Bands[^1].ConcentrateKg + goatCount * charts[1].Bands[^1].ConcentrateKg;
        var dailyRoughage = cattleCount * charts[0].Bands[^1].RoughageKg + goatCount * charts[1].Bands[^1].RoughageKg;
        decimal Need(string itemKey, int days, decimal margin)
        {
            var concentrate = recipes[0].Ingredients.FirstOrDefault(i => i.ItemKey == itemKey)?.Percent ?? 0m;
            var roughage = recipes[1].Ingredients.FirstOrDefault(i => i.ItemKey == itemKey)?.Percent ?? 0m;
            var daily = dailyConcentrate * concentrate / 100m + dailyRoughage * roughage / 100m;
            var quantity = daily * days * margin;
            var step = quantity >= 500m ? 50m : 5m;
            return Math.Ceiling(quantity / step) * step;
        }

        decimal Cost(string itemKey) => items.First(i => i.Key == itemKey).UnitCostBdt;

        var purchases = new List<DemoPurchaseSpec>
        {
            new(D(-149), "oxytet", 500m, Cost("oxytet"), "rahmanvet", "RVP-2201", null, []),
            new(D(-149), "ivermectin", 200m, Cost("ivermectin"), "rahmanvet", "RVP-2201", null, []),
            new(D(-149), "fmd", 40m, Cost("fmd"), "rahmanvet", "RVP-2202", null, []),
            new(D(-149), "lsd", 40m, Cost("lsd"), "rahmanvet", "RVP-2202", null, []),
        };

        var rounds = new[] { (Offset: -63, Days: 26, Invoice: "RFT-1041"), (Offset: -38, Days: 20, Invoice: "RFT-1078"), (Offset: -18, Days: 25, Invoice: "RFT-1112") };
        var concentrateItems = new[] { "wheatbran", "ricepolish", "khesari", "moc", "maize", "molasses", "salt", "dcp" };
        for (var r = 0; r < rounds.Length; r++)
        {
            var round = rounds[r];
            var date = D(round.Offset);
            foreach (var key in concentrateItems.Append("straw"))
            {
                // Molasses and DCP are not re-ordered in the last round, so they show as low stock.
                if (r == 2 && key is "molasses" or "dcp")
                    continue;

                var quantity = Need(key, round.Days, 1.35m);
                var supplier = key is "maize" or "straw" ? "bhaibhai" : "rupali";
                var invoice = supplier == "rupali" ? round.Invoice : $"BBA-{560 + r}";
                var unitCost = Cost(key) * (1m + r * 0.02m); // prices creep up a little each round

                // Round 1: bran and oil cake on credit, half paid; paid down later. Round 2: maize on credit.
                decimal? paidNow = null;
                var later = new List<DemoPaymentSpec>();
                if (r == 0 && key is "wheatbran" or "moc")
                {
                    paidNow = Math.Round(quantity * unitCost * 0.5m, 0);
                    later.Add(new DemoPaymentSpec(date.AddDays(20), Math.Round(quantity * unitCost * 0.3m, 0)));
                }
                else if (r == 1 && key == "maize")
                {
                    paidNow = 0m;
                    later.Add(new DemoPaymentSpec(date.AddDays(15), Math.Round(quantity * unitCost * 0.5m, 0)));
                }

                purchases.Add(new DemoPurchaseSpec(date, key, quantity, Math.Round(unitCost, 2), supplier, invoice, paidNow, later));
            }
        }

        for (var offset = -FeedingHistoryDays - 1; offset <= -1; offset += 10)
        {
            purchases.Add(new DemoPurchaseSpec(D(offset), "grass", Need("grass", 10, 1.25m), Cost("grass"), "bhaibhai",
                $"BBA-G{offset + 100:00}", null, []));
        }

        // ── Health ────────────────────────────────────────────────────────────
        var protocolSteps = new List<DemoProtocolStep>
        {
            new("FMD primary dose", 0, "FMD Vaccine (Trivalent)", "fmd", 1m),
            new("LSD dose", 14, "LSD Vaccine (Lumpivax)", "lsd", 1m),
            new("FMD booster", 180, "FMD Vaccine (Trivalent)", "fmd", 1m),
        };

        var incident = new DemoIncidentSpec(D(-43), "Haemorrhagic Septicaemia (HS)", IncidentSeverity.Severe, "SH-B",
            "High fever, swelling of the throat and brisket, laboured breathing", 3, D(-30));

        var treatments = new List<DemoTreatmentSpec>
        {
            new("SAF-C15", D(-110), "Internal parasites (routine deworming)", "ivermectin", 3.5m, "ml", 35, 650m, "Dr. Habibur Rahman", "Deworming on arrival.", TreatmentStatus.Completed, D(-110)),
            new("SAF-C07", D(-95), "Pneumonia (respiratory infection)", "oxytet", 15m, "ml", 28, 1_200m, "Dr. Habibur Rahman", "3-day course, recovered well.", TreatmentStatus.Completed, D(-91)),
            new("SAF-G02", D(-80), "Internal parasites (routine deworming)", "ivermectin", 0.4m, "ml", 35, 250m, "Dr. Habibur Rahman", "Goat deworming.", TreatmentStatus.Completed, D(-80)),
            new("SAF-C22", D(-50), "Foot rot", "oxytet", 12m, "ml", 28, 900m, "Dr. Habibur Rahman", "Hoof cleaned and dressed, antibiotic given.", TreatmentStatus.Completed, D(-46)),
            new("SAF-C19", D(-43), "Haemorrhagic Septicaemia (HS)", "oxytet", 20m, "ml", 28, 1_500m, "Dr. Nusrat Jahan", "Emergency treatment during the HS outbreak.", TreatmentStatus.Failed, D(-40)),
            new("SAF-C16", D(-43), "Haemorrhagic Septicaemia (HS)", "oxytet", 18m, "ml", 28, 1_400m, "Dr. Nusrat Jahan", "Responded to treatment.", TreatmentStatus.Completed, D(-38)),
            new("SAF-C21", D(-42), "Haemorrhagic Septicaemia (HS)", "oxytet", 18m, "ml", 28, 1_400m, "Dr. Nusrat Jahan", "Responded to treatment.", TreatmentStatus.Completed, D(-37)),
        };

        var vetVisit = new DemoVetVisitSpec(D(-43), "Dr. Nusrat Jahan (Upazila Livestock Office)", VetVisitType.Emergency,
            "HS outbreak in Shed B", "Three cattle with high fever and throat swelling, one critical.",
            "Isolate sick animals, antibiotic course, vaccinate the herd against HS next month.", 2_500m, D(14));

        // ── Finance ───────────────────────────────────────────────────────────
        var expenses = new List<DemoExpenseSpec>();
        var periods = new List<DemoPeriod>();
        for (var month = new DateOnly(start.Year, start.Month, 1); month <= today; month = month.AddMonths(1))
        {
            var last = month.AddMonths(1).AddDays(-1);
            var label = month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            var refSuffix = month.ToString("yyyyMM", CultureInfo.InvariantCulture);
            var from = month < start ? start : month;

            if (last < today)
            {
                // Three workers at 13,000 a month, prorated for the first, partial month.
                var share = (decimal)(last.DayNumber - from.DayNumber + 1) / (last.DayNumber - month.DayNumber + 1);
                expenses.Add(new DemoExpenseSpec(last, "LaborCost", Math.Round(39_000m * share, 0),
                    $"Farm labour salaries (3 workers) - {label}", $"SAL-{refSuffix}"));
                periods.Add(new DemoPeriod(from, last));
            }

            var bill = month.AddDays(24);
            if (bill >= start && bill < today)
                expenses.Add(new DemoExpenseSpec(bill, "Utilities", 4_200m + month.Month * 35m, $"Electricity bill (Palli Bidyut) - {label}", $"ELEC-{refSuffix}"));

            foreach (var (day, amount) in new[] { (5, 3_500m), (20, 4_800m) })
            {
                var date = month.AddDays(day);
                if (date >= start && date < today)
                    expenses.Add(new DemoExpenseSpec(date, "Transport", amount, "Feed and fodder transport (pickup van)", $"TRN-{refSuffix}-{day + 1:00}"));
            }
        }

        var loan = new DemoLoanSpec("Bangladesh Krishi Bank, Savar", 500_000m, 9m, D(-145), "Monthly",
            "Working-capital loan for the Eid fattening batch.", [new DemoPaymentSpec(D(-25), 60_000m)]);

        var investor = new DemoInvestorSpec("Md. Karim Uddin", 400_000m, D(-146), 30m, "+8801711000301",
            "Silent partner in the Eid batch; 30% of the profit.");

        return new DemoSeedPlan
        {
            Today = today,
            StartDate = start,
            BooksStartDate = start.AddDays(-2),
            FeedingStartDate = feedStart,
            IndividualPlansStartDate = D(-IndividualChartDays),
            OpeningCashBdt = 2_800_000m,
            OpeningOwnerCapitalBdt = 2_800_000m,
            Breeds = breeds,
            Sheds = sheds,
            Pens = pens,
            Batches = batches,
            Animals = animals,
            Weighings = weighings.OrderBy(w => w.Date).ToList(),
            Suppliers = suppliers,
            Items = items,
            Purchases = purchases.OrderBy(p => p.Date).ToList(),
            Recipes = recipes,
            Charts = charts,
            PenRations = penRations,
            ProtocolTitle = "Fattening Cattle Vaccination (FMD + LSD)",
            ProtocolSteps = protocolSteps,
            MissedVaccinations = [("SAF-C20", "LSD dose"), ("SAF-C23", "LSD dose")],
            Treatments = treatments,
            Incident = incident,
            VetVisit = vetVisit,
            Death = death,
            Sales = sales,
            Expenses = expenses.OrderBy(e => e.Date).ToList(),
            OverheadPeriods = periods,
            Loan = loan,
            Investor = investor,
        };
    }

    /// <summary>A small, stable, non-cryptographic hash (FNV-1a) so the "random" parts never change between runs.</summary>
    private static int Hash(string text, int salt)
    {
        unchecked
        {
            var hash = (uint)2166136261;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619;
            }

            hash = (hash ^ (uint)salt) * 16777619;
            hash ^= hash >> 15;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
