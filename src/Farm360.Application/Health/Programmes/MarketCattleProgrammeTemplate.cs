using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Health.Programmes;

/// <summary>
/// The vet's 108-day programme for cattle bought at the market (Dr. Md. Mehedi Hasan Kajal, DVM),
/// as a ready-made protocol. Day N of the programme is <c>N - 1</c> days after the purchase day
/// (day 1 = the purchase day). Medicines are left unlinked: every farm stocks different brands, so
/// the owner links their own stock item. Feeding advice is kept as care tasks; feeding plans are
/// never changed automatically.
/// </summary>
public static class MarketCattleProgrammeTemplate
{
    public const string Title = "হাট থেকে কেনা গরু – ১০৮ দিনের কর্মসূচি";
    public const string Description =
        "Cattle bought at market – 108 days. হাট থেকে গরু কেনার পর ভেটেরিনারি চিকিৎসকের ১০৮ দিনের পরিচর্যা, কৃমিনাশক ও টিকা কর্মসূচি। " +
        "দিন গোনা শুরু কেনার দিন থেকে (১ম দিন = কেনার দিন)। ওষুধের সাথে নিজের স্টক আইটেম লিংক করুন।";

    /// <summary>Default repeat of the day-94 dewormer and day-108 Ivermectin.</summary>
    public const int DefaultRepeatEveryDays = 90;

    /// <summary>The shortcut on the day-90 decision task: opens the animal's Sell or Keep card.</summary>
    public const string SellOrKeepLink = "sell-or-keep";

    private static readonly ProtocolStepOptions Care = new(HealthTaskKind.CareTask);
    private static readonly ProtocolStepOptions Medicine = new(HealthTaskKind.Medicine);

