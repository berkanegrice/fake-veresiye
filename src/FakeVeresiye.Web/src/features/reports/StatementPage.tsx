import { useEffect, useRef, useState } from "react";
import type { CustomerListItem, Statement } from "../../api/types";
import { api, ApiError } from "../../api/client";
import { useI18n } from "../../i18n";
import { money, formatDate, todayInput } from "../../format";
import { useDebouncedValue } from "../../hooks/useDebouncedValue";
import { Pager } from "../../components/Pager";

const PAGE_SIZE = 50;

interface StatementPageProps {
  initialCustomerId: number | null;
}

export function StatementPage({ initialCustomerId }: StatementPageProps) {
  const { t, locale } = useI18n();

  const [customerId, setCustomerId] = useState<number | null>(null);
  const [customerLabel, setCustomerLabel] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search.trim(), 250);
  const [results, setResults] = useState<CustomerListItem[]>([]);
  const [open, setOpen] = useState(false);

  const [from, setFrom] = useState("");
  const [to, setTo] = useState(todayInput());
  const [statement, setStatement] = useState<Statement | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const fromRef = useRef(from);
  fromRef.current = from;
  const toRef = useRef(to);
  toRef.current = to;

  const loadStatement = async (id: number, page: number) => {
    setBusy(true);
    setError("");
    try {
      const result = await api.getStatement(id, fromRef.current, toRef.current, page, PAGE_SIZE);
      setStatement(result);
      if (!fromRef.current) setFrom(result.from.slice(0, 10));
    } catch (e) {
      setStatement(null);
      setError(e instanceof ApiError ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  // Opened from a customer's "Ekstre aç" button.
  useEffect(() => {
    if (initialCustomerId == null) return;
    api.getCustomer(initialCustomerId).then((c) => {
      setCustomerId(c.id);
      setCustomerLabel(c.name);
      setSearch(c.name);
      void loadStatement(c.id, 1);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialCustomerId]);

  // Live customer search for the picker.
  useEffect(() => {
    if (!open || !debouncedSearch || debouncedSearch === customerLabel) {
      return;
    }
    let cancelled = false;
    api.listCustomers({ search: debouncedSearch, pageSize: 20 }).then((r) => {
      if (!cancelled) setResults(r.items);
    });
    return () => {
      cancelled = true;
    };
  }, [open, debouncedSearch, customerLabel]);

  const pick = (c: CustomerListItem) => {
    setCustomerId(c.id);
    setCustomerLabel(c.name);
    setSearch(c.name);
    setOpen(false);
    void loadStatement(c.id, 1);
  };

  const run = () => {
    if (customerId != null) void loadStatement(customerId, 1);
  };

  const goToPage = (page: number) => {
    if (customerId != null) void loadStatement(customerId, page);
  };

  const fileUrl = (format: "xlsx" | "pdf") =>
    customerId != null ? api.statementFileUrl(customerId, format, from, to) : "#";

  return (
    <section className="statement-page">
      <h2>{t("report.title")}</h2>

      <div className="statement-controls">
        <label className="combobox">
          <span>{t("report.customer")}</span>
          <input
            type="text"
            placeholder={t("report.searchCustomer")}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setOpen(true);
              setCustomerId(null);
            }}
            onFocus={() => setOpen(true)}
            onBlur={() => window.setTimeout(() => setOpen(false), 150)}
          />
          {open && results.length > 0 && (
            <div className="combobox-list">
              {results.map((c) => (
                <button type="button" key={c.id} className="customer-item" onMouseDown={() => pick(c)}>
                  <span>
                    <b>{c.name}</b>
                    {c.phone && <small>{c.phone}</small>}
                  </span>
                </button>
              ))}
            </div>
          )}
        </label>

        <label>
          <span>{t("report.from")}</span>
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label>
          <span>{t("report.to")}</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
        <button
          type="button"
          className="primary"
          onClick={run}
          disabled={busy || customerId == null}
        >
          {t("report.run")}
        </button>
        {customerId != null && statement && (
          <span className="export-links">
            <a href={fileUrl("xlsx")} target="_blank" rel="noreferrer">
              {t("report.exportExcel")}
            </a>
            <a href={fileUrl("pdf")} target="_blank" rel="noreferrer">
              {t("report.exportPdf")}
            </a>
          </span>
        )}
      </div>

      {error && <div className="error">{error}</div>}
      {customerId == null && !error && <p className="muted">{t("report.pickCustomer")}</p>}

      {statement && (
        <>
          <div className="statement-summary">
            <div>
              <small>{t("report.opening")}</small>
              <b>{money(statement.openingBalance)}</b>
            </div>
            <div>
              <small>{t("report.totalDebt")}</small>
              <b className="amount-debt">{money(statement.totalDebt)}</b>
            </div>
            <div>
              <small>{t("report.totalPayment")}</small>
              <b className="amount-payment">{money(statement.totalPayment)}</b>
            </div>
            <div>
              <small>{t("report.closing")}</small>
              <b className="balance-debt">{money(statement.closingBalance)}</b>
            </div>
          </div>

          {statement.totalPages > 1 && (
            <Pager page={statement.page} totalPages={statement.totalPages} onChange={goToPage} />
          )}

          <div className="table-wrap">
            <table className="statement-table">
              <thead>
                <tr>
                  <th>{t("report.date")}</th>
                  <th>{t("report.type")}</th>
                  <th>{t("report.description")}</th>
                  <th className="num">{t("report.debt")}</th>
                  <th className="num">{t("report.payment")}</th>
                  <th className="num">{t("report.runningBalance")}</th>
                </tr>
              </thead>
              <tbody>
                {statement.lines.length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted">
                      {t("report.noLines")}
                    </td>
                  </tr>
                )}
                {statement.lines.map((line) => (
                  <tr key={line.transactionId}>
                    <td>{formatDate(line.date, locale)}</td>
                    <td className={line.type === "Debt" ? "amount-debt" : "amount-payment"}>
                      {line.type === "Debt" ? t("tx.debt") : t("tx.payment")}
                    </td>
                    <td>{line.description ?? ""}</td>
                    <td className="num amount-debt">{line.debt ? money(line.debt) : ""}</td>
                    <td className="num amount-payment">{line.payment ? money(line.payment) : ""}</td>
                    <td className="num">{money(line.runningBalance)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {statement.totalPages > 1 && (
            <Pager page={statement.page} totalPages={statement.totalPages} onChange={goToPage} />
          )}
        </>
      )}
    </section>
  );
}
