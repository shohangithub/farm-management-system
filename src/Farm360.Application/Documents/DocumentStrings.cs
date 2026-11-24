using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Documents;

/// <summary>
/// Every fixed string a customer document prints, in both languages — the documents' counterpart
/// of <see cref="ReportStrings"/>.
/// </summary>
public static class DocumentStrings
{
    // ── Titles ──────────────────────────────────────────────────────────────
    public static LocalizedText SaleInvoice { get; } = new("Sale Invoice", "বিক্রয় রসিদ");
    public static LocalizedText MoneyReceipt { get; } = new("Money Receipt", "টাকা প্রাপ্তির রসিদ");
    public static LocalizedText PaymentVoucher { get; } = new("Payment Voucher", "পরিশোধ ভাউচার");
    public static LocalizedText PurchaseOrder { get; } = new("Purchase Order", "ক্রয় আদেশ");

    // ── Header fields ───────────────────────────────────────────────────────
    public static LocalizedText InvoiceNo { get; } = new("Invoice no.", "রসিদ নং");
    public static LocalizedText ReceiptNo { get; } = new("Receipt no.", "রসিদ নং");
    public static LocalizedText VoucherNo { get; } = new("Voucher no.", "ভাউচার নং");
    public static LocalizedText PoNo { get; } = new("PO no.", "ক্রয় আদেশ নং");
    public static LocalizedText Date { get; } = new("Date", "তারিখ");
    public static LocalizedText BilledTo { get; } = new("Billed to", "ক্রেতা");
    public static LocalizedText ReceivedFrom { get; } = new("Received from", "যার কাছ থেকে গৃহীত");
    public static LocalizedText PaidTo { get; } = new("Paid to", "যাকে পরিশোধ");
    public static LocalizedText Supplier { get; } = new("Supplier", "সরবরাহকারী");
    public static LocalizedText Phone { get; } = new("Phone", "ফোন");
    public static LocalizedText Email { get; } = new("Email", "ইমেইল");
    public static LocalizedText Address { get; } = new("Address", "ঠিকানা");
    public static LocalizedText ContactPerson { get; } = new("Contact", "যোগাযোগ");
    public static LocalizedText ExpectedDelivery { get; } = new("Expected delivery", "প্রত্যাশিত সরবরাহ");
    public static LocalizedText Status { get; } = new("Status", "অবস্থা");
    public static LocalizedText CashCustomer { get; } = new("Cash customer", "নগদ ক্রেতা");
    public static LocalizedText CashSale { get; } = new("Cash sale", "নগদ বিক্রয়");
    public static LocalizedText CreditSale { get; } = new("Credit sale", "বাকিতে বিক্রয়");

    // ── Sale line ───────────────────────────────────────────────────────────
    public static LocalizedText Description { get; } = new("Description", "বিবরণ");
    public static LocalizedText Tag { get; } = new("Tag", "ট্যাগ");
    public static LocalizedText Species { get; } = new("Species", "প্রজাতি");
    public static LocalizedText Breed { get; } = new("Breed", "জাত");
    public static LocalizedText Sex { get; } = new("Sex", "লিঙ্গ");
    public static LocalizedText Age { get; } = new("Age", "বয়স");
    public static LocalizedText WeightKg { get; } = new("Weight (kg)", "ওজন (কেজি)");
    public static LocalizedText PricePerKg { get; } = new("Price / kg", "দর / কেজি");
    public static LocalizedText AmountBdt { get; } = new("Amount (BDT)", "টাকা");

    // ── Totals ──────────────────────────────────────────────────────────────
    public static LocalizedText Total { get; } = new("Total", "মোট");
    public static LocalizedText Received { get; } = new("Received", "গৃহীত");
    public static LocalizedText Adjusted { get; } = new("Adjusted / written off", "সমন্বয় / মওকুফ");
    public static LocalizedText BalanceDue { get; } = new("Balance due", "বকেয়া");
    public static LocalizedText DueDate { get; } = new("Due date", "পরিশোধের শেষ তারিখ");
    public static LocalizedText AmountInWords { get; } = new("In words", "কথায়");
    public static LocalizedText GrandTotal { get; } = new("Grand total", "সর্বমোট");

