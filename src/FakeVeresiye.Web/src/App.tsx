import { useCallback, useEffect, useState } from "react";
import type { CustomerDetail } from "./api/types";
import { api } from "./api/client";
import { useT } from "./i18n";
import { LangToggle } from "./i18n/LangToggle";
import { CustomerList } from "./features/customers/CustomerList";
import { CustomerDetailView } from "./features/customers/CustomerDetail";
import { ImportWizard } from "./features/import/ImportWizard";
import { StatementPage } from "./features/reports/StatementPage";

type View = "ledger" | "reports";

export default function App() {
  const t = useT();
  const [view, setView] = useState<View>("ledger");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<CustomerDetail | null>(null);
  const [showImport, setShowImport] = useState(false);
  const [reportCustomerId, setReportCustomerId] = useState<number | null>(null);
  // Bumped whenever customer data changes, so the sidebar and ledger refetch.
  const [reloadSignal, setReloadSignal] = useState(0);
  const bumpReload = useCallback(() => setReloadSignal((n) => n + 1), []);

  const loadDetail = useCallback(async (id: number) => {
    setDetail(await api.getCustomer(id));
  }, []);

  useEffect(() => {
    if (selectedId === null) {
      setDetail(null);
      return;
    }
    void loadDetail(selectedId);
  }, [selectedId, loadDetail, reloadSignal]);

  const onChanged = useCallback(() => {
    bumpReload();
    if (selectedId !== null) void loadDetail(selectedId);
  }, [bumpReload, loadDetail, selectedId]);

  const openStatement = (customerId: number) => {
    setReportCustomerId(customerId);
    setView("reports");
  };

  return (
    <div className="app">
      <header className="app-bar">
        <div className="brand">
          <b>{t("app.title")}</b>
          <span>{t("app.subtitle")}</span>
        </div>
        <nav className="tabs">
          <button
            type="button"
            className={view === "ledger" ? "active" : ""}
            onClick={() => setView("ledger")}
          >
            {t("nav.ledger")}
          </button>
          <button
            type="button"
            className={view === "reports" ? "active" : ""}
            onClick={() => setView("reports")}
          >
            {t("nav.reports")}
          </button>
        </nav>
        <div className="app-bar-right">
          <button type="button" onClick={() => setShowImport(true)}>
            {t("nav.import")}
          </button>
          <LangToggle />
        </div>
      </header>

      {view === "ledger" ? (
        <main className="layout">
          <CustomerList
            selectedId={selectedId}
            reloadSignal={reloadSignal}
            onSelect={setSelectedId}
            onCreated={(id) => {
              bumpReload();
              setSelectedId(id);
            }}
          />
          {detail ? (
            <CustomerDetailView
              customer={detail}
              reloadSignal={reloadSignal}
              onChanged={onChanged}
              onDeleted={() => {
                setSelectedId(null);
                bumpReload();
              }}
              onOpenStatement={openStatement}
            />
          ) : (
            <section className="detail empty">
              <p className="muted">{t("customer.empty")}</p>
            </section>
          )}
        </main>
      ) : (
        <main className="layout single">
          <StatementPage initialCustomerId={reportCustomerId} />
        </main>
      )}

      {showImport && (
        <ImportWizard
          onClose={() => setShowImport(false)}
          onImported={onChanged}
        />
      )}
    </div>
  );
}
