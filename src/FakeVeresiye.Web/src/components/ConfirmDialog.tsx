import { useState } from "react";
import { Modal } from "./Modal";
import { useT } from "../i18n";

interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmLabel?: string;
  danger?: boolean;
  onConfirm: () => Promise<void> | void;
  onClose: () => void;
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  danger,
  onConfirm,
  onClose,
}: ConfirmDialogProps) {
  const t = useT();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const run = async () => {
    setBusy(true);
    setError("");
    try {
      await onConfirm();
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
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
          <button
            type="button"
            className={danger ? "danger" : "primary"}
            onClick={run}
            disabled={busy}
          >
            {confirmLabel ?? t("common.delete")}
          </button>
        </>
      }
    >
      <p>{message}</p>
      {error && <div className="error">{error}</div>}
    </Modal>
  );
}
