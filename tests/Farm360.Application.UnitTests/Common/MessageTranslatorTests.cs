using System.Text.RegularExpressions;
using Farm360.Application.Common.Localization;
using Farm360.Domain.Exceptions;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Farm360.Application.UnitTests.Common;

public class MessageTranslatorTests
{
    private static readonly MessageTranslator Translator = MessageTranslator.Default;

    [Fact]
    public void StockShortageKeepsItemAndQuantities()
    {
        var bangla = Translator.Translate(
            "Insufficient stock for 'Rice straw'. Required: 12.5 kg, Available: 3 kg.", "bn");

        bangla.Should().Be("'Rice straw'-এর মজুদ যথেষ্ট নয়। প্রয়োজন: 12.5 kg, আছে: 3 kg।");
    }

    [Fact]
    public void FeedSheetShortageTranslatesEveryListedItem()
    {
        var bangla = Translator.Translate(
            "Not enough stock, so nothing was saved. Insufficient stock for 'Maize'. Required: 5 kg, Available: 1 kg. "
            + "Insufficient stock for 'DCP'. Required: 0.2 kg, Available: 0 kg.", "bn");

        bangla.Should().StartWith("মজুদ যথেষ্ট নয়, তাই কিছুই সংরক্ষণ করা হয়নি।")
            .And.Contain("'Maize'-এর মজুদ যথেষ্ট নয়। প্রয়োজন: 5 kg, আছে: 1 kg।")
            .And.Contain("'DCP'-এর মজুদ যথেষ্ট নয়। প্রয়োজন: 0.2 kg, আছে: 0 kg।")
            .And.NotContain("Insufficient");
    }

    [Fact]
    public void AnimalNotOnFarmUsesBanglaMonthNames()
    {
        var bangla = Translator.Translate("Cow-12 was sold on 13 Nov 2026, so it can't be fed on 14 Nov 2026.", "bn");

        bangla.Should().Be("Cow-12 13 নভেম্বর 2026 তারিখে বিক্রি হয়েছে, তাই 14 নভেম্বর 2026 তারিখের খাবার লেখা যাবে না।");
    }

    [Fact]
    public void LeftFarmMessageTranslatesTheAction()
    {
        var bangla = Translator.Translate(
            "Cannot record a weight for animal 'B-7': it died on 02 Oct 2026. Only records dated before 02 Oct 2026 can be added.", "bn");

        bangla.Should().Contain("'B-7' পশুর জন্য ওজন লেখা যাবে না").And.Contain("02 অক্টোবর 2026");
    }

    [Fact]
    public void ExitDateReasonIsTranslatedInsideTheOuterMessage()
    {
        var bangla = Translator.Translate(
            "Invalid exit date for animal 'C-1': death date 01 Jan 2026 is before the acquisition date 05 Jan 2026.", "bn");

        bangla.Should().Be("'C-1' পশুর খামার ছাড়ার তারিখ সঠিক নয়: মৃত্যুর তারিখ 01 জানুয়ারি 2026 সংগ্রহের তারিখ 05 জানুয়ারি 2026-এর আগে।");
    }

    [Theory]
    [InlineData("An animal with this Tag ID already exists in your farm.", "এই ট্যাগ নম্বরের একটি পশু আপনার খামারে আগে থেকেই আছে।")]
    [InlineData("Tag ID is required.", "ট্যাগ নম্বর দিতে হবে।")]
    [InlineData("Sale price must be greater than zero.", "বিক্রয়মূল্য শূন্যের বেশি হতে হবে।")]
    [InlineData("Invalid phone number or password.", "ফোন নম্বর বা পাসওয়ার্ড ভুল।")]
    [InlineData("Entity 'InventoryItem' with key '5f1d' was not found.", "মজুদ পণ্য খুঁজে পাওয়া যায়নি।")]
    [InlineData("Cannot transition animal from 'Sold' to 'Quarantined'.", "পশুর অবস্থা 'বিক্রিত' থেকে 'কোয়ারেন্টাইনে' করা যাবে না।")]
    [InlineData("Your Basic plan allows up to 50 animal(s). Upgrade your subscription to register more.", "আপনার Basic প্ল্যানে সর্বোচ্চ 50টি পশু রাখা যায়। আরও পশু নিবন্ধন করতে সাবস্ক্রিপশন আপগ্রেড করুন।")]
    [InlineData("Validation Failed", "তথ্যে ভুল আছে")]
    public void KnownMessagesAreTranslated(string english, string bangla) =>
        Translator.Translate(english, "bn").Should().Be(bangla);

