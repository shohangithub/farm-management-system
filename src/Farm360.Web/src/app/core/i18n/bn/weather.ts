/**
 * Bangla text for the farm weather feature: dashboard card, Weather page, Today's-tasks banner, the farm
 * location helper, the advice the server sends (Farm360.Application.Weather.WeatherAnalyzer) and the
 * weather bell alert (WeatherAlertService). Key = the English text exactly; value = Bangla. Digits stay Latin.
 */
export const BN_WEATHER: Record<string, string> = {
  // ── Card, page, banner ──────────────────────────────────────────────────────────────────
  'Weather': 'আবহাওয়া',
  'Farm weather': 'খামারের আবহাওয়া',
  'Full forecast': 'পুরো পূর্বাভাস',
  "Set your farm's location to see weather": 'আবহাওয়া দেখতে খামারের অবস্থান দিন',
  'Set location': 'অবস্থান দিন',
  'Feels like {t}°': 'অনুভূত {t}°',
  'Feels like': 'অনুভূত',
  'Humidity': 'আর্দ্রতা',
  'Max': 'সর্বোচ্চ',
  'Min': 'সর্বনিম্ন',
  'Today': 'আজ',
  'Tomorrow': 'আগামীকাল',
  'Later this week': 'এই সপ্তাহে পরে',
  'Heat stress: {band} (THI {thi})': 'গরমের চাপ: {band} (THI {thi})',
  'Midday heat stress: {band} (THI {thi})': 'দুপুরে গরমের চাপ: {band} (THI {thi})',
  'Heat stress': 'গরমের চাপ',
  'Heat stress bands': 'গরমের চাপের মাত্রা',
  'Weather unavailable': 'আবহাওয়ার তথ্য পাওয়া যাচ্ছে না',
  'The weather service could not be reached. Try again in a few minutes.': 'আবহাওয়া সেবার সাথে যোগাযোগ করা যায়নি। কয়েক মিনিট পরে আবার চেষ্টা করুন।',
  'Heat stress and work advice for {farm}': '{farm}-এর গরমের চাপ ও কাজের পরামর্শ',
  'Heat stress and work advice for your cattle': 'আপনার গরুর গরমের চাপ ও কাজের পরামর্শ',
  'Choose a farm to see its weather.': 'আবহাওয়া দেখতে একটি খামার বেছে নিন।',
  'Add the latitude and longitude on the farm form. Only these coordinates are sent to the weather service.':
    'খামারের ফর্মে অক্ষাংশ ও দ্রাঘিমাংশ দিন। আবহাওয়া সেবায় শুধু এই অবস্থানটুকুই পাঠানো হয়।',
  'Right now': 'এখন',
  'Wind': 'বাতাস',
  'Gusts': 'দমকা হাওয়া',
  'km/h': 'কিমি/ঘণ্টা',
  'Rain': 'বৃষ্টি',
  'mm': 'মিমি',
  'Rain {p}%': 'বৃষ্টি {p}%',
  'Hottest in the next 48 hours: THI {thi} at {time}': 'আগামী ৪৮ ঘণ্টায় সবচেয়ে গরম: {time}-এ THI {thi}',
  'Source: {provider}, updated {time}. Location {lat}, {lon}.': 'উৎস: {provider}, হালনাগাদ {time}। অবস্থান {lat}, {lon}।',
  'Best hours today': 'আজ কাজের ভালো সময়',
  'Best hours tomorrow': 'আগামীকাল কাজের ভালো সময়',
  'For transport, weighing, vaccination and bathing: coolest daylight hours with little rain.':
    'পরিবহন, ওজন, টিকা ও গোসলের জন্য: দিনের সবচেয়ে ঠান্ডা ও কম বৃষ্টির সময়।',
  'No good hours left today.': 'আজ আর ভালো সময় বাকি নেই।',
  'Next 48 hours': 'আগামী ৪৮ ঘণ্টা',
  'Hourly forecast': 'ঘণ্টাভিত্তিক পূর্বাভাস',
  '7-day forecast': '৭ দিনের পূর্বাভাস',
  'What to do': 'কী করবেন',
  'Practical advice for your cattle from the forecast. General guidance: ask your vet about sick animals.':
    'পূর্বাভাস দেখে গরুর জন্য কাজের পরামর্শ। এগুলো সাধারণ নির্দেশনা: অসুস্থ পশুর জন্য পশু-চিকিৎসকের পরামর্শ নিন।',
  'No special care needed this week.': 'এই সপ্তাহে বিশেষ যত্নের দরকার নেই।',
  'THI (temperature-humidity index) = (1.8×T + 32) − (0.55 − 0.0055×RH) × (1.8×T − 26). Beef cattle: below 72 comfortable, 72–78 mild, 79–88 moderate, 89–98 severe, above 98 danger.':
    'THI (তাপমাত্রা-আর্দ্রতা সূচক) = (1.8×T + 32) − (0.55 − 0.0055×RH) × (1.8×T − 26)। মোটাতাজা গরু: 72-এর নিচে আরামদায়ক, 72–78 মৃদু, 79–88 মাঝারি, 89–98 গুরুতর, 98-এর বেশি বিপজ্জনক।',
  'Hot day: vaccinate, bathe and move animals in the morning (best before 10:00).':
    'গরমের দিন: টিকা, গোসল ও পশু সরানো সকালে করুন (সবচেয়ে ভালো 10:00-এর আগে)।',
  'Hot day: do this in the morning (best before 10:00)': 'গরমের দিন: কাজটি সকালে করুন (সবচেয়ে ভালো 10:00-এর আগে)',

  // ── Heat-stress bands ───────────────────────────────────────────────────────────────────
  'Comfortable': 'আরামদায়ক',
  'Mild': 'মৃদু',
  'Moderate': 'মাঝারি',
  'Severe': 'গুরুতর',
  'Danger': 'বিপজ্জনক',

  // ── Weather conditions (WMO codes) ──────────────────────────────────────────────────────
  'Clear sky': 'পরিষ্কার আকাশ',
  'Mostly clear': 'প্রায় পরিষ্কার আকাশ',
  'Partly cloudy': 'আংশিক মেঘলা',
  'Cloudy': 'মেঘলা',
  'Fog': 'কুয়াশা',
  'Freezing fog': 'ঘন কুয়াশা',
  'Light drizzle': 'হালকা গুঁড়ি গুঁড়ি বৃষ্টি',
  'Drizzle': 'গুঁড়ি গুঁড়ি বৃষ্টি',
  'Heavy drizzle': 'ঘন গুঁড়ি গুঁড়ি বৃষ্টি',
  'Freezing drizzle': 'ঠান্ডা গুঁড়ি গুঁড়ি বৃষ্টি',
  'Light rain': 'হালকা বৃষ্টি',
  'Heavy rain': 'ভারী বৃষ্টি',
  'Freezing rain': 'ঠান্ডা বৃষ্টি',
  'Light snow': 'হালকা তুষারপাত',
  'Snow': 'তুষারপাত',
  'Heavy snow': 'ভারী তুষারপাত',
  'Snow grains': 'তুষারকণা',
  'Light showers': 'হালকা ঝরো বৃষ্টি',
  'Showers': 'ঝরো বৃষ্টি',
  'Heavy showers': 'প্রবল ঝরো বৃষ্টি',
  'Snow showers': 'তুষার ঝরা',
  'Heavy snow showers': 'প্রবল তুষার ঝরা',
  'Thunderstorm': 'বজ্রঝড়',
  'Thunderstorm with hail': 'শিলাসহ বজ্রঝড়',
  'Thunderstorm with heavy hail': 'প্রবল শিলাসহ বজ্রঝড়',
  'Unknown weather': 'আবহাওয়া জানা নেই',

  // ── Advice (server rule texts) ──────────────────────────────────────────────────────────
  'Give plenty of cool water': 'প্রচুর ঠান্ডা পানি দিন',
  'Keep clean, cool water in front of every animal all day and refill the troughs often.':
    'সারাদিন প্রতিটি পশুর সামনে পরিষ্কার, ঠান্ডা পানি রাখুন এবং বারবার পাত্র ভরে দিন।',
  'Feed in the cool hours': 'ঠান্ডা সময়ে খাবার দিন',
  'Give concentrate early in the morning and in the evening, not at midday.':
    'দানাদার খাবার ভোরে ও সন্ধ্যায় দিন, দুপুরে নয়।',
  'Shade and fresh air': 'ছায়া ও বাতাস',
  'Keep the animals in the shade; run fans or open the shed sides for air flow.':
    'পশুদের ছায়ায় রাখুন; বাতাস চলাচলের জন্য ফ্যান চালান বা শেডের পাশ খুলে দিন।',
  'Cool the animals down': 'পশুদের শরীর ঠান্ডা করুন',
  'Bathe the animals or sprinkle water on them in the afternoon.':
    'বিকেলে পশুদের গোসল করান বা গায়ে পানি ছিটিয়ে দিন।',
  'No hard work in the heat': 'গরমে ভারী কাজ নয়',
  'Avoid transport, weighing and vaccination from {from} to {to}; do them in the morning.':
    '{from} থেকে {to} পর্যন্ত পরিবহন, ওজন মাপা ও টিকা দেওয়া এড়িয়ে চলুন; এগুলো সকালে করুন।',
  'Avoid transport, weighing and vaccination in the hot hours; do them in the morning.':
    'গরমের সময়ে পরিবহন, ওজন মাপা ও টিকা দেওয়া এড়িয়ে চলুন; এগুলো সকালে করুন।',
  'Watch for heat stress': 'গরমের চাপের লক্ষণ খেয়াল করুন',
  'Check for fast panting, open-mouth breathing or drooling; call a vet if you see them.':
    'দ্রুত হাঁপানো, মুখ খুলে শ্বাস নেওয়া বা লালা ঝরছে কিনা দেখুন; দেখা দিলে পশু-চিকিৎসক ডাকুন।',
  'Add electrolytes to the water': 'পানিতে স্যালাইন দিন',
  'Add electrolyte powder or ORS to the drinking water.': 'খাবার পানিতে ইলেক্ট্রোলাইট পাউডার বা খাবার স্যালাইন (ORS) মেশান।',
  'Keep the shed floor dry': 'শেডের মেঝে শুকনো রাখুন',
  'Keep the shed floor dry and change wet bedding.': 'শেডের মেঝে শুকনো রাখুন এবং ভেজা বিছানা বদলে দিন।',
  'Clear the drains': 'নালা পরিষ্কার করুন',
  'Clear the drains around the shed before the rain.': 'বৃষ্টির আগে শেডের চারপাশের নালা পরিষ্কার করুন।',
  'Keep feed and straw dry': 'খাবার ও খড় শুকনো রাখুন',
  'Store feed and straw covered and off the floor; damp feed grows mould.':
    'খাবার ও খড় ঢেকে, মেঝে থেকে উঁচুতে রাখুন; ভেজা খাবারে ছত্রাক জন্মায়।',
  'Ticks, flies and hooves': 'আঁঠালি, মাছি ও খুর',
  'Expect more ticks, flies and hoof problems; check the feet and the skin.':
    'আঁঠালি, মাছি ও খুরের সমস্যা বাড়তে পারে; পা ও চামড়া পরীক্ষা করুন।',
  'Skin and foot problems': 'চামড়া ও পায়ের সমস্যা',
  'Warm, damp days: watch for fungal skin and foot problems and keep the floor dry.':
    'গরম ও স্যাঁতসেঁতে দিন: চামড়া ও পায়ের ছত্রাকজনিত সমস্যা খেয়াল করুন এবং মেঝে শুকনো রাখুন।',
  'Check feed for mould': 'খাবারে ছত্রাক আছে কিনা দেখুন',
  'Check stored feed for mould; do not feed anything that smells or looks mouldy.':
    'মজুদ খাবারে ছত্রাক আছে কিনা দেখুন; গন্ধ বা ছত্রাক থাকলে সেই খাবার দেবেন না।',
  'Keep calves warm': 'বাছুরকে গরম রাখুন',
  'Cover calves and weak animals with a jute sack or blanket, especially at night.':
    'বাছুর ও দুর্বল পশুকে চটের বস্তা বা কম্বল দিয়ে ঢেকে রাখুন, বিশেষ করে রাতে।',
  'Block cold wind': 'ঠান্ডা বাতাস আটকান',
  'Block draughts: hang sacks or curtains on the open sides of the shed.':
    'ঠান্ডা বাতাস আটকান: শেডের খোলা পাশে চট বা পর্দা ঝুলিয়ে দিন।',
  'A little more energy feed': 'একটু বেশি শক্তির খাবার',
  'Give a little more energy feed, such as extra concentrate or molasses.':
    'একটু বেশি শক্তির খাবার দিন, যেমন বাড়তি দানাদার খাবার বা চিটাগুড়।',
  'Do not bathe': 'গোসল করাবেন না',
  'Do not bathe the animals on cold days; brush them instead.': 'ঠান্ডার দিনে পশুকে গোসল করাবেন না; বরং ব্রাশ করে দিন।',
  'Secure the roof': 'চাল শক্ত করে বাঁধুন',
  'Tie down the roof and tin sheets and move loose things away.': 'চাল ও টিন শক্ত করে বেঁধে দিন এবং আলগা জিনিস সরিয়ে রাখুন।',
  'Keep animals inside': 'পশুদের ভেতরে রাখুন',
  'Keep the animals inside the shed while the storm passes.': 'ঝড় না থামা পর্যন্ত পশুদের শেডের ভেতরে রাখুন।',
  'Good day for bathing, vaccination or transport': 'গোসল, টিকা বা পরিবহনের জন্য ভালো দিন',
  'Calm, mild weather: a good day to bathe, vaccinate or move animals. Give FMD in the morning or afternoon, to healthy animals only.':
    'শান্ত, সহনীয় আবহাওয়া: গোসল, টিকা বা পশু সরানোর জন্য ভালো দিন। খুরা রোগের (FMD) টিকা সকালে বা বিকেলে, শুধু সুস্থ পশুকে দিন।',

  // ── Bell alert ──────────────────────────────────────────────────────────────────────────
  'Weather alert': 'আবহাওয়া সতর্কতা',
  'Weather alerts ({count})': 'আবহাওয়া সতর্কতা ({count}টি)',
  'Storm risk today: gusts up to {value} km/h. Secure the roof and keep the animals inside.':
    'আজ ঝড়ের ঝুঁকি: দমকা হাওয়া {value} কিমি/ঘণ্টা পর্যন্ত। চাল শক্ত করে বাঁধুন, পশুদের ভেতরে রাখুন।',
  'Storm risk tomorrow: gusts up to {value} km/h. Secure the roof and keep the animals inside.':
    'আগামীকাল ঝড়ের ঝুঁকি: দমকা হাওয়া {value} কিমি/ঘণ্টা পর্যন্ত। চাল শক্ত করে বাঁধুন, পশুদের ভেতরে রাখুন।',
  'Severe heat today (THI up to {value}). Give extra water and shade; no transport or vaccination at midday.':
    'আজ তীব্র গরম (THI {value} পর্যন্ত)। বাড়তি পানি ও ছায়া দিন; দুপুরে পরিবহন বা টিকা নয়।',
  'Severe heat tomorrow (THI up to {value}). Give extra water and shade; no transport or vaccination at midday.':
    'আগামীকাল তীব্র গরম (THI {value} পর্যন্ত)। বাড়তি পানি ও ছায়া দিন; দুপুরে পরিবহন বা টিকা নয়।',
  'Heavy rain today (about {value} mm). Keep the shed dry and the feed covered.':
    'আজ ভারী বৃষ্টি (প্রায় {value} মিমি)। শেড শুকনো রাখুন, খাবার ঢেকে রাখুন।',
  'Heavy rain tomorrow (about {value} mm). Keep the shed dry and the feed covered.':
    'আগামীকাল ভারী বৃষ্টি (প্রায় {value} মিমি)। শেড শুকনো রাখুন, খাবার ঢেকে রাখুন।',
  'Cold today: down to {value}°C. Cover calves and weak animals and block cold wind.':
    'আজ ঠান্ডা: তাপমাত্রা {value}°C পর্যন্ত নামবে। বাছুর ও দুর্বল পশু ঢেকে রাখুন, ঠান্ডা বাতাস আটকান।',
  'Cold tomorrow: down to {value}°C. Cover calves and weak animals and block cold wind.':
    'আগামীকাল ঠান্ডা: তাপমাত্রা {value}°C পর্যন্ত নামবে। বাছুর ও দুর্বল পশু ঢেকে রাখুন, ঠান্ডা বাতাস আটকান।',

  // ── Farm form: location helper ──────────────────────────────────────────────────────────
  'Use my current location': 'আমার বর্তমান অবস্থান নিন',
  'Finding your location…': 'অবস্থান খোঁজা হচ্ছে…',
  'Stand at the farm and tap the button. The location gives the farm its weather forecast and heat-stress advice.':
    'খামারে দাঁড়িয়ে বোতামটি চাপুন। এই অবস্থান দিয়েই খামারের আবহাওয়ার পূর্বাভাস ও গরমের চাপের পরামর্শ দেখানো হয়।',
  'How to copy the location from Google Maps': 'Google Maps থেকে অবস্থান কীভাবে কপি করবেন',
  'Open Google Maps and find your farm.': 'Google Maps খুলে আপনার খামার খুঁজে বের করুন।',
  'Phone: touch and hold the farm until a red pin drops. Computer: right-click the farm.':
    'ফোনে: খামারের জায়গায় চেপে ধরে রাখুন, লাল পিন পড়বে। কম্পিউটারে: খামারের জায়গায় রাইট-ক্লিক করুন।',
  'The coordinates appear like 23.8103, 90.4125 (tap or click them to copy).':
    'অবস্থানটি 23.8103, 90.4125 এর মতো দেখাবে (কপি করতে এতে চাপুন বা ক্লিক করুন)।',
  'Put the first number in Latitude and the second in Longitude.': 'প্রথম সংখ্যাটি অক্ষাংশে এবং দ্বিতীয়টি দ্রাঘিমাংশে লিখুন।',
  'This point is outside Bangladesh. Check that latitude (about 20.5 to 26.6) and longitude (about 88 to 92.7) are not swapped.':
    'এই অবস্থানটি বাংলাদেশের বাইরে। অক্ষাংশ (প্রায় 20.5 থেকে 26.6) ও দ্রাঘিমাংশ (প্রায় 88 থেকে 92.7) উল্টে গেছে কিনা দেখুন।',
  'This browser cannot share its location. Copy the point from Google Maps instead.':
    'এই ব্রাউজার অবস্থান জানাতে পারে না। বরং Google Maps থেকে অবস্থান কপি করুন।',
  'Location permission was refused. Allow it in the browser, or copy the point from Google Maps.':
    'অবস্থানের অনুমতি দেওয়া হয়নি। ব্রাউজারে অনুমতি দিন, অথবা Google Maps থেকে অবস্থান কপি করুন।',
  'Could not read your location. Try again outdoors, or copy the point from Google Maps.':
    'অবস্থান পাওয়া যায়নি। খোলা জায়গায় আবার চেষ্টা করুন, অথবা Google Maps থেকে অবস্থান কপি করুন।',
  'Change location': 'অবস্থান বদলান',
};
