type EntityActionIconProps = {
  action: "edit" | "delete";
  label: string;
  onClick: () => void;
  disabled?: boolean;
};

export function EntityActionIcon({ action, label, onClick, disabled }: EntityActionIconProps) {
  const isDelete = action === "delete";

  return (
    <button
      type="button"
      className={`icon-button${isDelete ? " danger-text" : ""}`}
      onClick={onClick}
      disabled={disabled}
      aria-label={`${isDelete ? "Delete" : "Edit"} ${label}`}
      title={`${isDelete ? "Delete" : "Edit"} ${label}`}
    >
      {isDelete ? (
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h18" /><path d="M8 6V4h8v2" /><path d="m19 6-1 14H6L5 6" /><path d="M10 11v5M14 11v5" /></svg>
      ) : (
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 20h9" /><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z" /></svg>
      )}
    </button>
  );
}
