/**
 * Bangla text for money accounts (টাকার খাতা) and partners (অংশীদার): the "Paid from" selector,
 * transfers, partner money, the partners' capital, share and profit split.
 * Key = the English text exactly as shown; value = Bangla.
 */
export const BN_MONEY: Record<string, string> = {
  // ── Money accounts ──
  'Cash in hand': 'হাতে নগদ',
  'Money accounts': 'টাকার খাতা',
  'Money account': 'টাকার খাতা',
  'Bank account': 'ব্যাংক হিসাব',
  'Mobile wallet': 'মোবাইল ওয়ালেট',
  'Mobile wallet (bKash, Nagad…)': 'মোবাইল ওয়ালেট (বিকাশ, নগদ…)',
  "Where the farm's money is: cash in hand, bank and mobile wallets. Every spend comes out of one; every receipt goes into one.":
    'খামারের টাকা কোথায় আছে: হাতে নগদ, ব্যাংক ও মোবাইল ওয়ালেট। প্রতিটি খরচ কোনো একটি খাতা থেকে যায়, প্রতিটি আয় কোনো একটিতে জমা হয়।',
  'New account': 'নতুন খাতা',
  'New money account': 'নতুন টাকার খাতা',
  'Edit money account': 'টাকার খাতা সম্পাদনা',
  'Total cash & bank': 'মোট নগদ ও ব্যাংক',
  'The same figure as Cash & Bank on the balance sheet, split by where the money is kept.':
    'উদ্বৃত্তপত্রের নগদ ও ব্যাংকের একই অঙ্ক, টাকা কোথায় রাখা আছে সেই অনুযায়ী ভাগ করা।',
  'Postings that do not name an account land here.': 'যে লেনদেনে কোনো খাতা বলা নেই, তা এখানে জমা হয়।',
  'Default': 'মূল খাতা',
  'Closed': 'বন্ধ',
  'Transfer from here': 'এখান থেকে স্থানান্তর',
  'Make default': 'মূল খাতা করুন',
  'Close account': 'খাতা বন্ধ করুন',
  'Reopen account': 'খাতা আবার চালু করুন',
  'Below zero: more was paid out of this account than came in. Add partner money or transfer into it.':
    'শূন্যের নিচে: এই খাতা থেকে যত টাকা এসেছে তার চেয়ে বেশি খরচ হয়েছে। অংশীদারের টাকা যোগ করুন বা এই খাতায় স্থানান্তর করুন।',
  'Show closed accounts': 'বন্ধ খাতাগুলোও দেখান',
  'Cash book: {account}': 'নগদান বই: {account}',
  'Every taka in and out of this account, with the running balance.': 'এই খাতায় প্রতিটি টাকা আসা-যাওয়া, চলতি জেরসহ।',
  'PDF / Excel': 'পিডিএফ / এক্সেল',
  'Money in': 'টাকা এসেছে',
  'Money out': 'টাকা গেছে',
  'Opening': 'প্রারম্ভিক জের',
  'Closing': 'সমাপনী জের',
  'Particulars': 'বিবরণ',
  'In': 'জমা',
  'Out': 'খরচ',
  'Delete this transfer (both sides)': 'এই স্থানান্তর মুছুন (দুই দিকই)',
  'No money moved through this account in the period.': 'এই সময়ে এই খাতায় কোনো টাকা আসা-যাওয়া হয়নি।',
  'Default account changed.': 'মূল খাতা বদলানো হয়েছে।',
  'Delete this transfer? Both sides are removed and the two balances go back.':
    'এই স্থানান্তর মুছবেন? দুই দিকই মুছে যাবে এবং দুই খাতার জের আগের মতো হবে।',
  'Transfer from {account}': '{account} থেকে স্থানান্তর',
  'Transfer to {account}': '{account}-এ স্থানান্তর',
  'Kind': 'ধরন',
  'Account name': 'খাতার নাম',
  'e.g. Sonali Bank, bKash (owner), Cash box': 'যেমন সোনালী ব্যাংক, বিকাশ (মালিক), ক্যাশ বাক্স',
  'Bank name': 'ব্যাংকের নাম',
  'Wallet provider': 'ওয়ালেট প্রতিষ্ঠান',
  'e.g. Sonali Bank': 'যেমন সোনালী ব্যাংক',
  'e.g. bKash': 'যেমন বিকাশ',
  'Account number': 'হিসাব নম্বর',
  'Wallet number': 'ওয়ালেট নম্বর',
  'Branch': 'শাখা',
  'A new account starts at ৳0. Put money in with "Add partner money" or move it from another account with a transfer.':
    'নতুন খাতা ৳0 দিয়ে শুরু হয়। "অংশীদারের টাকা যোগ করুন" দিয়ে টাকা জমা দিন, বা অন্য খাতা থেকে স্থানান্তর করুন।',
  'Where the money is': 'টাকা কোথায় আছে',
  'of which in {account}': 'এর মধ্যে {account}-এ',
  'Open money accounts': 'টাকার খাতা খুলুন',
  'Loading…': 'লোড হচ্ছে…',

  // ── Transfers ──
  'Transfer between accounts': 'খাতার মধ্যে স্থানান্তর',
  'Transfer': 'স্থানান্তর',
  'Moves money between your own accounts. It is not income or expense; total cash stays the same.':
    'আপনার নিজের খাতাগুলোর মধ্যে টাকা সরায়। এটি আয় বা খরচ নয়; মোট নগদ একই থাকে।',
  'From account': 'যে খাতা থেকে',
  'To account': 'যে খাতায়',
  'Choose…': 'বেছে নিন…',
  '{account} has {balance}; this moves {amount}.': '{account}-এ আছে {balance}; এখানে সরানো হচ্ছে {amount}।',
  'Reference / Notes (Optional)': 'রেফারেন্স / মন্তব্য (ঐচ্ছিক)',
  'e.g. Withdrew from bank for wages': 'যেমন মজুরির জন্য ব্যাংক থেকে তোলা',

  // ── Paid from ──
  'Paid from': 'কোথা থেকে দেওয়া হলো',
  'Money goes into': 'টাকা যে খাতায় জমা হবে',
  'Paid out of': 'যে খাতা থেকে দেওয়া হবে',
  'Advance paid from': 'অগ্রিম যে খাতা থেকে',
  'Paid by partner': 'অংশীদার দিয়েছেন',
  '{account} has {balance}; this spends {amount}.': '{account}-এ আছে {balance}; এখানে খরচ হচ্ছে {amount}।',
  'You can still save; the account will show below zero until money is added.':
    'তবুও সংরক্ষণ করা যাবে; টাকা যোগ না করা পর্যন্ত খাতাটি শূন্যের নিচে দেখাবে।',
  'Add partner money': 'অংশীদারের টাকা যোগ করুন',
  'Choose the partner who paid…': 'যে অংশীদার টাকা দিয়েছেন তাঁকে বেছে নিন…',
  "The partner paid out of their own pocket. This is booked as their capital put into the farm and the payment made from it, on the same day — the farm's cash does not change, and their share grows.":
    'অংশীদার নিজের পকেট থেকে দিয়েছেন। একই দিনে এটি খামারে তাঁর মূলধন জমা এবং সেখান থেকে খরচ হিসেবে লেখা হবে — খামারের নগদ বদলায় না, তাঁর অংশ বাড়ে।',
  'No partners yet. Add the partners (the owner too) on the Partners page first.':
    'এখনও কোনো অংশীদার নেই। আগে অংশীদার পাতায় অংশীদারদের (মালিকসহ) যোগ করুন।',
  'Money a partner or the owner takes out is recorded under Finance → Partners, not as an expense.':
    'অংশীদার বা মালিক যে টাকা তুলে নেন তা খরচ নয়; সেটি হিসাব-নিকাশ → অংশীদার থেকে লিখুন।',

  // ── Partner money dialog ──
  'Partner money': 'অংশীদারের টাকা',
  "Money a partner puts in, takes out, or is paid as profit — always through one of the farm's accounts.":
    'অংশীদার যে টাকা জমা দেন, তুলে নেন বা লাভ হিসেবে পান — সবসময় খামারের কোনো একটি খাতার মাধ্যমে।',
  'Partner': 'অংশীদার',
  'Choose a partner…': 'অংশীদার বেছে নিন…',
  'Capital {amount} · share {percent}%': 'মূলধন {amount} · অংশ {percent}%',
  'What happened?': 'কী হয়েছে?',
  'Put money in': 'টাকা জমা দিয়েছেন',
  'Take capital out': 'মূলধন তুলেছেন',
  'Pay profit': 'লাভ পরিশোধ',
  'Reference (Optional)': 'রেফারেন্স (ঐচ্ছিক)',
  'e.g. BANK-TRF-001': 'যেমন BANK-TRF-001',

  // ── Partners page ──
  'Partners': 'অংশীদার',
  "Everyone who has money in the farm — the owner too. Money first comes into a farm account; each partner's share is their capital.":
    'খামারে যাঁদের টাকা আছে — মালিকসহ সবাই। টাকা প্রথমে খামারের কোনো খাতায় আসে; প্রত্যেক অংশীদারের অংশ তাঁর মূলধন অনুযায়ী।',
  'New partner': 'নতুন অংশীদার',
  "Partners' capital": 'অংশীদারদের মূলধন',
  'Animals + stock + cash & bank + money owed to the farm + fixed assets − loans and other debts, at book value.':
    'পশু + মজুদ + নগদ ও ব্যাংক + খামারের পাওনা + স্থায়ী সম্পদ − ঋণ ও অন্যান্য দেনা, খাতা মূল্যে।',
  'Farm net assets': 'খামারের নিট সম্পদ',
  'Profit allocated': 'বণ্টিত লাভ',
  'Profit paid out': 'পরিশোধিত লাভ',
  'Profit paid': 'পরিশোধিত লাভ',
  'Profit due': 'পাওনা লাভ',
  'Capital and share': 'মূলধন ও অংশ',
  "Share = partner's capital ÷ all partners' capital (live).": 'অংশ = অংশীদারের মূলধন ÷ সব অংশীদারের মোট মূলধন (এই মুহূর্তের)।',
  'Put in': 'জমা',
  'Taken out': 'উত্তোলন',
  'Capital': 'মূলধন',
  'Share': 'অংশ',
  'Statement': 'বিবরণী',
  'Inactive': 'নিষ্ক্রিয়',
  'Activate': 'সক্রিয় করুন',
  'Deactivate': 'নিষ্ক্রিয় করুন',
  'No partners yet. Add each partner — the owner too — then record the money each one puts in.':
    'এখনও কোনো অংশীদার নেই। প্রত্যেক অংশীদারকে (মালিকসহ) যোগ করুন, তারপর প্রত্যেকে কত টাকা জমা দিলেন তা লিখুন।',
  'Profit split for a period': 'নির্দিষ্ট সময়ের লাভ বণ্টন',
  "How profit is split: by capital-days — each partner's capital multiplied by the number of days it was in the farm during the period. Example: A puts in ৳1,00,000 for the whole year, B puts in ৳1,00,000 half-way through. A held it twice as long, so A gets about 66.7% and B about 33.3% of that year's profit.":
    'লাভ যেভাবে ভাগ হয়: মূলধন-দিনে — প্রত্যেক অংশীদারের মূলধন × সেই সময়ে টাকাটা কত দিন খামারে ছিল। উদাহরণ: A সারা বছর ৳1,00,000 রেখেছেন, B বছরের মাঝামাঝি ৳1,00,000 দিয়েছেন। A দ্বিগুণ সময় রেখেছেন, তাই সেই বছরের লাভের প্রায় 66.7% A পাবেন আর প্রায় 33.3% B পাবেন।',
  'Net profit': 'নিট লাভ',
  'Charity ({percent}%)': 'দান ({percent}%)',
  'To share': 'ভাগ করার জন্য',
  'Days': 'দিন',
  'Capital held on an average day of the period.': 'এই সময়ের গড় একটি দিনে যত মূলধন ছিল।',
  'Average capital': 'গড় মূলধন',
  'Capital-days': 'মূলধন-দিন',
  'Share of profit': 'লাভের অংশ',
  'No partner had money in the farm during this period.': 'এই সময়ে কোনো অংশীদারের টাকা খামারে ছিল না।',
  'Whose money where': 'কার টাকা কোথায়',
  'Each spend split between the partners by their capital on the day it was spent (pooled fund).':
    'প্রতিটি খরচ যেদিন হয়েছে সেদিনের মূলধন অনুযায়ী অংশীদারদের মধ্যে ভাগ করা (যৌথ তহবিল)।',
  'Spend': 'খরচ',
  'Amount': 'পরিমাণ',
  'No spending in this period.': 'এই সময়ে কোনো খরচ নেই।',
  'Total': 'মোট',

  // ── Partner statement ──
  'Partner statement': 'অংশীদারের হিসাব বিবরণী',
  'Partner statement: money put in and taken out, profit allocated and paid, and part of the farm.':
    'অংশীদারের হিসাব বিবরণী: জমা ও উত্তোলন, বণ্টিত ও পরিশোধিত লাভ, এবং খামারে অংশ।',
  'Period': 'সময়কাল',
  'Capital now': 'বর্তমান মূলধন',
  'Share of the farm': 'খামারে অংশ',
  'Profit allocated (all time)': 'বণ্টিত লাভ (শুরু থেকে)',
  'Part of net assets': 'নিট সম্পদে ভাগ',
  'Capital ledger': 'মূলধনের খতিয়ান',
  'Entry': 'বিবরণ',
  'In / allocated': 'জমা / বণ্টিত',
  'Out / paid': 'উত্তোলন / পরিশোধ',
  'Capital after': 'এরপর মূলধন',
  'Capital at start': 'শুরুর মূলধন',
  'Capital put in': 'মূলধন জমা',
  'Capital taken out': 'মূলধন উত্তোলন',
  'Profit allocated (capital-days)': 'লাভের ভাগ (মূলধন-দিন অনুযায়ী)',
  'Share of profit {from} – {to} by capital-days': 'মূলধন-দিন অনুযায়ী লাভের ভাগ, {from} – {to}',
  'No money moved for this partner in the period.': 'এই সময়ে এই অংশীদারের কোনো লেনদেন নেই।',
  "Part of the farm's net assets": 'খামারের নিট সম্পদে ভাগ',
  "At book value (what was paid), by the partner's current share of {percent}%.": 'খাতা মূল্যে (যা দেওয়া হয়েছিল), অংশীদারের বর্তমান {percent}% অংশ অনুযায়ী।',
  'Item': 'বিষয়',
  'Farm': 'খামার',
  'This partner': 'এই অংশীদার',
  'Net assets': 'নিট সম্পদ',

  // ── Accounting health (server issue titles) ──
  'Cash balance is negative': 'নগদ জের শূন্যের নিচে',
  'Money account balance is negative': 'টাকার খাতার জের শূন্যের নিচে',
  'More money has been recorded as paid out of these accounts than came in. Add partner money, transfer between accounts, or enter the missing opening balance or loan receipt.':
    'এই খাতাগুলো থেকে যত টাকা এসেছে তার চেয়ে বেশি খরচ লেখা হয়েছে। অংশীদারের টাকা যোগ করুন, খাতার মধ্যে স্থানান্তর করুন, বা বাদ পড়া প্রারম্ভিক জের বা ঋণ প্রাপ্তি লিখুন।',

  // ── Navigation ──
  'Investors': 'অংশীদার',
};
