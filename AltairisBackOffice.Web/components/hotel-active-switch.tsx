"use client";

type HotelActiveSwitchProps = {
  active: boolean;
  hotelName: string;
  onChange: (active: boolean) => void;
};

export function HotelActiveSwitch({ active, hotelName, onChange }: HotelActiveSwitchProps) {
  return (
    <button
      type="button"
      className={`hotel-active-switch${active ? " is-active" : ""}`}
      role="switch"
      aria-checked={active}
      aria-label={`${active ? "Deactivate" : "Activate"} ${hotelName}`}
      title={active ? "Active" : "Inactive"}
      onClick={() => onChange(!active)}
    >
      <span />
    </button>
  );
}
