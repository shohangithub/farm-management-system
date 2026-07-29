# Breed Intelligence Reference

This document serves as the master reference for cattle breed data, growth metrics, feed conversion ratios (FCR), and expected daily weight gains based on farming conditions in Bangladesh. This data will be seeded into the `Breed` entity to drive the Smart Farm Intelligence Engines.

## 1. Expected Daily Gain by Farming Condition
The environment and management quality significantly impact a breed's ability to reach its genetic potential.

| Farming Condition | Expected Daily Gain | Description |
| :--- | :--- | :--- |
| **Poor management** | `0.2 - 0.4 kg/day` | Basic subsistence farming, minimal nutritional planning. |
| **Average farm** | `0.4 - 0.7 kg/day` | Standard smallholder farm, moderate feed quality. |
| **Good commercial farm** | `0.7 - 1.0 kg/day` | Professional management, balanced rations, good health protocols. |
| **Intensive beef fattening** | `1.0 - 1.5 kg/day` | High-energy diets, excellent genetics, strict monitoring. |

## 2. Feed Conversion Ratios (Approximate)
Feed Required for 1 kg Weight Gain (Dry Matter). Lower is better (more efficient).

| Breed / Type | FCR (Dry Matter per 1 kg gain) |
| :--- | :--- |
| **Deshi (Native)** | `8 - 10 kg` |
| **Red Chittagong (RCC)** | `7 - 9 kg` |
| **Holstein Cross** | `6 - 8 kg` |
| **Brahman Cross** | `5 - 7 kg` |

## 3. Breed Master Data (Categorized)

### Indigenous (Native) Cattle
Hardy, disease-resistant, adapted to local climate.

| Breed | Daily Weight Gain | Milk per Day | Fat % | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Deshi (Local)** | 0.2 - 0.4 kg | 1 - 3 L | 4.5 - 5.5% | Low-cost farming |
| **Red Chittagong (RCC)** | 0.3 - 0.5 kg | 2 - 5 L | 4.5 - 5.0% | Small dairy farms |
| **Pabna** | 0.3 - 0.5 kg | 5 - 10 L | 4.0 - 4.5% | Native dairy |

### Exotic (Imported) Breeds
High yield for milk or meat.

| Breed | Daily Weight Gain | Milk per Day | Fat % | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Holstein Friesian** | 0.6 - 1.0 kg | 20 - 35 L | 3.4 - 3.8% | High-volume commercial dairy |
| **Jersey** | 0.5 - 0.8 kg | 15 - 25 L | 4.8 - 5.5% | Premium milk (high butterfat) |
| **Sahiwal** | 0.5 - 0.8 kg | 8 - 15 L | 4.5 - 5.0% | Heat-tolerant dairy |
| **Red Sindhi** | 0.4 - 0.7 kg | 8 - 12 L | 4.5 - 5.0% | Dairy |
| **Hariana** | 0.4 - 0.7 kg | 6 - 10 L | 4.0 - 4.5% | Dual-purpose |
| **Brahman** | 0.8 - 1.2 kg | 2 - 5 L | 4.0 - 4.5% | Beef |

### Crossbred Cattle
Balanced approach combining local hardiness with exotic yields.

| Breed | Daily Weight Gain | Milk per Day | Fat % | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Holstein × Deshi** | 0.7 - 1.0 kg | 12 - 25 L | 3.8 - 4.2% | Commercial dairy |
| **Jersey × Deshi** | 0.5 - 0.8 kg | 8 - 18 L | 4.5 - 5.2% | Quality milk |
| **Sahiwal × Deshi** | 0.6 - 0.9 kg | 8 - 15 L | 4.2 - 4.8% | Balanced dairy |
| **Brahman × Deshi** | 0.9 - 1.3 kg | 2 - 6 L | 4.0 - 4.5% | Beef with limited milk |

## 4. Practical Calculations for Engines

**Growth Prediction Engine:**
- **Formula:** `Projected Weight = Current Weight + (Target ADG * Days)`
- **Target ADG Selection:** Target ADG is determined by querying the `Breed` entity for its configured `DailyWeightGain_Max` or `DailyWeightGain_Min` based on the Farm's assigned `FarmingCondition` level.

**Cost & Profit Engine (Feed Forecasting):**
- **Formula:** `Daily Feed Dry Matter (kg) = Target ADG * Breed FCR`
- Example: 200 kg Brahman × Deshi gaining 1.1 kg/day.
- Daily Feed DM = `1.1 kg * 6 kg (Avg FCR)` = `6.6 kg Dry Matter / day`.
- Forecasted Cost = `Daily Feed DM * Cost per Kg of Feed`.
