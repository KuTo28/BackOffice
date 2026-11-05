"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { api, Booking, dateLabel, Hotel, localDate, money, Overview, Page } from "@/lib/api";
import { HotelActiveSwitch } from "@/components/hotel-active-switch";
import { BookingTrendChart } from "@/components/booking-trend-chart";
import { BookingSortField, BookingSortHeader, SortDirection } from "@/components/booking-sort-header";
import { sortBookings } from "@/lib/booking-sort";

export default function OverviewPage() {
  const [overview, setOverview] = useState<Overview>();
  const [hotels, setHotels] = useState<Hotel[]>([]);
  const [sortBy, setSortBy] = useState<BookingSortField>("bookingDate");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const [trendDate, setTrendDate] = useState(localDate());
  const [isOverviewLoading, setIsOverviewLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    setIsOverviewLoading(true);
    setError("");
    api<Overview>(`Overview?date=${localDate()}&trendDate=${trendDate}`, { signal: controller.signal })
      .then(setOverview)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) {
          setError(reason instanceof Error ? reason.message : "Could not load overview.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsOverviewLoading(false);
      });
    return () => controller.abort();
  }, [trendDate]);

  useEffect(() => {
    const controller = new AbortController();
    api<Page<Hotel>>("Hotel/search?page=1&pageSize=100", { signal: controller.signal })
      .then(({ items }) => setHotels(items))
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) {
          setError(reason instanceof Error ? reason.message : "Could not load hotels.");
        }
      });
    return () => controller.abort();
  }, []);

  async function setHotelActive(hotel: Hotel, active: boolean) {
    try {
      await api(`Hotel/${hotel.id}`, { method: "PUT", body: JSON.stringify({ active }) });
      setHotels((current) => current.map((item) => item.id === hotel.id ? { ...item, active } : item));
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not update hotel status.");
    }
  }

  function changeSort(field: BookingSortField) {
    setSortDirection((current) => sortBy === field ? (current === "asc" ? "desc" : "asc") : "desc");
    setSortBy(field);
  }

  return (
    <>
      <div className="page-heading">
        <div><p className="eyebrow">PROPERTY OVERVIEW</p><h1>Good day</h1><p className="page-subtitle">Here&apos;s what&apos;s happening across your properties today.</p></div>
        <Link className="button button-primary" href="/hotels">＋ Choose hotel</Link>
      </div>
      {error && <div className="alert alert-error">{error}</div>}
      {overview && (
        <BookingTrendChart
          data={overview.bookingsPerDay}
          anchorDate={trendDate}
          isLoading={isOverviewLoading}
          onAnchorDateChange={setTrendDate}
        />
      )}
      <section className="stat-grid" aria-label="Daily operations">
        <Stat label="Arrivals today" value={overview?.arrivals} icon="↘" tone="blue" note="Scheduled check-ins" />
        <Stat label="Departures today" value={overview?.departures} icon="↗" tone="violet" note="Scheduled check-outs" />
        <Stat label="Active bookings" value={overview?.booked} icon="▣" tone="green" note="Across all properties" />
        <Stat label="Canceled bookings" value={overview?.canceled} icon="×" tone="amber" note="All-time canceled" />
      </section>
      <section className="panel">
        <div className="panel-heading">
          <div><h2>Recent bookings</h2><p>Latest activity across your hotels</p></div>
          <Link className="text-link" href="/hotels">Open a hotel <span aria-hidden="true">→</span></Link>
        </div>
        <BookingTable
          bookings={overview?.recentBookings ?? []}
          sortBy={sortBy}
          sortDirection={sortDirection}
          onSort={changeSort}
        />
      </section>
      <section className="panel hotel-overview">
        <div className="panel-heading">
          <div><h2>All hotels</h2><p>Choose a property to manage its rooms, inventory, and bookings.</p></div>
          <Link className="text-link" href="/hotels">Manage hotels <span aria-hidden="true">→</span></Link>
        </div>
        {!hotels.length ? <div className="empty-state"><strong>No hotels available</strong><span>Add a hotel to get started.</span></div> :
          <div className="hotel-overview-grid">{hotels.map((hotel) =>
            <div className="hotel-overview-card" key={hotel.id}>
              <Link href={`/hotels/${hotel.id}`} className="hotel-overview-link">
                <span className="hotel-avatar">{hotel.name.slice(0, 1).toUpperCase()}</span>
                <span className="hotel-overview-info"><strong>{hotel.name}</strong><small>{[hotel.city, hotel.country].filter(Boolean).join(", ") || "Location not provided"}</small></span>
                <b aria-hidden="true">→</b>
              </Link>
              <HotelActiveSwitch active={hotel.active ?? false} hotelName={hotel.name} onChange={(active) => void setHotelActive(hotel, active)} />
            </div>,
          )}</div>}
      </section>
    </>
  );
}

function Stat({ label, value, icon, tone, note }: { label: string; value?: number; icon: string; tone: string; note: string }) {
  return <article className="stat-card">
    <div className={`stat-icon ${tone}`}>{icon}</div>
    <div className="stat-label">{label}</div>
    <div className="stat-value">{value ?? "—"}</div>
    <div className="stat-note">{note}</div>
  </article>;
}

function BookingTable({
  bookings,
  sortBy,
  sortDirection,
  onSort,
}: {
  bookings: Booking[];
  sortBy: BookingSortField;
  sortDirection: SortDirection;
  onSort: (field: BookingSortField) => void;
}) {
  if (!bookings.length) return <div className="empty-state"><span className="empty-icon">▣</span><strong>No bookings yet</strong><span>Bookings will appear here as they are created.</span></div>;
  const sortedBookings = sortBookings(bookings, sortBy, sortDirection);
  return <div className="table-scroll"><table>
    <thead><tr><BookingSortHeader field="id" label="BOOKING" sortBy={sortBy} direction={sortDirection} onSort={onSort} /><BookingSortHeader field="bookingDate" label="BOOKING DATE" sortBy={sortBy} direction={sortDirection} onSort={onSort} /><th>GUEST</th><th>HOTEL</th><th>STAY</th><th>ROOMS</th><th>STATUS</th><th>TOTAL</th></tr></thead>
    <tbody>{sortedBookings.map((booking) => <tr key={booking.id}>
      <td><span className="booking-id">#{booking.id}</span></td>
      <td>{dateLabel(booking.bookingDate)}</td>
      <td><strong>{booking.guestName || "—"}</strong><small className="cell-sub">{booking.guestEmail}</small></td>
      <td>{booking.hotelName}</td>
      <td>{shortDate(booking.checkIn)} <span className="muted">→</span> {shortDate(booking.checkOut)}</td>
      <td>{booking.rooms?.length ?? 0}</td>
      <td><Status status={booking.status} /></td>
      <td className="amount">{money(booking.totalPrice)}</td>
    </tr>)}</tbody>
  </table></div>;
}

function shortDate(value: string | null) {
  return value ? new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short" }).format(new Date(value)) : "—";
}

function Status({ status }: { status: "booked" | "canceled" }) {
  return <span className={`status-pill ${status}`}>{status}</span>;
}
