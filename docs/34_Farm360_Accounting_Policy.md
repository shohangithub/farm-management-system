# Farm360 Accounting Policy

How every money-related record becomes a ledger posting, and how the statements are built from it.
The code that enforces this lives in `src/Farm360.Application/Finance/Services/AccountChart.cs`.

## 1. Basis: modified accrual

| Item | Bought | Becomes a cost when |
|---|---|---|
| Feed, medicine, supplies | **Inventory** asset (at cost) | Used (feeding, treatment, vaccination, consumables) or written off |
| Animals | **Livestock** asset (at purchase price) | Sold or slaughtered (*Cost of Animals Sold*) or dies (*Mortality Loss*) |
| Loans | **Loans Payable** liability | Only the **interest** part of a repayment is an expense |
| Investor / owner money | **Equity** | Never profit; distributions and drawings reduce equity |
| Bought / sold on credit | **Accounts Payable / Receivable** | Cost or revenue on the deal date; cash moves only when settled |

So a month in which cattle or a year of feed is bought is no longer shown as a huge loss, and
feed is not counted twice (once on purchase, again on use).

## 2. One posting = one double entry

Each `FinancialTransaction` has a **category** (one account) and a **settlement** (the other):

- `Settlement = Cash` — money moved. Only these appear in the cash book.
- `Settlement = Inventory` — stock was used or written off (Dr expense, Cr Inventory).
- `Settlement = Livestock` — an animal was sold or died (Dr cost/loss, Cr Livestock).
- `Settlement = Payable` — bought on credit (Dr expense/asset, Cr Accounts Payable 2100).
- `Settlement = Receivable` — sold on credit (Dr Accounts Receivable 1100, Cr revenue).

Expense-type rows debit the category account and credit the settlement; income-type rows the reverse.
The trial balance and balance sheet are built from these entries, so they always balance.

## 3. Who posts what

| Event | Posting |
|---|---|
| PO received / Stock-in (paid) | Stock Purchase — Dr Inventory, Cr Cash |
| Opening stock / owner-supplied stock | Owner's Capital — Dr Inventory, Cr Owner's Capital |
| Feeding entry confirmed | Feed Cost from stock (Cr Inventory) + untracked ingredients (Cr Cash) |
| Consumables, treatment & vaccine from stock, stock-out | Expense, Cr Inventory (at weighted average cost) |
| Stock write-off | Stock Write-off, Cr Inventory |
| Supplier return | Dr Cash, Cr Inventory |
| Animal registered (purchased) | Livestock Purchase on the **acquisition date** |
| Animal sold | Livestock Sales (cash) + Cost of Animals Sold (Cr Livestock) |
| Animal died / slaughtered | Mortality Loss / Cost of Animals Sold (Cr Livestock) |
| Loan created | Loan Received — Dr Cash, Cr Loans Payable |
| Loan repayment | Principal (Dr Loans Payable) + Interest (expense), split pro rata |
| Investor / share transactions | Investor Capital, Withdrawal, Profit Distribution |

All automated postings are keyed on their source document (`SourceDocumentId` + category), so a
re-published event or a corrected record **revises** the posting instead of duplicating it.

## 4. Manual entries

The Income/Expense forms accept only heads that no other module owns: labour, utilities, transport,
direct feed/medicine purchases, vet fees, miscellaneous, milk/by-product/other income, owner's
capital and drawings. Stock, animals, loans and investors must be recorded in their own modules.
Automated postings are read-only in the ledger — correct the source record instead.

## 5. Proving the books

**Finance → Accounting Health** reconciles each balance-sheet account against its register:
Inventory vs stock on hand, Livestock vs the herd, Loans Payable vs the loan register, Investor
Capital vs the investor register. **Repair ledger** fixes records created before these rules
(missing loan receipts, unsplit repayments, mis-dated purchases, sold/dead animals still carried)
and can optionally adjust the books to a physical stock count.

## 6. Payables & receivables (credit)

A purchase or sale is booked by its own module exactly as before. Choosing **On credit (বাকি)** — on
Stock In, Purchase Order receipt, Record Expense, Sell Animal, Record Income, or **Put on credit** on
a ledger entry — raises a **bill** (`TradeBill`) for the unpaid part and re-settles that posting against
Accounts Payable / Receivable instead of Cash.

| Event | Posting |
|---|---|
| Bought on credit | Expense or asset, **Cr Accounts Payable** (not cash) |
| Sold on credit | **Dr Accounts Receivable**, Cr revenue |
| Supplier paid | Supplier Payment — Dr Accounts Payable, Cr Cash |
| Customer pays | Customer Collection — Dr Cash, Cr Accounts Receivable |
| Return against an unpaid order | Dr Accounts Payable, Cr Inventory (reduces the bill, no cash refund) |
| Supplier waives the rest | Dr Accounts Payable, Cr Other Income |
| Customer will never pay | Bad Debt expense, Cr Accounts Receivable |

Rules:

- A bill mirrors its source posting. If the purchase or sale is corrected later (price changed),
  the bill follows; if it is removed after something was paid, the bill shows **Overpaid** (an
  advance to recover or a customer credit).
- Payments can be voided (the cash entry is reversed). An unpaid bill can be cancelled, which turns
  the deal back into a cash deal on its original date.
- Default terms are 30 days. Aging buckets: not yet due, 1–30, 31–60, 61–90, over 90 days.
- Only purchase and sale heads can be on credit; owner money, interest, loans and investor money
  are always cash.
- **Finance → Payables & Receivables** lists every open bill, the balance per supplier/customer and
  the aging, and proves the bills add up to accounts 2100 / 1100. Accounting Health checks the same
  and warns about customer balances more than 90 days overdue.

## 7. Not yet covered

- Moving animals between farms — the app only moves animals between sheds on one farm, which has
  no accounting effect. A farm-to-farm transfer, if added, should move the animal's book cost
  between the two farms' Livestock accounts with no profit or loss.
- Fair-value revaluation of livestock (the herd is carried at cost). A market value can be shown
  as a memo figure, but is not booked as profit.
