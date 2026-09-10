import type {
  CustomerDetail,
  CustomerListItem,
  ImportPreview,
  ImportResult,
  LedgerSortField,
  Paged,
  SortDir,
  Statement,
  TransactionResponse,
  ValidationResult,
} from "./types";

const BASE = import.meta.env.VITE_API_BASE ?? "/api";

export class ApiError extends Error {}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(BASE + path, init);
  const text = await res.text();
  if (!res.ok) {
    let message = text || res.statusText;
    try {
      const parsed = JSON.parse(text);
      message = parsed.error ?? parsed.title ?? message;
    } catch {
      /* keep raw text */
    }
    throw new ApiError(message);
  }
  return (text ? JSON.parse(text) : null) as T;
}

function json(body: unknown): RequestInit {
  return {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  };
}

export interface TransactionInput {
  type: number;
  amount: number;
  transactionDate: string;
  description: string;
}

export interface TransactionEdit {
  amount: number;
  transactionDate: string;
  description: string;
}

export interface CustomerListParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

export interface LedgerParams {
  page?: number;
  pageSize?: number;
  sort?: LedgerSortField;
  dir?: SortDir;
}

function qs(params: Record<string, string | number | undefined>): string {
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== "") sp.set(k, String(v));
  }
  const s = sp.toString();
  return s ? `?${s}` : "";
}

export const api = {
  listCustomers: (params: CustomerListParams = {}) =>
    request<Paged<CustomerListItem>>(
      `/customers${qs({ page: params.page, pageSize: params.pageSize, search: params.search })}`,
    ),

  getCustomer: (id: number) => request<CustomerDetail>(`/customers/${id}`),

  listTransactions: (customerId: number, params: LedgerParams = {}) =>
    request<Paged<TransactionResponse>>(
      `/customers/${customerId}/transactions${qs({
        page: params.page,
        pageSize: params.pageSize,
        sort: params.sort,
        dir: params.dir,
      })}`,
    ),

  createCustomer: (input: { name: string; phone: string; notes?: string }) =>
    request<CustomerListItem>("/customers", json(input)),

  deleteCustomer: (id: number) =>
    request<null>(`/customers/${id}`, { method: "DELETE" }),

  addTransaction: (customerId: number, input: TransactionInput) =>
    request<TransactionResponse>(`/customers/${customerId}/transactions`, json(input)),

  updateTransaction: (id: number, input: TransactionEdit) =>
    request<TransactionResponse>(`/transactions/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    }),

  deleteTransaction: (id: number) =>
    request<null>(`/transactions/${id}`, { method: "DELETE" }),

  previewExa: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return request<ImportPreview>("/import/exa/preview", { method: "POST", body: form });
  },

  validateExa: (token: string) =>
    request<ValidationResult>(`/import/exa/validate/${token}`, { method: "POST" }),

  importExa: (token: string) =>
    request<ImportResult>(`/import/exa/import/${token}`, { method: "POST" }),

  getStatement: (
    customerId: number,
    from: string,
    to: string,
    page = 1,
    pageSize = 50,
  ) =>
    request<Statement>(
      `/reports/customers/${customerId}/statement${qs({ from, to, page, pageSize })}`,
    ),

  statementFileUrl: (customerId: number, format: "xlsx" | "pdf", from: string, to: string) => {
    const q = new URLSearchParams();
    if (from) q.set("from", from);
    if (to) q.set("to", to);
    const qs = q.toString();
    return `${BASE}/reports/customers/${customerId}/statement.${format}${qs ? `?${qs}` : ""}`;
  },
};
