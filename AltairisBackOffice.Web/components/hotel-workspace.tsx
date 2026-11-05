"use client";

import Link from "next/link";
import { useParams, usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { api, Hotel } from "@/lib/api";
import { Loading } from "@/components/ui";
import { HotelActiveSwitch } from "@/components/hotel-active-switch";

const tabs = [
  { segment: "", label: "Overview", icon: <svg viewBox="0 0 24 24"><path d="M4 19V5M4 19h17" /><path d="M7 15l4-4 3 2 6-7" /></svg> },
  { segment: "rooms", label: "Room types", icon: <svg viewBox="0 0 24 24"><path d="M3 18V8a2 2 0 0 1 2-2h1a3 3 0 0 1 3 3v3" /><path d="M3 12h16a2 2 0 0 1 2 2v4M3 18v2m18-2v2M9 12V9h8a2 2 0 0 1 2 2v1" /></svg> },
  { segment: "inventory", label: "Inventory", icon: <svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="3.5" /><path d="M5 20v-1a7 7 0 0 1 14 0v1z" /></svg> },
  { segment: "bookings", label: "Bookings", icon: <svg viewBox="0 0 24 24"><path d="M5 4.5A2.5 2.5 0 0 1 7.5 2H20v18H7.5A2.5 2.5 0 0 1 5 17.5z" /><path d="M5 4v14m4-11h7m-7 4h7" /></svg> },
];

export function HotelWorkspace({ children }: Readonly<{ children: ReactNode }>) {
  const { id } = useParams<{ id: string }>();
  const pathname = usePathname();
  const [hotel, setHotel] = useState<Hotel>();
  const [error, setError] = useState("");
  const [updateError, setUpdateError] = useState("");

  async function updateActive(active: boolean) {
    try {
      await api(`Hotel/${id}`, { method: "PUT", body: JSON.stringify({ active }) });
      setHotel((current) => current ? { ...current, active } : current);
      setUpdateError("");
    } catch (reason) {
      setUpdateError(reason instanceof Error ? reason.message : "Could not update hotel status.");
    }
  }

  useEffect(() => {
    api<Hotel>(`Hotel/${id}`)
      .then(setHotel)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load hotel."));
  }, [id]);

  if (error) return <div className="alert alert-error">{error}</div>;
  if (!hotel) return <Loading />;

  return <>
    <div className="hotel-workspace-sticky">
      <div className="hotel-workspace-heading">
        <div>
          <Link className="back-link" href="/hotels">← All hotels</Link>
          <p className="eyebrow">HOTEL WORKSPACE</p>
          <div className="hotel-title-row">
            <h1>{hotel.name}</h1>
            <HotelActiveSwitch active={hotel.active ?? false} hotelName={hotel.name} onChange={(active) => void updateActive(active)} />
          </div>
          <p className="hotel-description">{hotel.description || "No description provided."}</p>
          <p className="hotel-location">{[hotel.city, hotel.country].filter(Boolean).join(", ") || hotel.address || "Property management"}</p>
        </div>
      </div>
      <nav className="hotel-tabs" aria-label={`${hotel.name} sections`}>
        {tabs.map((tab) => {
          const href = tab.segment ? `/hotels/${id}/${tab.segment}` : `/hotels/${id}`;
          const active = tab.segment
            ? pathname === href || pathname.startsWith(`${href}/`)
            : pathname === href;
          return <Link className={`hotel-tab${active ? " active" : ""}`} href={href} key={tab.segment}>
            <span aria-hidden="true">{tab.icon}</span>{tab.label}
          </Link>;
        })}
      </nav>
    </div>
    {updateError && <div className="alert alert-error">{updateError}</div>}
    <div className="hotel-workspace-content">{children}</div>
  </>;
}
