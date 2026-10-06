import type { Locale } from "./i18n";

const MONEY = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  maximumFractionDigits: 2,
});

export function money(value: number): string {
  return MONEY.format(value);
}

export function formatDate(iso: string, locale: Locale): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleDateString(locale === "tr" ? "tr-TR" : "en-GB");
}

/** yyyy-MM-dd for <input type="date"> and API query params. */
export function toDateInput(iso: string): string {
  return iso.slice(0, 10);
}

const AMOUNT_PATTERN = /^(\d{1,3}(\.\d{3})+|\d+)(,\d{1,2})?$/;
const AMOUNT_FORMAT = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 });

/** Parses a Turkish-formatted amount ("2.000,50"); null when the format is not valid. */
export function parseAmountInput(text: string): number | null {
  const trimmed = text.trim();
  if (!AMOUNT_PATTERN.test(trimmed)) return null;
  return Number(trimmed.replace(/\./g, "").replace(",", "."));
}

/** Turkish display form of an amount: 2000.5 -> "2.000,5". */
export function toAmountInput(value: number): string {
  return AMOUNT_FORMAT.format(value);
}

export function todayInput(): string {
  // Local date, not toISOString() (UTC), which is yesterday for the first hours after midnight.
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
