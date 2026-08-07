export interface FinancialTransaction {
  id: string;
  farmId: string;
  type: string;
  category: string;
  amountBdt: number;
  transactionDate: string;
  referenceId?: string;
  notes?: string;
  createdAtUtc: string;
}

export interface FinancialTransactionSummary {
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netBalanceBdt: number;
}

export interface CreateFinancialTransactionRequest {
  type: string;
  category: string;
  amountBdt: number;
  transactionDate: string;
  referenceId?: string;
  notes?: string;
}
