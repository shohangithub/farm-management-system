import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  FinancialTransaction, 
  FinancialTransactionSummary, 
  CreateFinancialTransactionRequest 
} from '../models/finance.model';

@Injectable({
  providedIn: 'root'
})
export class FinanceService {
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/farms';

  getTransactions(farmId: string): Observable<FinancialTransaction[]> {
    return this.http.get<FinancialTransaction[]>(`${this.baseUrl}/${farmId}/financial-transactions`);
  }

  getSummary(farmId: string): Observable<FinancialTransactionSummary> {
    return this.http.get<FinancialTransactionSummary>(`${this.baseUrl}/${farmId}/financial-transactions/summary`);
  }

  createTransaction(farmId: string, request: CreateFinancialTransactionRequest): Observable<FinancialTransaction> {
    // Sanitize payload to handle empty strings
    const payload = {
      ...request,
      referenceId: request.referenceId === '' ? null : request.referenceId,
      notes: request.notes === '' ? null : request.notes
    };
    return this.http.post<FinancialTransaction>(`${this.baseUrl}/${farmId}/financial-transactions`, payload);
  }
}