    public static VaccinationProtocol Build(Guid tenantId, int repeatEveryDays = DefaultRepeatEveryDays, bool autoStartForPurchased = true)
    {
        if (repeatEveryDays is < 1 or > 365)
            throw new Farm360.Domain.Exceptions.DomainException("The repeat must be between 1 and 365 days.");

        var protocol = VaccinationProtocol.Create(tenantId, Title, AnimalSpecies.CattleBeef, Description);
        protocol.SetAutoStartForPurchased(autoStartForPurchased);
        var repeating = new ProtocolStepOptions(HealthTaskKind.Medicine, repeatEveryDays);

        // Day 1 — arrival
        Step(protocol, 1, "পটাশ পানিতে পা ধোয়া ও বিশ্রাম", "পটাশ পানিতে পা ধোয়া",
            "পটাশ পানিতে পা ভালোভাবে ধুয়ে দিন; ১.৫–২ ঘণ্টা সম্পূর্ণ বিশ্রাম; খাবার শুধু পরিষ্কার পানি ও খড়; আবহাওয়া অনুকূল থাকলে গোসল; শরীর মুছে আলাদা শেডে রাখুন", Care);
        Step(protocol, 1, "Fast Vet ট্যাবলেট (ব্যথা ও জ্বর)", "Fast Vet",
            "প্রতি ১৫০ কেজি ওজনে ১টি ট্যাবলেট", new ProtocolStepOptions(HealthTaskKind.Medicine,
                DoseRule: new WeightDoseRule(1m, 150m, DoseRounding.Half, 1m, null, "tablet")));

        // Days 2–3 — watch closely, light feed
        for (var day = 2; day <= 3; day++)
        {
            Step(protocol, day, $"জ্বর ও দুর্বলতা পর্যবেক্ষণ (দিন {Bn(day)})", "স্বাস্থ্য পর্যবেক্ষণ",
                "জ্বর বা দুর্বলতা নিবিড়ভাবে দেখুন; হালকা খড় ও অল্প ঘাস; বদহজম রোধে অতিরিক্ত খাবার নয়; প্রয়োজনে ভেটের পরামর্শ নিন", Care);
        }

        // Day 4 — dewormer
        Step(protocol, 4, "কৃমিনাশক (খালি পেটে, সকালে)", "কৃমিনাশক",
            "সকালে খালি পেটে, শরীরের ওজন অনুযায়ী সঠিক মাত্রায়", Medicine);

        // Days 5–10 — zinc + liver tonic (daily extras)
        for (var day = 5; day <= 10; day++)
        {
            Step(protocol, day, $"জিংক ৫০ মি.লি. + লিভার টনিক ৫০ মি.লি. (দিন {Bn(day)})", "জিংক + লিভার টনিক",
                "প্রতিদিন নিয়মিত ৫০ মি.লি. জিংক ও ৫০ মি.লি. লিভার টনিক", Care);
        }

        // Days 11–13 — daily health check, then FMD on day 14
        for (var day = 11; day <= 13; day++)
        {
            Step(protocol, day, $"দৈনিক স্বাস্থ্য পরীক্ষা (দিন {Bn(day)})", "স্বাস্থ্য পরীক্ষা",
                "খাবার গ্রহণ, জাবর কাটা ও গোবর স্বাভাবিক আছে কি না দেখুন", Care);
        }
        Step(protocol, 14, "এফএমডি (FMD) টিকা", "FMD টিকা",
            "গরু সম্পূর্ণ সুস্থ থাকলে তবেই; সকালে বা বিকেলে দিন", Medicine);

        // Days 15–18 — after the vaccine, external and internal parasites
        Step(protocol, 15, "টিকার পার্শ্বপ্রতিক্রিয়া পর্যবেক্ষণ (দিন ১৫–১৮)", "টিকা পরবর্তী পর্যবেক্ষণ",
            "জ্বর বা খাবারে অরুচি হলে খেয়াল রাখুন", Care);
        Step(protocol, 15, "আইভারমেকটিন ইনজেকশন (উকুন-আঁটালি)", "আইভারমেকটিন",
            "উকুন-আঁটালি দূর করতে", Medicine);
        Step(protocol, 16, "কলিজা কৃমির ওষুধ (ঝুঁকি থাকলে)", "কলিজা কৃমির ওষুধ",
            "কলিজা কৃমির ঝুঁকি থাকলে উপযুক্ত ওষুধ; না থাকলে কারণ লিখে বাদ দিন", Medicine);

        // Days 28 and 42 — vaccines
        Step(protocol, 28, "এলএসডি (LSD) টিকা", "LSD টিকা",
            "নির্ধারিত ডোজ ও প্রয়োগ পদ্ধতি অনুসরণ করুন", Medicine);
        Step(protocol, 42, "অ্যানথ্রাক্স (Anthrax) টিকা", "Anthrax টিকা",
            "স্থানীয় ঝুঁকি বিবেচনা করে দিন; প্রয়োজন না হলে কারণ লিখে বাদ দিন", Medicine);

        // Days 43–90 — routine care and fattening
        Step(protocol, 43, "নিয়মিত পরিচর্যা ও মোটাতাজাকরণ শুরু (দিন ৪৩–৯০)", "নিয়মিত পরিচর্যা",
            "ওজন অনুযায়ী সুষম দানাদার খাবার; নিয়মিত গোসল ও শেড পরিষ্কার; ওজন বৃদ্ধি ও স্বাস্থ্য নিবিড়ভাবে দেখুন", Care);

        // Day 90 — decide: sell or keep
        Step(protocol, 90, "সিদ্ধান্ত: বিক্রি করবেন না রাখবেন", "বিক্রি না রাখা – সিদ্ধান্ত",
            "কাঙ্ক্ষিত ওজন ও বাজার পরিস্থিতি বিবেচনা করে বিক্রির সিদ্ধান্ত নিন", new ProtocolStepOptions(HealthTaskKind.CareTask, ActionLink: SellOrKeepLink));

        // Day 94 — dewormer again (then every N days)
        Step(protocol, 94, "কৃমিনাশক (পুনরায়, খালি পেটে)", "কৃমিনাশক",
            "সকালে খালি পেটে, শরীরের ওজন অনুযায়ী মাত্রায়", repeating);

        // Days 95–100 — zinc + liver tonic
        for (var day = 95; day <= 100; day++)
        {
            Step(protocol, day, $"জিংক + লিভার টনিক (দিন {Bn(day)})", "জিংক + লিভার টনিক",
                "কৃমিনাশক প্রয়োগের পর প্রতিদিন জিংক ও লিভার টনিক", Care);
        }

        // Day 108 — parasite control again (then every N days)
        Step(protocol, 108, "আইভারমেকটিন ইনজেকশন (পুনরায়)", "আইভারমেকটিন",
            "বাহ্যিক পরজীবী (উকুন-আঁটালি) নিয়ন্ত্রণে", repeating);
        Step(protocol, 108, "কলিজা কৃমির ওষুধ (প্রয়োজনে)", "কলিজা কৃমির ওষুধ",
            "কলিজা কৃমির সংক্রমণ থাকলে উপযুক্ত ওষুধ; না থাকলে কারণ লিখে বাদ দিন", Medicine);

        return protocol;
    }

    private static void Step(VaccinationProtocol protocol, int programmeDay, string stepName, string taskName, string instruction, ProtocolStepOptions options) =>
        protocol.AddStep(stepName, programmeDay - 1, taskName, instruction, options: options);

    private static string Bn(int number)
    {
        const string digits = "০১২৩৪৫৬৭৮৯";
        return string.Concat(number.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(c => digits[c - '0']));
    }
}
