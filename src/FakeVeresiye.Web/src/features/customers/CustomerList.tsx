import { useEffect, useState } from "react";
import type { Paged, CustomerListItem } from "../../api/types";
import { api, ApiError } from "../../api/client";
import { useT } from "../../i18n";
import { money } from "../../format";
import { useDebouncedValue } from "../../hooks/useDebouncedValue";
import { Pager } from "../../components/Pager";
import { CustomerFormDialog } from "./CustomerFormDialog";

const PAGE_SIZE = 25;

interface CustomerListProps {
  selectedId: number | null;
  reloadSignal: number;
  onSelect: (id: number) => void;
  onCreated: (id: number) => void;
}

export function CustomerList({ selectedId, reloadSignal, onSelect, onCreated }: CustomerListProps) {
  const t = useT();
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search.trim(), 250);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<CustomerListItem> | null>(null);
  const [error, setError] = useState("");
  const [adding, setAdding] = useState(false);

  // Any new search term or external change starts back at page 1.
  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, reloadSignal]);

  useEffect(() => {
    let cancelled = false;
    setError("");
    api
      .listCustomers({ page, pageSize: PAGE_SIZE, search: debouncedSearch })
      .then((result) => {
        if (!cancelled) setData(result);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof ApiError ? e.message : String(e));
      });
    return () => {
      cancelled = true;
    };
  }, [page, debouncedSearch, reloadSignal]);

  const items = data?.items ?? [];

  return (
    <aside className="sidebar">
      <div className="sidebar-top">
        <input
          className="search"
          placeholder={t("common.search")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button type="button" className="primary" onClick={() => setAdding(true)}>
          {t("customer.add")}
        </button>
      </div>

      <div className="customer-list">
        {error && <p className="error">{error}</p>}
        {!error && items.length === 0 && <p className="muted pad">{t("customer.none")}</p>}
        {items.map((c) => (
          <button
            type="button"
            key={c.id}
            className={`customer-item${c.id === selectedId ? " selected" : ""}`}
            onClick={() => onSelect(c.id)}
          >
            <span>
              <b>{c.name}</b>
              {c.phone && <small>{c.phone}</small>}
            </span>
            <strong className={c.balance > 0 ? "amount-debt" : ""}>{money(c.balance)}</strong>
          </button>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <Pager page={data.page} totalPages={data.totalPages} onChange={setPage} />
      )}

      {adding && (
        <CustomerFormDialog
          onClose={() => setAdding(false)}
          onCreated={(id) => {
            setAdding(false);
            setSearch("");
            onCreated(id);
          }}
        />
      )}
    </aside>
  );
}
