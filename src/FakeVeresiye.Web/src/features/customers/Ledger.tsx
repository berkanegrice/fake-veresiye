import { useEffect, useState } from "react";
import type { LedgerSortField, Paged, SortDir, TransactionResponse } from "../../api/types";
import { api, ApiError } from "../../api/client";
import { useI18n } from "../../i18n";
import { money, formatDate } from "../../format";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { Pager } from "../../components/Pager";
import { TransactionFormDialog } from "../transactions/TransactionFormDialog";

const PAGE_SIZES = [10, 25, 50, 100];

interface LedgerProps {
  customerId: number;
  reloadSignal: number;
  onChanged: () => void;
}

export function Ledger({ customerId, reloadSignal, onChanged }: LedgerProps) {
  const { t, locale } = useI18n();

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [sort, setSort] = useState<LedgerSortField>("date");
  const [dir, setDir] = useState<SortDir>("desc");
  const [data, setData] = useState<Paged<TransactionResponse> | null>(null);
  const [error, setError] = useState("");
  const [localReload, setLocalReload] = useState(0);

  const [editing, setEditing] = useState<TransactionResponse | null>(null);
  const [deleting, setDeleting] = useState<TransactionResponse | null>(null);

  // Reset to the first page whenever the ordering, page size or customer changes.
  useEffect(() => {
    setPage(1);
  }, [sort, dir, pageSize, customerId]);

  useEffect(() => {
    let cancelled = false;
    setError("");
    api
      .listTransactions(customerId, { page, pageSize, sort, dir })
      .then((result) => {
        if (cancelled) return;
        setData(result);
        // A delete can empty the last page; step back if so.
        if (result.items.length === 0 && result.page > 1) setPage(result.page - 1);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof ApiError ? e.message : String(e));
      });
    return () => {
      cancelled = true;
    };
  }, [customerId, page, pageSize, sort, dir, reloadSignal, localReload]);

  const changeSort = (field: LedgerSortField) => {
    if (field === sort) {
      setDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSort(field);
      setDir("desc");
    }
  };

  const afterMutation = () => {
    setLocalReload((n) => n + 1);
    onChanged();
  };

  const arrow = (field: LedgerSortField) => (field === sort ? (dir === "asc" ? " ▲" : " ▼") : "");
  const items = data?.items ?? [];

  return (
    <div className="ledger">
      <div className="ledger-head">
        <h3>{t("ledger.transactions")}</h3>
        <div className="sort-bar">
          {(["date", "amount", "type"] as const).map((field) => (
            <button
              type="button"
              key={field}
              className={field === sort ? "active" : ""}
              onClick={() => changeSort(field)}
            >
              {t(
                field === "date"
                  ? "ledger.sortDate"
                  : field === "amount"
                    ? "ledger.sortAmount"
                    : "ledger.sortType",
              )}
              {arrow(field)}
            </button>
          ))}
        </div>
      </div>

      {data && (data.totalPages > 1 || pageSize !== 25) && (
        <Pager page={data.page} totalPages={data.totalPages} onChange={setPage}>
          <select
            className="size"
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value))}
            aria-label={t("common.perPage")}
          >
            {PAGE_SIZES.map((n) => (
              <option key={n} value={n}>
                {n} {t("common.perPage")}
              </option>
            ))}
          </select>
        </Pager>
      )}

      {error && <p className="error">{error}</p>}
      {!error && items.length === 0 && <p className="muted">{t("ledger.noTransactions")}</p>}

      {items.map((tx) => (
        <div className={`ledger-row ${tx.type === "Debt" ? "debt" : "payment"}`} key={tx.id}>
          <span className="ledger-main">
            <b className={tx.type === "Debt" ? "tag-debt" : "tag-payment"}>
              {tx.type === "Debt" ? t("tx.debt") : t("tx.payment")}
            </b>
            <small>
              {formatDate(tx.transactionDate, locale)}
              {tx.description ? ` · ${tx.description}` : ""}
            </small>
          </span>
          <strong className={tx.type === "Debt" ? "amount-debt" : "amount-payment"}>
            {tx.type === "Debt" ? "+" : "−"}
            {money(tx.amount)}
          </strong>
          <span className="ledger-actions">
            <button
              type="button"
              className="icon"
              title={t("common.save")}
              onClick={() => setEditing(tx)}
            >
              ✎
            </button>
            <button
              type="button"
              className="icon"
              title={t("common.delete")}
              onClick={() => setDeleting(tx)}
            >
              ×
            </button>
          </span>
        </div>
      ))}

      {editing && (
        <TransactionFormDialog
          mode={{ kind: "edit", transaction: editing }}
          onClose={() => setEditing(null)}
          onSaved={afterMutation}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title={t("common.delete")}
          message={t("tx.deleteConfirm")}
          danger
          onConfirm={async () => {
            await api.deleteTransaction(deleting.id);
            afterMutation();
          }}
          onClose={() => setDeleting(null)}
        />
      )}
    </div>
  );
}
