import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { createPortal } from "react-dom";
import { Modal } from "../../components/Modal";
import { api } from "../../api/client";
import { TransactionTypeValue, type TransactionResponse } from "../../api/types";
import { useT } from "../../i18n";
import { toDateInput, todayInput } from "../../format";
import { useDebouncedValue } from "../../hooks/useDebouncedValue";

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
  const [descriptionOpen, setDescriptionOpen] = useState(false);
  const [descriptionSuggestions, setDescriptionSuggestions] = useState<string[]>([]);
  const debouncedDescription = useDebouncedValue(description.trim(), 250);

  const descriptionInputRef = useRef<HTMLInputElement>(null);
  const [descriptionRect, setDescriptionRect] = useState<{
    top: number;
    left: number;
    width: number;
  } | null>(null);

  // The dropdown is portaled to <body> and fixed-positioned from this rect, so it isn't
  // clipped by the modal body's `overflow-y: auto` when the field sits near the bottom.
  const updateDescriptionRect = useCallback(() => {
    const el = descriptionInputRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    setDescriptionRect({ top: r.bottom + 2, left: r.left, width: r.width });
  }, []);

  useEffect(() => {
    if (!descriptionOpen) return;
    let cancelled = false;
    api.listDescriptions(debouncedDescription).then((results) => {
      if (!cancelled) {
        setDescriptionSuggestions(
          results.filter((d) => d.toLowerCase() !== debouncedDescription.toLowerCase()),
        );
      }
    });
    return () => {
      cancelled = true;
    };
  }, [descriptionOpen, debouncedDescription]);

  useEffect(() => {
    if (!descriptionOpen) return;
    updateDescriptionRect();
    window.addEventListener("scroll", updateDescriptionRect, true);
    window.addEventListener("resize", updateDescriptionRect);
    return () => {
      window.removeEventListener("scroll", updateDescriptionRect, true);
      window.removeEventListener("resize", updateDescriptionRect);
    };
  }, [descriptionOpen, updateDescriptionRect]);

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
      // Plain local date (no "Z"): converting to UTC shifts it a day back east of Greenwich.
      const isoDate = date + "T00:00:00";
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
        <label className="combobox">
          <span>{t("tx.description")}</span>
          <input
            ref={descriptionInputRef}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            onFocus={() => setDescriptionOpen(true)}
            onBlur={() => window.setTimeout(() => setDescriptionOpen(false), 150)}
          />
        </label>
        {descriptionOpen &&
          descriptionSuggestions.length > 0 &&
          descriptionRect &&
          createPortal(
            <div
              className="combobox-list floating"
              style={{
                top: descriptionRect.top,
                left: descriptionRect.left,
                width: descriptionRect.width,
              }}
            >
              {descriptionSuggestions.map((suggestion) => (
                <button
                  type="button"
                  key={suggestion}
                  className="customer-item"
                  onMouseDown={() => {
                    setDescription(suggestion);
                    setDescriptionOpen(false);
                  }}
                >
                  <span>{suggestion}</span>
                </button>
              ))}
            </div>,
            document.body,
          )}
        {editing && <p className="hint">{t("tx.editHint")}</p>}
        {error && <div className="error">{error}</div>}
      </form>
    </Modal>
  );
}
