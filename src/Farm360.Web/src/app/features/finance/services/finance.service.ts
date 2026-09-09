import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { 
  FinancialTransaction, 
  PagedFinancialTransactionsResult,
  FinancialTransactionParams,
  RecordIncomeRequest,
  RecordExpenseRequest,
  UpdateFinancialTransactionRequest,
  LoanRecord,
  CreateLoanRecordRequest,
  RecordLoanRepaymentRequest,
  AnimalCostLedger,
  BreakEvenCalculator,
  BatchPnLReport,
  MonthlyPnLReport,
  ConsolidatedPnLReport,
  FinancialDashboard,
  Investor,
  InvestorTransaction,
  CreateInvestorRequest,
  UpdateInvestorRequest,
  RecordInvestorTransactionRequest,
  InvestorPnLSummary
} from '../models/finance.model';

@Injectable({
  providedIn: 'root'
})
export class FinanceService {
  private http = inject(HttpClient);
  
  private getBaseUrl(farmId: string): string {
    return `/api/farms/${farmId}/finance`;
  }

  // --- Transactions ---

  getPagedTransactions(farmId: string, params?: FinancialTransactionParams): Observable<PagedFinancialTransactionsResult> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.pageNumber) httpParams = httpParams.set('pageNumber', params.pageNumber.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
      if (params.search) httpParams = httpParams.set('search', params.search);
      if (params.type) httpParams = httpParams.set('type', params.type);
      if (params.category) httpParams = httpParams.set('category', params.category);
      if (params.startDate) httpParams = httpParams.set('startDate', params.startDate);
      if (params.endDate) httpParams = httpParams.set('endDate', params.endDate);
      if (params.animalId) httpParams = httpParams.set('animalId', params.animalId);
      if (params.batchId) httpParams = httpParams.set('batchId', params.batchId);
      if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
      if (params.sortDesc !== undefined) httpParams = httpParams.set('sortDesc', params.sortDesc.toString());
      if (params.isAutomated !== undefined) httpParams = httpParams.set('isAutomated', params.isAutomated.toString());
      if (params.sourceModule) httpParams = httpParams.set('sourceModule', params.sourceModule);
    }
    return this.http.get<PagedFinancialTransactionsResult>(`${this.getBaseUrl(farmId)}/transactions`, { params: httpParams });
  }

  getTransactions(farmId: string): Observable<FinancialTransaction[]> {
    return this.getPagedTransactions(farmId, { pageNumber: 1, pageSize: 1000 }).pipe(
      map(res => res.items)
    );
  }

  getTransactionById(farmId: string, id: string): Observable<FinancialTransaction> {
    return this.http.get<FinancialTransaction>(`${this.getBaseUrl(farmId)}/transactions/${id}`);
  }

  recordIncome(farmId: string, request: RecordIncomeRequest): Observable<FinancialTransaction> {
    return this.http.post<FinancialTransaction>(`${this.getBaseUrl(farmId)}/income`, this.sanitizePayload(request));
  }

  recordExpense(farmId: string, request: RecordExpenseRequest): Observable<FinancialTransaction> {
    return this.http.post<FinancialTransaction>(`${this.getBaseUrl(farmId)}/expense`, this.sanitizePayload(request));
  }

  updateTransaction(farmId: string, id: string, request: UpdateFinancialTransactionRequest): Observable<FinancialTransaction> {
    return this.http.put<FinancialTransaction>(`${this.getBaseUrl(farmId)}/transactions/${id}`, this.sanitizePayload(request));
  }

  deleteTransaction(farmId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${this.getBaseUrl(farmId)}/transactions/${id}`);
  }

  exportTransactionsCsv(farmId: string, params?: FinancialTransactionParams): Observable<Blob> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.search) httpParams = httpParams.set('search', params.search);
      if (params.type) httpParams = httpParams.set('type', params.type);
      if (params.category) httpParams = httpParams.set('category', params.category);
      if (params.startDate) httpParams = httpParams.set('startDate', params.startDate);
      if (params.endDate) httpParams = httpParams.set('endDate', params.endDate);
      if (params.isAutomated !== undefined) httpParams = httpParams.set('isAutomated', params.isAutomated.toString());
      if (params.sourceModule) httpParams = httpParams.set('sourceModule', params.sourceModule);
    }
    return this.http.get(`${this.getBaseUrl(farmId)}/transactions/export`, {
      params: httpParams,
      responseType: 'blob'
    });
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

  // --- Investors & Profit Sharing ---

  getInvestors(farmId: string, includeInactive: boolean = false): Observable<Investor[]> {
    const params = new HttpParams().set('includeInactive', includeInactive.toString());
    return this.http.get<Investor[]>(`${this.getBaseUrl(farmId)}/investors`, { params });
  }

  getInvestorById(farmId: string, id: string): Observable<Investor> {
    return this.http.get<Investor>(`${this.getBaseUrl(farmId)}/investors/${id}`);
  }

  createInvestor(farmId: string, request: CreateInvestorRequest): Observable<Investor> {
    return this.http.post<Investor>(`${this.getBaseUrl(farmId)}/investors`, this.sanitizePayload(request));
  }

  updateInvestor(farmId: string, id: string, request: UpdateInvestorRequest): Observable<Investor> {
    return this.http.put<Investor>(`${this.getBaseUrl(farmId)}/investors/${id}`, this.sanitizePayload(request));
  }

  toggleInvestorStatus(farmId: string, id: string, isActive: boolean): Observable<Investor> {
    return this.http.patch<Investor>(`${this.getBaseUrl(farmId)}/investors/${id}/status?isActive=${isActive}`, {});
  }

  recordInvestorTransaction(farmId: string, investorId: string, request: RecordInvestorTransactionRequest): Observable<InvestorTransaction> {
    return this.http.post<InvestorTransaction>(`${this.getBaseUrl(farmId)}/investors/${investorId}/transactions`, this.sanitizePayload(request));
  }

  getInvestorPnL(farmId: string, fromDate?: string, toDate?: string): Observable<InvestorPnLSummary> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return this.http.get<InvestorPnLSummary>(`${this.getBaseUrl(farmId)}/investors/pnl`, { params });
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
