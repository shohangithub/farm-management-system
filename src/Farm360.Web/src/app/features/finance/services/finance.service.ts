import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  FinancialTransaction, 
  RecordIncomeRequest,
  RecordExpenseRequest,
  LoanRecord,
  CreateLoanRecordRequest,
  RecordLoanRepaymentRequest,
  AnimalCostLedger,
  BreakEvenCalculator,
  BatchPnLReport,
  MonthlyPnLReport,
  ConsolidatedPnLReport,
  FinancialDashboard
} from '../models/finance.model';

@Injectable({
  providedIn: 'root'
})
export class FinanceService {
  private http = inject(HttpClient);
  
  // Note: Old base url was /api/v1/farms, but new API maps to /api/farms/{farmId}/finance
  private getBaseUrl(farmId: string): string {
    return `/api/farms/${farmId}/finance`;
  }

  // --- Transactions ---

  getTransactions(farmId: string): Observable<FinancialTransaction[]> {
    return this.http.get<FinancialTransaction[]>(`${this.getBaseUrl(farmId)}/transactions`);
  }

  recordIncome(farmId: string, request: RecordIncomeRequest): Observable<FinancialTransaction> {
    return this.http.post<FinancialTransaction>(`${this.getBaseUrl(farmId)}/income`, this.sanitizePayload(request));
  }

  recordExpense(farmId: string, request: RecordExpenseRequest): Observable<FinancialTransaction> {
    return this.http.post<FinancialTransaction>(`${this.getBaseUrl(farmId)}/expense`, this.sanitizePayload(request));
  }

  // --- Loans ---

  getLoans(farmId: string): Observable<LoanRecord[]> {
    return this.http.get<LoanRecord[]>(`${this.getBaseUrl(farmId)}/loans`);
  }

  createLoan(farmId: string, request: CreateLoanRecordRequest): Observable<LoanRecord> {
    return this.http.post<LoanRecord>(`${this.getBaseUrl(farmId)}/loans`, this.sanitizePayload(request));
  }

  recordLoanRepayment(farmId: string, loanId: string, request: RecordLoanRepaymentRequest): Observable<LoanRecord> {
    return this.http.post<LoanRecord>(`${this.getBaseUrl(farmId)}/loans/${loanId}/repayments`, this.sanitizePayload(request));
  }

  // --- Animal Ledger ---

  getAnimalCostLedger(farmId: string, animalId: string): Observable<AnimalCostLedger> {
    return this.http.get<AnimalCostLedger>(`${this.getBaseUrl(farmId)}/animals/${animalId}/ledger`);
  }

  getBreakEven(farmId: string, animalId: string): Observable<BreakEvenCalculator> {
    return this.http.get<BreakEvenCalculator>(`${this.getBaseUrl(farmId)}/animals/${animalId}/breakeven`);
  }

  // --- Reports & Dashboard ---

  getBatchPnL(farmId: string, batchId: string): Observable<BatchPnLReport> {
    return this.http.get<BatchPnLReport>(`${this.getBaseUrl(farmId)}/reports/batch/${batchId}/pnl`);
  }

  getMonthlyPnL(farmId: string, year: number, month: number): Observable<MonthlyPnLReport> {
    return this.http.get<MonthlyPnLReport>(`${this.getBaseUrl(farmId)}/reports/monthly?year=${year}&month=${month}`);
  }

  getConsolidatedPnL(year: number, month: number): Observable<ConsolidatedPnLReport> {
    // This is tenant-wide, so it goes to /api/finance
    return this.http.get<ConsolidatedPnLReport>(`/api/finance/reports/consolidated?year=${year}&month=${month}`);
  }

  getDashboard(farmId: string): Observable<FinancialDashboard> {
    return this.http.get<FinancialDashboard>(`${this.getBaseUrl(farmId)}/dashboard`);
  }

  // --- Helpers ---

  /**
   * Sanitizes the payload to prevent BadHttpRequestException deserialization errors
   * as per AGENTS.md rule: Form submit handlers MUST sanitize empty string fields ("") to null
   */
  private sanitizePayload<T>(payload: any): T {
    const sanitized = { ...payload };
    for (const key in sanitized) {
      if (sanitized[key] === '') {
        sanitized[key] = null;
      }
    }
    return sanitized as T;
  }
}
