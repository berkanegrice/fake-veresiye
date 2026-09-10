export type TransactionKind = "Debt" | "Payment";

/** Wire value for TransactionType on the API (1 = Debt, 2 = Payment). */
export const TransactionTypeValue = { Debt: 1, Payment: 2 } as const;

/** A single page of results plus the totals a pager needs. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export type LedgerSortField = "date" | "amount" | "type";
export type SortDir = "asc" | "desc";

export interface CustomerListItem {
  id: number;
  name: string;
  phone: string | null;
  balance: number;
}

export interface TransactionResponse {
  id: number;
  type: TransactionKind;
  amount: number;
  description: string | null;
  transactionDate: string;
}

export interface CustomerDetail {
  id: number;
  name: string;
  phone: string | null;
  notes: string | null;
  balance: number;
  transactionCount: number;
}

export interface ValidationResult {
  errors: string[];
  warnings: string[];
  isValid: boolean;
}

export interface ImportPreview {
  token: string;
  fileName: string;
  customers: number;
  transactions: number;
  totalDebt: number;
  totalPayments: number;
  outstanding: number;
  validation: ValidationResult;
  customerPreview: { id: number; name: string; phone: string | null }[];
  transactionPreview: unknown[];
}

export interface ImportResult {
  customers: number;
  sourceTransactions: number;
  importedTransactions: number;
}

export interface StatementLine {
  transactionId: number;
  date: string;
  type: TransactionKind;
  description: string | null;
  debt: number;
  payment: number;
  runningBalance: number;
}

export interface Statement {
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  from: string;
  to: string;
  openingBalance: number;
  totalDebt: number;
  totalPayment: number;
  closingBalance: number;
  lines: StatementLine[];
  page: number;
  pageSize: number;
  lineTotal: number;
  totalPages: number;
}
