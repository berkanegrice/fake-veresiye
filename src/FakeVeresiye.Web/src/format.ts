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

export function todayInput(): string {
  return new Date().toISOString().slice(0, 10);
}
