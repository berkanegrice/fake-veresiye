import { useState, type FormEvent } from "react";
import { Modal } from "../../components/Modal";
import { api } from "../../api/client";
import { TransactionTypeValue, type TransactionResponse } from "../../api/types";
import { useT } from "../../i18n";
import { toDateInput, todayInput } from "../../format";

type Mode =
  | { kind: "debt"; customerId: number }
  | { kind: "payment"; customerId: number }
  | { kind: "edit"; transaction: TransactionResponse };

interface TransactionFormDialogProps {
  mode: Mode;
  onClose: () => void;
  onSaved: () => void;
}

export function TransactionFormDialog({ mode, onClose, onSaved }: TransactionFormDialogProps) {
  const t = useT();

  const editing = mode.kind === "edit";
  const [amount, setAmount] = useState(editing ? String(mode.transaction.amount) : "");
  const [date, setDate] = useState(
    editing ? toDateInput(mode.transaction.transactionDate) : todayInput(),
  );
  const [description, setDescription] = useState(
    editing ? (mode.transaction.description ?? "") : "",
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const title =
    mode.kind === "debt"
      ? t("tx.addDebtTitle")
      : mode.kind === "payment"
        ? t("tx.addPaymentTitle")
        : t("tx.editTitle");

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const value = Number(amount.replace(",", "."));
    if (!Number.isFinite(value) || value <= 0) {
      setError(t("common.amountPositive"));
      return;
    }
    setBusy(true);
    setError("");
    try {
      const isoDate = new Date(date + "T00:00:00").toISOString();
      if (mode.kind === "edit") {
        await api.updateTransaction(mode.transaction.id, {
          amount: value,
          transactionDate: isoDate,
          description: description.trim(),
        });
      } else {
        await api.addTransaction(mode.customerId, {
          type:
            mode.kind === "debt" ? TransactionTypeValue.Debt : TransactionTypeValue.Payment,
          amount: value,
          transactionDate: isoDate,
          description: description.trim(),
        });
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setBusy(false);
    }
  };

  return (
    <Modal
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </button>
          <button type="submit" form="tx-form" className="primary" disabled={busy}>
            {t("common.save")}
          </button>
        </>
      }
    >
      <form id="tx-form" className="form" onSubmit={submit}>
        <label>
          <span>{t("tx.amount")}</span>
          <input
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            inputMode="decimal"
            autoFocus
            required
          />
        </label>
        <label>
          <span>{t("tx.date")}</span>
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
        </label>
        <label>
          <span>{t("tx.description")}</span>
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </label>
        {editing && <p className="hint">{t("tx.editHint")}</p>}
        {error && <div className="error">{error}</div>}
      </form>
    </Modal>
  );
}
