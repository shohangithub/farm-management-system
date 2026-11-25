using System.Diagnostics;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Commands;
using Farm360.Application.Farms.Pens.Commands;
using Farm360.Application.Farms.Sheds.Commands;
using Farm360.Application.Feeding.Commands.AnimalFeedingPlans;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Commands.FeedFormulas;
using Farm360.Application.Feeding.Commands.FeedIngredients;
using Farm360.Application.Feeding.Commands.FeedingRuleSets;
using Farm360.Application.Feeding.Jobs;
using Farm360.Application.Finance.Commands;
using Farm360.Application.Health.Commands.DiseaseIncidents;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Application.Health.Commands.MortalityRecords;
using Farm360.Application.Health.Commands.VaccinationEvents;
using Farm360.Application.Health.Commands.VaccinationProtocols;
using Farm360.Application.Health.Commands.VetVisits;
using Farm360.Application.Inventory.Commands.InventoryItems;
using Farm360.Application.Inventory.Commands.StockTransactions;
using Farm360.Application.Inventory.Commands.Suppliers;
using Farm360.Application.Livestock.Commands;
using Farm360.Application.Organizations.Branches.Commands;
using Farm360.Contracts.Finance;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Demo;

/// <summary>
/// Fills a new demo tenant by sending the REAL MediatR commands, as the tenant's owner, in date
/// order: purchases, animals, weighings, vaccinations, treatments, sales, expenses and every day's
/// feeding. Stock movements, the animal cost ledger, finance postings, payables and receivables
/// therefore stay consistent exactly as for a real farm. Each step runs in its own DI scope (fresh
/// DbContext), with the tenant and the owner set on it the way a request would set them.
/// </summary>
public sealed class DemoSeeder(IServiceScopeFactory scopeFactory, ILogger<DemoSeeder> logger)
{
    public async Task SeedAsync(DemoTenantInfo tenant, Guid ownerUserId, DemoSeedPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(plan);

        var run = new DemoSeedRun(scopeFactory, tenant, ownerUserId, plan, logger);
        await run.ExecuteAsync(cancellationToken);
    }
}

