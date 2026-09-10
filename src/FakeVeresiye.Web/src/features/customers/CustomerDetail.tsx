import { useState } from "react";
import type { CustomerDetail as CustomerDetailModel } from "../../api/types";
import { api } from "../../api/client";
import { useT } from "../../i18n";
import { money } from "../../format";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { TransactionFormDialog } from "../transactions/TransactionFormDialog";
import { Ledger } from "./Ledger";

interface CustomerDetailProps {
  customer: CustomerDetailModel;
  reloadSignal: number;
  onChanged: () => void;
  onDeleted: () => void;
  onOpenStatement: (customerId: number) => void;
}

type TxDialog = { kind: "debt" } | { kind: "payment" } | null;

export function CustomerDetailView({
  customer,
  reloadSignal,
  onChanged,
  onDeleted,
  onOpenStatement,
}: CustomerDetailProps) {
  const t = useT();
  const [txDialog, setTxDialog] = useState<TxDialog>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <section className="detail">
      <div className="detail-head">
        <div>
          <h2>{customer.name}</h2>
          {customer.phone && <p className="muted">{customer.phone}</p>}
        </div>
        <strong className={customer.balance >= 0 ? "balance balance-debt" : "balance"}>
          {money(customer.balance)}
        </strong>
      </div>

      <div className="detail-actions">
        <button type="button" onClick={() => setTxDialog({ kind: "debt" })}>
          + {t("ledger.addDebt")}
        </button>
        <button type="button" onClick={() => setTxDialog({ kind: "payment" })}>
          − {t("ledger.addPayment")}
        </button>
        <button type="button" onClick={() => onOpenStatement(customer.id)}>
          {t("ledger.openStatement")}
        </button>
        <button type="button" className="danger ghost" onClick={() => setConfirmDelete(true)}>
          {t("common.delete")}
        </button>
      </div>

      <Ledger customerId={customer.id} reloadSignal={reloadSignal} onChanged={onChanged} />

      {txDialog && (
        <TransactionFormDialog
          mode={{ kind: txDialog.kind, customerId: customer.id }}
          onClose={() => setTxDialog(null)}
          onSaved={onChanged}
        />
      )}

      {confirmDelete && (
        <ConfirmDialog
          title={t("common.delete")}
          message={t("customer.deleteConfirm", { name: customer.name })}
          danger
          onConfirm={async () => {
            await api.deleteCustomer(customer.id);
            onDeleted();
          }}
          onClose={() => setConfirmDelete(false)}
        />
      )}
    </section>
  );
}
