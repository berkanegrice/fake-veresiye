import { useI18n } from "./index";

export function LangToggle() {
  const { locale, setLocale, t } = useI18n();
  return (
    <div className="lang-toggle" role="group" aria-label="Language">
      <button
        type="button"
        className={locale === "tr" ? "active" : ""}
        onClick={() => setLocale("tr")}
      >
        {t("lang.tr")}
      </button>
      <button
        type="button"
        className={locale === "en" ? "active" : ""}
        onClick={() => setLocale("en")}
      >
        {t("lang.en")}
      </button>
    </div>
  );
}