internal sealed class DemoSeedRun(
    IServiceScopeFactory scopeFactory,
    DemoTenantInfo tenant,
    Guid ownerUserId,
    DemoSeedPlan plan,
    ILogger logger)
{
    private const string OwnerRole = "Owner";

    private readonly Dictionary<string, Guid> _sheds = [];
    private readonly Dictionary<string, Guid> _pens = [];
    private readonly Dictionary<string, Guid> _breeds = [];
    private readonly Dictionary<string, Guid> _batches = [];
    private readonly Dictionary<string, Guid> _animals = [];
    private readonly Dictionary<string, Guid> _items = [];
    private readonly Dictionary<string, Guid> _suppliers = [];
    private readonly Dictionary<string, Guid> _recipes = [];
    private readonly Dictionary<string, Guid> _charts = [];
    private readonly Dictionary<Guid, string> _planTags = [];
    private readonly Dictionary<Guid, int> _formulaLines = [];
    private readonly Dictionary<Guid, string> _protocolSteps = [];
    private readonly List<(DateOnly Date, Guid EventId, string Tag)> _vaccinationsDue = [];
    private readonly Dictionary<(string Tag, DateOnly Date), Guid> _treatments = [];
    private readonly List<(DateOnly Date, Guid BillId, decimal Amount, string Method)> _billPayments = [];

    private Guid _farmId;
    private Guid _protocolId;
    private Guid _incidentId;
    private Guid _loanId;
    private int _feedingDays;
    private int _entriesConfirmed;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();

        await SetupAsync(ct);
        Log("Demo seed: farm structure, master data and accounts set up in {Seconds:0.0}s.", clock.Elapsed.TotalSeconds);

        for (var day = plan.BooksStartDate; day <= plan.Today; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            await RunDayAsync(day, ct);
        }

        Log("Demo seed: timeline done in {Seconds:0.0}s ({Days} feeding days, {Entries} feeding entries confirmed).",
            clock.Elapsed.TotalSeconds, _feedingDays, _entriesConfirmed);

        foreach (var period in plan.OverheadPeriods)
        {
            await InScopeAsync((s, _) => s.Send(new AllocateOverheadCommand(_farmId, period.From, period.To), ct));
        }

        Log("Demo seed: completed in {Seconds:0.0}s.", clock.Elapsed.TotalSeconds);
    }

    // ── Scope plumbing ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs one unit of work in a fresh scope acting as the demo owner: the ambient tenant (EF query
    /// filter, handlers) and the current user (audit columns, cross-tenant guard) are both the demo's.
    /// </summary>
    private async Task<T> InScopeAsync<T>(Func<ISender, IServiceProvider, Task<T>> work)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantService>().SetTenant(tenant.TenantId, tenant.Slug, tenant.Name, tenant.Tier, "Active");
        services.GetRequiredService<ICurrentUserOverride>().Set(ownerUserId, tenant.TenantId, OwnerRole, tenant.Tier);
        return await work(services.GetRequiredService<ISender>(), services);
    }

    private async Task InScopeAsync(Func<ISender, IServiceProvider, Task> work)
    {
        await InScopeAsync<bool>(async (s, sp) =>
        {
            await work(s, sp);
            return true;
        });
    }

    private void Log(string message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
#pragma warning disable CA2254 // Templates are constant strings passed by this class.
            logger.LogInformation(message, args);
#pragma warning restore CA2254
        }
    }

    // ── 1. Structure, master data, accounts ───────────────────────────────────

    private async Task SetupAsync(CancellationToken ct)
    {
        var branchId = await InScopeAsync((s, _) => s.Send(new CreateBranchCommand(
            tenant.OrganizationId, "SAV-01", DemoConstants.BranchName, "savar@shapla-agro.demo", "+8801700000100",
            "Hemayetpur, Savar", "Savar", "Dhaka", "Bangladesh", "1340", 23.7956, 90.2587, "Sat-Thu 07:00-18:00", null, true), ct));

        _farmId = await InScopeAsync((s, _) => s.Send(new CreateFarmCommand(
            branchId, "SAF-01", DemoConstants.FarmName, FarmType.Mixed, 1.5, 1.2, 23.7961, 90.2602, null, 60,
            null, null, "Sample cattle and goat fattening farm with sheds, feeding, health and accounts filled in."), ct));

        foreach (var shed in plan.Sheds)
        {
            _sheds[shed.Number] = await InScopeAsync((s, _) => s.Send(new CreateShedCommand(
                _farmId, shed.Number, shed.Name, shed.Capacity, shed.AnimalType, "Concrete", "Tin", true, true, false), ct));
        }

        foreach (var pen in plan.Pens)
        {
            _pens[pen.Number] = await InScopeAsync((s, _) => s.Send(new CreatePenCommand(
                _sheds[pen.ShedNumber], pen.Number, pen.Name, pen.Capacity, pen.AnimalGroup, null), ct));
        }

        // Accounts: financial year (July-June) and books start, then the opening position.
        await InScopeAsync(async (s, _) =>
        {
            await s.Send(new SaveAccountingSettingsCommand(tenant.TenantId, _farmId,
                new SaveAccountingSettingsRequest(plan.FiscalYearStartMonth, plan.BooksStartDate.ToDateTime(TimeOnly.MinValue))), ct);
        });
        await InScopeAsync(async (s, _) =>
        {
            var date = plan.BooksStartDate.ToDateTime(TimeOnly.MinValue);
            await s.Send(new CreateBalanceSheetEntryCommand(tenant.TenantId, _farmId,
                new SaveBalanceSheetEntryRequest("Opening", date, "1010", plan.OpeningCashBdt, "Opening cash and bank balance")), ct);
            await s.Send(new CreateBalanceSheetEntryCommand(tenant.TenantId, _farmId,
                new SaveBalanceSheetEntryRequest("Opening", date, "3200", plan.OpeningOwnerCapitalBdt, "Owner's capital brought in")), ct);
        });

        await InScopeAsync(async (s, sp) =>
        {
            var breeds = sp.GetRequiredService<IBreedRepository>();
            foreach (var b in plan.Breeds)
            {
                var existing = await breeds.GetByNameAsync(b.Name, ct);
                _breeds[b.Key] = existing?.Id ?? (await s.Send(new CreateBreedCommand(
                    b.Name, $"{b.Name} - {b.BestFor}.", b.Category, b.Origin, b.MainPurpose, b.BestFor,
                    b.AdgPoor, b.AdgAverage, b.AdgGood, b.AdgIntensive, b.StandardAdgMin, b.StandardAdgMax,
                    b.FcrMin, b.FcrMax, 0m, 0m, 0m, 0m), ct)).Id;
            }
        });

        await InScopeAsync(async (s, _) =>
        {
            foreach (var supplier in plan.Suppliers)
            {
                _suppliers[supplier.Key] = await s.Send(new CreateSupplierCommand(
                    supplier.Name, supplier.ContactPerson, supplier.Phone, null, supplier.Address, null), ct);
            }

            foreach (var item in plan.Items)
            {
                _items[item.Key] = await s.Send(new CreateInventoryItemCommand(
                    _farmId, item.Name, item.Category, item.Unit, item.ReorderThreshold,
                    Sku: "SAF-" + item.Key.ToUpperInvariant(), StorageLocation: item.Nutrition is null ? "Medicine cabinet" : "Feed store"), ct);
            }
        });

        // Feed: every stock feed is a ration ingredient linked to its stock item; two recipes; two charts.
        var ingredients = new Dictionary<string, Guid>();
        await InScopeAsync(async (s, _) =>
        {
            foreach (var item in plan.Items.Where(i => i.Nutrition is not null))
            {
                var n = item.Nutrition!;
                ingredients[item.Key] = await s.Send(new CreateFeedIngredientCommand(
                    item.Name, n.Category, n.DryMatterPct, n.CrudeProteinPct, n.MeMjPerKg, n.CrudeFiberPct, n.CalciumPct,
                    n.PhosphorusPct, item.UnitCostBdt, "kg", null, _items[item.Key]), ct);
            }

            foreach (var recipe in plan.Recipes)
            {
                _recipes[recipe.Key] = await s.Send(new CreateFeedFormulaCommand(
                    recipe.Title, recipe.Species, recipe.Stage, recipe.Description,
                    recipe.Ingredients.Select(i => new FormulaIngredientRequest(ingredients[i.ItemKey], i.Percent)).ToList()), ct);
            }
        });

        _formulaLines[_recipes[DemoSeedPlan.ConcentrateRecipeKey]] = 0;
        _formulaLines[_recipes[DemoSeedPlan.RoughageRecipeKey]] = 1;

        await InScopeAsync(async (s, _) =>
        {
            foreach (var chart in plan.Charts)
            {
                var lines = new List<FeedingRuleLineRequest>();
                foreach (var band in chart.Bands)
                {
                    lines.Add(new FeedingRuleLineRequest(band.MinKg, band.MaxKg, null, null, FeedCategory.Concentrate, band.ConcentrateKg,
                        FormulaId: _recipes[DemoSeedPlan.ConcentrateRecipeKey], SessionsPerDay: 2));
                    lines.Add(new FeedingRuleLineRequest(band.MinKg, band.MaxKg, null, null, FeedCategory.Forage, band.RoughageKg,
                        FormulaId: _recipes[DemoSeedPlan.RoughageRecipeKey], SessionsPerDay: 2));
                }

                _charts[chart.Key] = await s.Send(new CreateFeedingRuleSetCommand(
                    chart.Name, FeedingPlanType.WeightQuantity, chart.Species, FeedingPurpose.Fattening,
                    BaseNotes: "Concentrate twice a day, roughage morning and evening, clean water always.", Lines: lines), ct);
            }

            foreach (var ration in plan.PenRations)
            {
                _charts[ration.Key] = await s.Send(new CreateFeedingRuleSetCommand(
                    ration.Name, FeedingPlanType.FixedQuantity, ration.Species, FeedingPurpose.Fattening,
                    BaseNotes: "Group ration per head, used before each animal moved to its own weight-based chart.",
                    Lines:
                    [
                        new FeedingRuleLineRequest(null, null, null, null, FeedCategory.Concentrate, ration.ConcentrateKgPerHead,
                            FormulaId: _recipes[DemoSeedPlan.ConcentrateRecipeKey], SessionsPerDay: 2),
                        new FeedingRuleLineRequest(null, null, null, null, FeedCategory.Forage, ration.RoughageKgPerHead,
                            FormulaId: _recipes[DemoSeedPlan.RoughageRecipeKey], SessionsPerDay: 2),
                    ]), ct);
            }

            _protocolId = await s.Send(new CreateVaccinationProtocolCommand(
                plan.ProtocolTitle, AnimalSpecies.CattleBeef, "FMD and LSD for newly bought fattening cattle.", false,
                plan.ProtocolSteps.Select(step => new CreateVaccinationProtocolStepDto(
                    step.StepName, step.Day, step.VaccineName, "1 dose (2 ml) subcutaneous", _items[step.ItemKey], step.Dose)).ToList()), ct);

            foreach (var batch in plan.Batches)
            {
                _batches[batch.Key] = await s.Send(new CreateBatchCommand(_farmId, batch.Name, batch.Notes), ct);
            }
        });

        await InScopeAsync(async (_, sp) =>
        {
            var protocol = await sp.GetRequiredService<IVaccinationRepository>().GetProtocolByIdAsync(_protocolId, ct)
                ?? throw new InvalidOperationException("The demo vaccination protocol was not saved.");
            foreach (var step in protocol.Steps)
                _protocolSteps[step.Id] = step.StepName;
        });
    }

    // ── 2. One day of the farm's life ─────────────────────────────────────────

    private async Task RunDayAsync(DateOnly day, CancellationToken ct)
    {
        var at = day.ToDateTime(new TimeOnly(10, 0));

        if (plan.Investor.Date == day)
        {
            var i = plan.Investor;
            await InScopeAsync((s, _) => s.Send(new CreateInvestorCommand(
                tenant.TenantId, _farmId, i.Name, i.AmountBdt, at, i.ProfitSharePercent, null, i.Phone, null, i.Notes, "INV-001"), ct));
        }

        if (plan.Loan.Date == day)
        {
            var l = plan.Loan;
            _loanId = (await InScopeAsync((s, _) => s.Send(new CreateLoanRecordCommand(
                tenant.TenantId, _farmId, l.Lender, l.PrincipalBdt, l.InterestRatePercent, at, l.Schedule, l.Notes), ct))).Id;
        }

        foreach (var repayment in plan.Loan.Repayments.Where(r => r.Date == day))
        {
            await InScopeAsync((s, _) => s.Send(new RecordLoanRepaymentCommand(
                tenant.TenantId, _farmId, _loanId, repayment.AmountBdt, at, "BKB-REP-01", "Monthly instalment"), ct));
        }

        await PurchasesAsync(day, ct);
        await AnimalsBoughtAsync(day, ct);
        await VaccinationsAsync(day, ct);

        foreach (var weighing in plan.Weighings.Where(w => w.Date == day))
        {
            await InScopeAsync((s, _) => s.Send(new RecordWeightCommand(
                _animals[weighing.Tag], weighing.WeightKg, day, "Fortnightly weighing"), ct));
        }

        await HealthAsync(day, ct);

        foreach (var expense in plan.Expenses.Where(e => e.Date == day))
        {
            await InScopeAsync((s, _) => s.Send(new RecordExpenseCommand(
                tenant.TenantId, _farmId, expense.Category, expense.AmountBdt, at, expense.Description, expense.Reference,
                string.Empty, null, null, null), ct));
        }

        await ExitsAsync(day, ct);

        foreach (var payment in _billPayments.Where(p => p.Date == day))
        {
            await InScopeAsync((s, _) => s.Send(new RecordBillPaymentCommand(
                _farmId, payment.BillId, new RecordBillPaymentRequest(payment.Amount, day, payment.Method)), ct));
        }

        if (day == plan.FeedingStartDate)
            await AssignPenRationsAsync(day, ct);

        if (day == plan.IndividualPlansStartDate)
            await AssignFeedingPlansAsync(day, ct);

        if (day >= plan.FeedingStartDate && day < plan.Today)
            await FeedDayAsync(day, ct);
    }

    private async Task PurchasesAsync(DateOnly day, CancellationToken ct)
    {
        foreach (var purchase in plan.Purchases.Where(p => p.Date == day))
        {
            var supplier = plan.Suppliers.First(x => x.Key == purchase.SupplierKey);
            var credit = purchase.OnCredit
                ? new CreditTermsRequest(supplier.Name, supplier.Phone, day.AddDays(30), purchase.CreditPaidNowBdt!.Value, "Cash")
                : null;

            var transactionId = await InScopeAsync((s, _) => s.Send(new RecordStockInCommand(
                _farmId, _items[purchase.ItemKey], purchase.Quantity, purchase.UnitCostBdt, day,
                _suppliers[purchase.SupplierKey], purchase.InvoiceNumber, Credit: credit), ct));

            if (credit is null || purchase.LaterPayments.Count == 0)
                continue;

            var billId = await FindBillAsync(transactionId, ct);
            foreach (var payment in purchase.LaterPayments)
                _billPayments.Add((payment.Date, billId, payment.AmountBdt, "Bank transfer"));
        }
    }

    private async Task<Guid> FindBillAsync(Guid sourceDocumentId, CancellationToken ct) =>
        await InScopeAsync(async (_, sp) =>
        {
            var bills = await sp.GetRequiredService<ITradeBillRepository>().GetByFarmIdAsync(_farmId, cancellationToken: ct);
            return bills.FirstOrDefault(b => b.SourceDocumentId == sourceDocumentId)?.Id
                ?? throw new InvalidOperationException("The credit bill for a demo purchase or sale was not created.");
        });

    private async Task AnimalsBoughtAsync(DateOnly day, CancellationToken ct)
    {
        var bought = plan.Animals.Where(a => a.PurchaseDate == day).ToList();
        foreach (var a in bought)
        {
            var dto = await InScopeAsync((s, _) => s.Send(new RegisterAnimalCommand(
                _farmId, a.Tag, TagType.EarTag, a.Species, _breeds[a.BreedKey], a.Sex, a.DateOfBirth,
                AcquisitionType.Purchased, a.PurchaseDate, a.PriceBdt,
                $"Bought at {(a.Species == AnimalSpecies.Goat ? "Savar goat haat" : "Gabtoli cattle haat")}.",
                _sheds[a.ShedNumber], _pens[a.PenNumber]), ct));
            // Registration places the animal in its pen from the acquisition (purchase) date, so pen
            // history, group feeding and exits all line up with the day it arrived.
            _animals[a.Tag] = dto.Id;
        }

        foreach (var group in bought.GroupBy(a => a.BatchKey))
        {
            await InScopeAsync((s, _) => s.Send(new AssignAnimalsToBatchCommand(
                _batches[group.Key], group.Select(a => _animals[a.Tag]).ToList()), ct));
        }

        // Vaccination protocol for cattle a couple of days after they arrive.
        var enrol = plan.Animals
            .Where(a => a.Species != AnimalSpecies.Goat && a.PurchaseDate.AddDays(plan.ProtocolStartAfterPurchaseDays) == day)
            .ToList();
        if (enrol.Count == 0)
            return;

        await InScopeAsync((s, _) => s.Send(new AssignProtocolToAnimalsCommand(_protocolId, enrol.Select(a => _animals[a.Tag]).ToList(), day), ct));
        await InScopeAsync(async (_, sp) =>
        {
            var repository = sp.GetRequiredService<IVaccinationRepository>();
            foreach (var a in enrol)
            {
                foreach (var e in await repository.GetEventsByAnimalIdAsync(_animals[a.Tag], ct))
                {
                    var step = e.ProtocolStepId is { } stepId && _protocolSteps.TryGetValue(stepId, out var name) ? name : null;
                    if (plan.MissedVaccinations.Contains((a.Tag, step ?? string.Empty)))
                        continue;
                    _vaccinationsDue.Add((e.ScheduledDate, e.Id, a.Tag));
                }
            }
        });
    }

    private async Task VaccinationsAsync(DateOnly day, CancellationToken ct)
    {
        foreach (var (_, eventId, tag) in _vaccinationsDue.Where(v => v.Date == day && day < plan.Today).ToList())
        {
            if (!plan.IsOnFarm(plan.Animal(tag), day))
                continue;
            await InScopeAsync((s, _) => s.Send(new RecordVaccinationAdministrationCommand(eventId, day, "Given by farm staff"), ct));
        }
    }

    private async Task HealthAsync(DateOnly day, CancellationToken ct)
    {
        if (plan.Incident.Date == day)
        {
            var i = plan.Incident;
            _incidentId = await InScopeAsync((s, _) => s.Send(new ReportDiseaseIncidentCommand(
                _farmId, _sheds[i.ShedNumber], i.DiseaseName, i.Severity, day, i.Symptoms, i.AffectedAnimals,
                "Sick animals isolated; Upazila vet called."), ct));
        }

        if (plan.VetVisit.Date == day)
        {
            var v = plan.VetVisit;
            await InScopeAsync((s, _) => s.Send(new CreateVetVisitCommand(
                _farmId, v.VetName, day, v.VisitType, v.Purpose, v.Findings, v.Recommendations, v.CostBdt, v.NextVisitDate), ct));
        }

        foreach (var t in plan.Treatments.Where(t => t.Date == day))
        {
            _treatments[(t.Tag, t.Date)] = await InScopeAsync((s, _) => s.Send(new LogMedicalTreatmentCommand(
                _animals[t.Tag], t.Diagnosis, plan.Items.First(i => i.Key == t.ItemKey).Name, t.Dose, t.DoseUnit, 0,
                t.MeatWithdrawalDays, day, t.CostBdt, t.Veterinarian, t.Notes, _items[t.ItemKey], t.Dose), ct));
        }

        foreach (var t in plan.Treatments.Where(t => t.OutcomeDate == day))
        {
            await InScopeAsync((s, _) => s.Send(new UpdateTreatmentStatusCommand(_treatments[(t.Tag, t.Date)], t.Outcome, t.Notes), ct));
        }

        if (plan.Incident.ResolvedOn == day && _incidentId != Guid.Empty)
        {
            await InScopeAsync((s, _) => s.Send(new UpdateDiseaseIncidentCommand(
                _incidentId, IncidentStatus.Resolved, plan.Incident.AffectedAnimals, "Outbreak over; herd to be vaccinated against HS."), ct));
        }
    }

    private async Task ExitsAsync(DateOnly day, CancellationToken ct)
    {
        foreach (var sale in plan.Sales.Where(s => s.Date == day))
        {
            var credit = sale.OnCredit
                ? new CreditTermsRequest(sale.BuyerName, sale.BuyerPhone, day.AddDays(21), sale.CreditPaidNowBdt!.Value, "Cash",
                    "Balance to be paid after the buyer's Eid sales.")
                : null;
            await InScopeAsync((s, _) => s.Send(new SellAnimalCommand(
                _animals[sale.Tag], sale.PriceBdt, day, sale.BuyerName, sale.SaleWeightKg, credit), ct));

            if (credit is null || sale.Collections.Count == 0)
                continue;

            var billId = await FindBillAsync(_animals[sale.Tag], ct);
            foreach (var collection in sale.Collections)
                _billPayments.Add((collection.Date, billId, collection.AmountBdt, "bKash"));
        }

        if (plan.Death.Date == day)
        {
            var d = plan.Death;
            await InScopeAsync((s, _) => s.Send(new RecordMortalityCommand(
                _animals[d.Tag], day, d.Cause, d.DiseaseName, d.Notes, null, _incidentId == Guid.Empty ? null : _incidentId), ct));
        }
    }

    // ── 3. Feeding ────────────────────────────────────────────────────────────

    /// <summary>Group ration per pen until the day every animal gets its own weight-based chart.</summary>
    private async Task AssignPenRationsAsync(DateOnly day, CancellationToken ct)
    {
        var lastDay = plan.IndividualPlansStartDate.AddDays(-1);
        foreach (var ration in plan.PenRations)
        {
            foreach (var penNumber in ration.PenNumbers)
            {
                var pen = plan.Pens.First(p => p.Number == penNumber);
                var ids = await InScopeAsync((s, _) => s.Send(new AssignAnimalFeedingPlanCommand(
                    _farmId, [_charts[ration.Key]], FeedingPlanType.FixedQuantity, day, lastDay,
                    ShedId: _sheds[pen.ShedNumber], PenId: _pens[penNumber]), ct));
                foreach (var id in ids)
                    _planTags[id] = penNumber;
            }
        }
    }

    /// <summary>From here on every animal on the farm has its own plan on the weight-based chart.</summary>
    private async Task AssignFeedingPlansAsync(DateOnly day, CancellationToken ct)
    {
        foreach (var group in plan.Animals.Where(a => plan.IsOnFarm(a, day)).GroupBy(a => plan.ChartFor(a).Key))
        {
            await InScopeAsync((s, _) => s.Send(new AssignAnimalFeedingPlanCommand(
                _farmId, [_charts[group.Key]], FeedingPlanType.WeightQuantity, day, null,
                group.Select(a => _animals[a.Tag]).ToList()), ct));
        }

        var tags = _animals.ToDictionary(kv => kv.Value, kv => kv.Key);
        await InScopeAsync(async (_, sp) =>
        {
            var plans = await sp.GetRequiredService<IAnimalFeedingPlanRepository>()
                .GetActivePlansByFarmAsync(tenant.TenantId, _farmId, ct);
            foreach (var p in plans.Where(p => p.AnimalId.HasValue && tags.ContainsKey(p.AnimalId.Value)))
                _planTags[p.Id] = tags[p.AnimalId!.Value];
        });
    }

    /// <summary>
    /// Generates the day's entries with the real daily job command, then confirms them one by one
    /// (a few adjusted or skipped). Each confirmation deducts stock, books the cost and charges the
    /// animals, inside its own transaction, as when a farm worker confirms the Today page.
    /// </summary>
    private async Task FeedDayAsync(DateOnly day, CancellationToken ct)
    {
        _feedingDays++;
        await InScopeAsync((s, _) => s.Send(new CreateDailyFeedingEntriesCommand(day, _farmId), ct));

        await InScopeAsync(async (s, sp) =>
        {
            var entries = await sp.GetRequiredService<IDailyFeedingEntryRepository>()
                .GetEntriesByDateAsync(tenant.TenantId, _farmId, day, ct);

            foreach (var entry in entries.Where(e => e.Status == DailyFeedingEntryStatus.Pending).OrderBy(e => e.Id))
            {
                var tag = _planTags.GetValueOrDefault(entry.FeedingPlanId) ?? string.Empty;
                var outcome = plan.FeedingOutcomeFor(day, tag, _formulaLines.GetValueOrDefault(entry.FormulaId));
                switch (outcome.Action)
                {
                    case DemoFeedingAction.Skip:
                        await s.Send(new SkipDailyFeedingEntryCommand(entry.Id, outcome.Reason!), ct);
                        break;
                    case DemoFeedingAction.Adjust:
                        await s.Send(new AdjustDailyFeedingEntryCommand(entry.Id, Math.Round(entry.ExpectedKg * outcome.Factor, 2), outcome.Reason), ct);
                        _entriesConfirmed++;
                        break;
                    default:
                        await s.Send(new ConfirmDailyFeedingEntryCommand(entry.Id, entry.ExpectedKg), ct);
                        _entriesConfirmed++;
                        break;
                }
            }
        });
    }
}
