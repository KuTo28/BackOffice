"use client";

export type BookingSortField = "id" | "bookingDate";
export type SortDirection = "asc" | "desc";

type BookingSortHeaderProps = {
  field: BookingSortField;
  label: string;
  sortBy: BookingSortField;
  direction: SortDirection;
  onSort: (field: BookingSortField) => void;
};

export function BookingSortHeader({ field, label, sortBy, direction, onSort }: BookingSortHeaderProps) {
  const active = sortBy === field;
  const nextDirection = active ? (direction === "asc" ? "ascending" : "descending") : "not sorted";

  return <th aria-sort={active ? (direction === "asc" ? "ascending" : "descending") : "none"}>
    <button
      type="button"
      className="table-sort-button"
      onClick={() => onSort(field)}
      aria-label={`Sort by ${label}, ${nextDirection}`}
    >
      {label}<span aria-hidden="true">{active ? (direction === "asc" ? "↑" : "↓") : "↕"}</span>
    </button>
  </th>;
}
