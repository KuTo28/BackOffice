"use client";

import type { ReactNode } from "react";

export function Field({ label, required, wide, children }: { label: string; required?: boolean; wide?: boolean; children: ReactNode }) {
  return <label className={`field${wide ? " wide" : ""}`}><span>{label}{required && <b className="required-mark"> *</b>}</span>{children}</label>;
}

export function Pager({ page, total, itemLabel, onChange }: { page: number; total: number; itemLabel: string; onChange: (page: number) => void }) {
  const max = Math.max(1, Math.ceil(total / 10));
  return <div className="pager"><span>Page {page} of {max} · {total} {itemLabel} total</span><div><button className="button button-small" disabled={page <= 1} onClick={() => onChange(page - 1)}>Previous</button><button className="button button-small" disabled={page >= max} onClick={() => onChange(page + 1)}>Next</button></div></div>;
}

export function Loading() { return <div className="loading-state"><span className="spinner" /> Loading data...</div>; }

export function Empty({ title, detail }: { title: string; detail: string }) {
  return <div className="empty-state"><span className="empty-icon">⌕</span><strong>{title}</strong><span>{detail}</span></div>;
}