    [Fact]
    public void ArgumentExceptionParameterSuffixIsDropped() =>
        Translator.Translate(new ArgumentException("Weight must be greater than zero.", "weightKg").Message, "bn")
            .Should().Be("ওজন শূন্যের বেশি হতে হবে।");

    [Fact]
    public void UnmatchedMessageStaysEnglish()
    {
        const string message = "The flux capacitor needs 1.21 gigawatts.";
        Translator.Translate(message, "bn").Should().Be(message);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("")]
    public void EnglishRequestsAreNotTranslated(string language)
    {
        const string message = "Insufficient stock for 'Maize'. Required: 5 kg, Available: 1 kg.";
        Translator.Translate(message, language).Should().Be(message);
    }

    [Fact]
    public void BanglaCultureNamesAreRecognised()
    {
        MessageTranslator.IsBangla("bn").Should().BeTrue();
        MessageTranslator.IsBangla("bn-BD").Should().BeTrue();
        MessageTranslator.IsBangla("en").Should().BeFalse();
        MessageTranslator.IsBangla("bnx").Should().BeFalse();
    }

    [Fact]
    public void FluentValidationDefaultMessagesGetBanglaFieldNames()
    {
        var validator = new InlineValidator<Sample>();
        validator.RuleFor(x => x.TagId).NotEmpty();
        validator.RuleFor(x => x.Notes).MaximumLength(5);
        validator.RuleFor(x => x.WeightKg).GreaterThan(0);

        var previous = ValidatorOptions.Global.LanguageManager.Culture;
        ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("en");
        try
        {
            var messages = validator.Validate(new Sample(string.Empty, "too long notes", 0))
                .Errors.Select(e => Translator.Translate(e.ErrorMessage, "bn")).ToList();

            messages.Should().Contain("ট্যাগ নম্বর দিতে হবে।");
            messages.Should().Contain("নোট সর্বোচ্চ 5 অক্ষরের হতে পারে। আপনি 14 অক্ষর লিখেছেন।");
            messages.Should().Contain("ওজন (কেজি) অবশ্যই 0-এর বেশি হতে হবে।");
        }
        finally
        {
            ValidatorOptions.Global.LanguageManager.Culture = previous;
        }
    }

    [Fact]
    public void DomainExceptionMessageRoundTrip() =>
        Translator.Translate(new DomainException("This feeding entry is already confirmed; its stock and cost have already been posted.").Message, "bn")
            .Should().StartWith("এই খাদ্য এন্ট্রিটি আগেই নিশ্চিত");

    [Fact]
    public void EveryTemplateCompilesAndUsesOnlyItsOwnPlaceholders()
    {
        MessageTranslator.TemplateCount.Should().Be(BanglaMessageCatalogue.Templates.Count);
        var placeholder = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)(?::[wt])?\}", RegexOptions.None, TimeSpan.FromSeconds(1));
        foreach (var (english, bangla) in BanglaMessageCatalogue.Templates)
        {
            var names = placeholder.Matches(english).Select(m => m.Groups[1].Value).ToHashSet();
            foreach (Match m in placeholder.Matches(bangla))
                names.Should().Contain(m.Groups[1].Value, $"'{bangla}' uses a value its English template '{english}' does not capture");
            bangla.Should().NotContain(":w}").And.NotContain(":t}");
        }
    }

    private sealed record Sample(string TagId, string Notes, decimal WeightKg);
}
