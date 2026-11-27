/** Bangla text for the shared screens. Key = the English text exactly as shown; value = Bangla. */
export const BN_SHARED: Record<string, string> = {
  // Confirmation dialog / confirm service defaults
  'Are you sure?': 'আপনি কি নিশ্চিত?',
  'Please confirm': 'অনুগ্রহ করে নিশ্চিত করুন',
  'Notice': 'বিজ্ঞপ্তি',
  'Dismiss': 'বন্ধ করুন',

  // Empty / loading / error states
  'No Data Available': 'কোনো তথ্য নেই',
  'There is currently no data to display here.': 'এখানে দেখানোর মতো কোনো তথ্য এখন নেই।',
  'Loading data...': 'তথ্য লোড হচ্ছে...',
  "Couldn't load data": 'তথ্য লোড করা যায়নি',
  'Something went wrong while loading. Please check your connection and try again.': 'লোড করার সময় সমস্যা হয়েছে। ইন্টারনেট সংযোগ দেখে আবার চেষ্টা করুন।',

  // Access denied
  "You don't have access to this page": 'এই পেজে প্রবেশের অনুমতি আপনার নেই',
  "Your role does not include the permission needed for this screen. Ask your organisation's owner or administrator to grant it if you need access.":
    'এই পেজের জন্য প্রয়োজনীয় অনুমতি আপনার ভূমিকায় নেই। প্রবেশাধিকার দরকার হলে আপনার প্রতিষ্ঠানের মালিক বা অ্যাডমিনকে অনুমতি দিতে বলুন।',
  'Back to dashboard': 'ড্যাশবোর্ডে ফিরে যান',

  // Data table
  'Restore': 'পুনরুদ্ধার করুন',
  'Select page': 'পেজ নির্বাচন করুন',

  // Farm guidelines dialog
  'Got it': 'বুঝেছি',

  // Global search
  'Search everywhere...': 'সবখানে খুঁজুন...',
  'Searching...': 'খোঁজা হচ্ছে...',
  'No results found for "{term}"': '"{term}"-এর জন্য কিছু পাওয়া যায়নি',
  'Animals': 'পশু',
  'Tag: {tag}': 'ট্যাগ: {tag}',
  'Category: {category}': 'শ্রেণি: {category}',

  // Animal pickers
  'Shed (Optional)': 'শেড (ঐচ্ছিক)',
  '-- All Sheds --': '-- সব শেড --',
  'Search Animal (Tag, Breed)': 'পশু খুঁজুন (ট্যাগ, জাত)',
  'Search and Select Animals': 'পশু খুঁজে নির্বাচন করুন',
  'Type tag or breed...': 'ট্যাগ বা জাত লিখুন...',
  'Shed ID: {id}': 'শেড আইডি: {id}',
  'Remove': 'সরিয়ে দিন',
  'Remove {tag}': '{tag} সরিয়ে দিন',
  'Selected animals': 'নির্বাচিত পশু',
  'Unknown': 'অজানা',
  'Male': 'পুরুষ',
  'Female': 'স্ত্রী',

  // Raw values shown from the server (species, animal status, item category)
  'CattleBeef': 'গরু (মাংস)',
  'CattleDairy': 'গরু (দুগ্ধ)',
  'Goat': 'ছাগল',
  'Sheep': 'ভেড়া',
  'Active': 'সক্রিয়',
  'Quarantined': 'কোয়ারেন্টাইনে',
  'Sold': 'বিক্রিত',
  'Slaughtered': 'জবাইকৃত',
  'Dead': 'মৃত',
  'Transferred': 'স্থানান্তরিত',
  'Feed': 'খাদ্য',
  'Medicine': 'ওষুধ',
  'Vaccine': 'টিকা',
  'Chemical': 'রাসায়নিক',
  'Equipment': 'যন্ত্রপাতি',
  'Other': 'অন্যান্য',
  'Consumable': 'ব্যবহার্য সামগ্রী',

  // ── Farm map (shared/components/farm-map) ──────────────────────────────────────────────
  'Map of {name}': '{name}-এর মানচিত্র',
  'Farm location map': 'খামারের অবস্থানের মানচিত্র',
  'Farm location': 'খামারের অবস্থান',
  'Map type': 'মানচিত্রের ধরন',
  'Street': 'রাস্তা',
  'Satellite': 'স্যাটেলাইট',
  'Loading map…': 'মানচিত্র লোড হচ্ছে…',
  'The map could not be loaded. Check the internet connection.': 'মানচিত্র লোড করা যায়নি। ইন্টারনেট সংযোগ দেখুন।',
  'Open in Google Maps': 'Google Maps-এ খুলুন',
};
