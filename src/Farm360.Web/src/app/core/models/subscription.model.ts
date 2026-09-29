export interface TenantSubscriptionStatus {
  tenantId: string;
  tenantName: string;
  tier: string;
  billingCycle: string;
  status: string;
  isTrial: boolean;
  trialDays: number | null;
  trialEndsAtUtc: string | null;
  trialDaysRemaining: number;
  subscriptionExpiresAtUtc: string | null;
  daysRemaining: number | null;
  hasUsedTrial: boolean;
  maxUsers: number;
  currentUsers: number;
  maxFarms: number;
  currentFarms: number;
  maxAnimals: number;
  currentAnimals: number;
}

export interface SubscriptionPlan {
  tier: string;
  name: string;
  description: string;
  monthlyPrice: number;
  yearlyPrice: number;
  oneTimePrice: number;
  maxUsers: number;
  maxFarms: number;
  maxAnimals: number;
  features: string[];
  isPopular?: boolean;
}

export interface TrialOption {
  days: number;
  title: string;
  description: string;
}

export interface SubscriptionCatalog {
  plans: SubscriptionPlan[];
  trialOptions: TrialOption[];
}

export interface TenantSubscriptionRecord {
  id: string;
  tier: string;
  billingCycle: string;
  amount: number;
  currency: string;
  startedAtUtc: string;
  expiresAtUtc: string | null;
  paymentMethod: string;
  paymentReference: string | null;
  status: string;
  invoiceNumber: string;
  notes: string | null;
  createdAtUtc: string;
}

export interface SubscribeRequest {
  tier: number;
  billingCycle: number;
  paymentMethod: string;
  paymentReference?: string;
  notes?: string;
}

export interface StartTrialRequest {
  trialDays: number;
}
