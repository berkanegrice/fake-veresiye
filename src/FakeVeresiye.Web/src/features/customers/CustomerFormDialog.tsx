import { useState, type FormEvent } from "react";
import { Modal } from "../../components/Modal";
import { api } from "../../api/client";
import { useT } from "../../i18n";

interface CustomerFormDialogProps {
  onClose: () => void;
  onCreated: (id: number) => void;
}

export function CustomerFormDialog({ onClose, onCreated }: CustomerFormDialogProps) {
  const t = useT();
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError(t("common.required"));
      return;
    }
    setBusy(true);
    setError("");
    try {
      const created = await api.createCustomer({
        name: name.trim(),
        phone: phone.trim(),
        notes: notes.trim() || undefined,
      });
      onCreated(created.id);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setBusy(false);
    }
  };

  return (
    <Modal
      title={t("customer.new")}
      onClose={onClose}
      footer={
        <>
          <button type="button" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </button>
          <button type="submit" form="customer-form" className="primary" disabled={busy}>
            {t("common.save")}
          </button>
        </>
      }
    >
      <form id="customer-form" className="form" onSubmit={submit}>
        <label>
          <span>{t("customer.name")}</span>
          <input value={name} onChange={(e) => setName(e.target.value)} autoFocus required />
        </label>
        <label>
          <span>{t("customer.phone")}</span>
          <input value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" />
        </label>
        <label>
          <span>{t("customer.notes")}</span>
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} />
        </label>
        {error && <div className="error">{error}</div>}
      </form>
    </Modal>
  );
}