    // ── Payments ────────────────────────────────────────────────────────────
    public static LocalizedText PaymentHistory { get; } = new("Payments received", "পরিশোধের বিবরণ");
    public static LocalizedText Method { get; } = new("Method", "মাধ্যম");
    public static LocalizedText Reference { get; } = new("Reference", "রেফারেন্স");
    public static LocalizedText Notes { get; } = new("Notes", "মন্তব্য");
    public static LocalizedText AgainstBill { get; } = new("Against bill", "যে বিলের বিপরীতে");
    public static LocalizedText BillDate { get; } = new("Bill date", "বিলের তারিখ");
    public static LocalizedText BillAmount { get; } = new("Bill amount", "বিলের পরিমাণ");
    public static LocalizedText PaidToDate { get; } = new("Paid to date (incl. this)", "এ পর্যন্ত পরিশোধ (এটিসহ)");
    public static LocalizedText BalanceRemaining { get; } = new("Balance remaining", "অবশিষ্ট বকেয়া");
    public static LocalizedText AmountReceived { get; } = new("Amount received", "গৃহীত টাকা");
    public static LocalizedText AmountPaid { get; } = new("Amount paid", "পরিশোধিত টাকা");

    // ── Purchase order ──────────────────────────────────────────────────────
    public static LocalizedText Item { get; } = new("Item", "পণ্য");
    public static LocalizedText Quantity { get; } = new("Qty", "পরিমাণ");
    public static LocalizedText Unit { get; } = new("Unit", "একক");
    public static LocalizedText UnitCost { get; } = new("Unit cost", "একক দর");
    public static LocalizedText LineTotal { get; } = new("Total", "মোট");
    public static LocalizedText TermsTitle { get; } = new("Terms", "শর্তাবলি");
    public static LocalizedText PoTerms { get; } = new(
        "Please quote this PO number on the delivery challan and the invoice. Goods are accepted subject to inspection on arrival.",
        "চালান ও বিলে এই ক্রয় আদেশ নম্বর উল্লেখ করুন। পৌঁছানোর পর পরিদর্শন সাপেক্ষে মালামাল গ্রহণ করা হবে।");

    // ── Signatures & footer ─────────────────────────────────────────────────
    public static LocalizedText ReceivedBy { get; } = new("Received by", "গ্রহণকারী");
    public static LocalizedText AuthorisedSignature { get; } = new("Authorised signature", "অনুমোদিত স্বাক্ষর");
    public static LocalizedText PreparedBy { get; } = new("Prepared by", "প্রস্তুতকারী");
    public static LocalizedText ApprovedBy { get; } = new("Approved by", "অনুমোদনকারী");
    public static LocalizedText ThankYou { get; } = new("Thank you for your business.", "আমাদের সাথে লেনদেনের জন্য ধন্যবাদ।");
    public static LocalizedText ComputerGenerated { get; } = new("Computer-generated document", "কম্পিউটারে প্রস্তুতকৃত");
    public static LocalizedText Page { get; } = new("Page", "পৃষ্ঠা");
    public static LocalizedText Of { get; } = new("of", "এর");

    // ── Animal attributes ───────────────────────────────────────────────────
    public static LocalizedText Male { get; } = new("Male", "পুরুষ");
    public static LocalizedText Female { get; } = new("Female", "স্ত্রী");
    public static LocalizedText CattleBeef { get; } = new("Cattle (beef)", "গরু (মাংস)");
    public static LocalizedText CattleDairy { get; } = new("Cattle (dairy)", "গরু (দুধ)");
    public static LocalizedText Goat { get; } = new("Goat", "ছাগল");
    public static LocalizedText Sheep { get; } = new("Sheep", "ভেড়া");
    public static LocalizedText Years { get; } = new("y", "বছর");
    public static LocalizedText Months { get; } = new("m", "মাস");
    public static LocalizedText Days { get; } = new("d", "দিন");

    // ── Purchase order statuses ─────────────────────────────────────────────
    public static LocalizedText Draft { get; } = new("Draft", "খসড়া");
    public static LocalizedText PendingApproval { get; } = new("Pending approval", "অনুমোদনের অপেক্ষায়");
    public static LocalizedText Approved { get; } = new("Approved", "অনুমোদিত");
    public static LocalizedText Fulfilled { get; } = new("Received", "গৃহীত");
    public static LocalizedText Cancelled { get; } = new("Cancelled", "বাতিল");
}
