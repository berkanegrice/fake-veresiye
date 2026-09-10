import { useState } from "react";
import { Modal } from "../../components/Modal";
import { Stat } from "../../components/Stat";
import { api } from "../../api/client";
import type { ImportPreview, ImportResult } from "../../api/types";
import { useT } from "../../i18n";
import { money } from "../../format";

interface ImportWizardProps {
  onClose: () => void;
  onImported: () => void;
}

export function ImportWizard({ onClose, onImported }: ImportWizardProps) {
  const t = useT();
  const [step, setStep] = useState<0 | 1 | 2 | 3>(0);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const pickFile = async (file: File) => {
    setBusy(true);
    setError("");
    try {
      setPreview(await api.previewExa(file));
      setStep(1);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const validate = async () => {
    if (!preview) return;
    setBusy(true);
    setError("");
    try {
      const validation = await api.validateExa(preview.token);
      setPreview({ ...preview, validation });
      setStep(2);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const runImport = async () => {
    if (!preview?.validation.isValid) return;
    setBusy(true);
    setError("");
    try {
      setResult(await api.importExa(preview.token));
      setStep(3);
      onImported();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const reset = () => {
    setStep(0);
    setPreview(null);
    setResult(null);
    setError("");
  };

  return (
    <Modal title={t("import.title")} onClose={onClose}>
      <div className="wizard">
        <ol className="steps">
          <li className={step >= 0 ? "active" : ""}>{t("import.step.preview")}</li>
          <li className={step >= 2 ? "active" : ""}>{t("import.step.validate")}</li>
          <li className={step >= 3 ? "active" : ""}>{t("import.step.import")}</li>
        </ol>

        {error && <div className="error">{error}</div>}

        {step === 0 && (
          <div className="wizard-body">
            <p>{t("import.previewNote")}</p>
            <label className="drop">
              <input
                type="file"
                accept=".exa"
                onChange={(e) => e.target.files?.[0] && pickFile(e.target.files[0])}
              />
              {busy ? t("import.reading") : t("import.choose")}
            </label>
          </div>
        )}

        {step === 1 && preview && (
          <div className="wizard-body">
            <p className="muted">{preview.fileName}</p>
            <div className="stats">
              <Stat label={t("import.customers")} value={preview.customers.toLocaleString()} />
              <Stat
                label={t("import.transactions")}
                value={preview.transactions.toLocaleString()}
              />
              <Stat label={t("import.totalDebt")} value={money(preview.totalDebt)} />
              <Stat label={t("import.totalPayments")} value={money(preview.totalPayments)} />
              <Stat label={t("import.outstanding")} value={money(preview.outstanding)} />
            </div>
            <h4>{t("import.firstCustomers")}</h4>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>{t("customer.name")}</th>
                    <th>{t("customer.phone")}</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.customerPreview.map((c) => (
                    <tr key={c.id}>
                      <td>{c.id}</td>
                      <td>{c.name}</td>
                      <td>{c.phone ?? ""}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="wizard-actions">
              <button type="button" className="primary" disabled={busy} onClick={validate}>
                {busy ? t("import.validating") : t("import.toValidation")}
              </button>
            </div>
          </div>
        )}

        {step === 2 && preview && (
          <div className="wizard-body">
            <div className={preview.validation.isValid ? "banner ok" : "banner bad"}>
              {preview.validation.isValid ? t("import.ready") : t("import.blocked")}
            </div>
            {preview.validation.errors.length > 0 && (
              <>
                <h4>{t("import.errors")}</h4>
                <ul className="issues">
                  {preview.validation.errors.map((x, i) => (
                    <li key={i}>{x}</li>
                  ))}
                </ul>
              </>
            )}
            {preview.validation.warnings.length > 0 && (
              <>
                <h4>{t("import.warnings")}</h4>
                <ul className="issues warn">
                  {preview.validation.warnings.map((x, i) => (
                    <li key={i}>{x}</li>
                  ))}
                </ul>
              </>
            )}
            <div className="wizard-actions">
              <button type="button" onClick={() => setStep(1)}>
                ← {t("common.back")}
              </button>
              <button
                type="button"
                className="primary"
                disabled={!preview.validation.isValid || busy}
                onClick={runImport}
              >
                {busy ? t("import.running") : t("import.run")}
              </button>
            </div>
          </div>
        )}

        {step === 3 && result && (
          <div className="wizard-body">
            <div className="banner ok">{t("import.done")}</div>
            <p>
              {t("import.processedCustomers")}: <b>{result.customers}</b>
            </p>
            <p>
              {t("import.importedTransactions")}: <b>{result.importedTransactions}</b>
            </p>
            <div className="wizard-actions">
              <button type="button" onClick={reset}>
                {t("import.another")}
              </button>
              <button type="button" className="primary" onClick={onClose}>
                {t("common.close")}
              </button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
