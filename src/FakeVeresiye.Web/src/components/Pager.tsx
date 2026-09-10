import type { ReactNode } from "react";
import { useT } from "../i18n";

interface PagerProps {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
  children?: ReactNode;
}

/** Prev / "page X / Y" / Next control. `children` renders on the left (e.g. a page-size select). */
export function Pager({ page, totalPages, onChange, children }: PagerProps) {
  const t = useT();
  return (
    <div className="pager">
      {children}
      <button type="button" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        ◀ {t("common.previous")}
      </button>
      <span className="pager-info">
        {t("common.page")} {page} / {Math.max(totalPages, 1)}
      </span>
      <button type="button" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        {t("common.next")} ▶
      </button>
    </div>
  );
}
