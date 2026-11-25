import { BN_COMMON } from './common';
import { BN_AUTH } from './auth';
import { BN_LAYOUT } from './layout';
import { BN_SHARED } from './shared';
import { BN_DASHBOARD } from './dashboard';
import { BN_ORGANIZATIONS } from './organizations';
import { BN_FEEDING } from './feeding';
import { BN_FINANCE } from './finance';
import { BN_STATEMENTS } from './statements';
import { BN_HEALTH } from './health';
import { BN_INVENTORY } from './inventory';
import { BN_LIVESTOCK } from './livestock';
import { BN_REPORTS } from './reports';
import { BN_SETTINGS } from './settings';
import { BN_INTELLIGENCE } from './intelligence';

/**
 * The whole Bangla dictionary, loaded only when the interface is in Bangla.
 * Later files win when two define the same English text; keep shared words in common.ts.
 */
export const BN: Record<string, string> = {
  ...BN_COMMON,
  ...BN_AUTH,
  ...BN_LAYOUT,
  ...BN_SHARED,
  ...BN_DASHBOARD,
  ...BN_ORGANIZATIONS,
  ...BN_FEEDING,
  ...BN_FINANCE,
  ...BN_STATEMENTS,
  ...BN_HEALTH,
  ...BN_INVENTORY,
  ...BN_LIVESTOCK,
  ...BN_REPORTS,
  ...BN_SETTINGS,
  ...BN_INTELLIGENCE,
};
