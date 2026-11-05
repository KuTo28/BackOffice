"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { api, Booking, localDate, money, Overview } from "@/lib/api";
import { Empty, Loading } from "@/components/ui";
import { BookingTrendChart } from "@/components/booking-trend-chart";
import { BookingSortField, BookingSortHeader, SortDirection } from "@/components/booking-sort-header";
import { sortBookings } from "@/lib/booking-sort";

export function HotelOverview() {
  const { id } = useParams<{ id: string }>();
  const [overview, setOverview] = useState<Overview>();
  const [sortBy, setSortBy] = useState<BookingSortField>("bookingDate");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const [trendDate, setTrendDate] = useState(localDate());
  const [isOverviewLoading, setIsOverviewLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    setIsOverviewLoading(true);
    setError("");
    api<Overview>(
      `Overview/hotel/${id}?date=${localDate()}&trendDate=${trendDate}`,
      { signal: controller.signal },
    )
      .then(setOverview)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) {
          setError(reason instanceof Error ? reason.message : "Could not load hotel overview.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsOverviewLoading(false);
      });
    return () => controller.abort();
  }, [id, trendDate]);

  if (error) return <div className="alert alert-error">{error}</div>;
  if (!overview) return <Loading />;

  function changeSort(field: BookingSortField) {
    setSortDirection((current) => sortBy === field ? (current === "asc" ? "desc" : "asc") : "desc");
    setSortBy(field);
  }

  return <>
    <div className="page-heading">
      <div><p className="eyebrow">HOTEL OVERVIEW</p><h1>Today&apos;s operations</h1><p className="page-subtitle">Bookings and guest activity for this property.</p></div>
      <Link className="button button-primary" href={`/hotels/${id}/bookings`}>View bookings <span aria-hidden="true">→</span></Link>
    </div>
    <BookingTrendChart
      data={overview.bookingsPerDay}
      scope="this hotel"
      anchorDate={trendDate}
      isLoading={isOverviewLoading}
      onAnchorDateChange={setTrendDate}
    />
    <section className="stat-grid" aria-label="Hotel daily operations">
      <Stat label="Arrivals today" value={overview.arrivals} icon="↘" tone="blue" note="Scheduled check-ins" />
      <Stat label="Departures today" value={overview.departures} icon="↗" tone="violet" note="Scheduled check-outs" />
      <Stat label="Active bookings" value={overview.booked} icon="▣" tone="green" note="For this hotel" />
      <Stat label="Canceled bookings" value={overview.canceled} icon="×" tone="amber" note="All-time canceled" />
    </section>
    <section className="panel">
      <div className="panel-heading">
        <div><h2>Recent bookings</h2><p>Latest activity for this property</p></div>
        <Link className="text-link" href={`/hotels/${id}/bookings`}>View all <span aria-hidden="true">→</span></Link>
      </div>
      {!overview.recentBookings.length ? <Empty title="No bookings yet" detail="Bookings for this hotel will appear here." /> :
        <BookingTable
          bookings={overview.recentBookings}
          sortBy={sortBy}
          sortDirection={sortDirection}
          onSort={changeSort}
        />}
    </section>
  </>;
}

function Stat({ label, value, icon, tone, note }: { label: string; value: number; icon: string; tone: string; note: string }) {
  return <article className="stat-card">
    <div className={`stat-icon ${tone}`}>{icon}</div>
    <div className="stat-label">{label}</div>
    <div className="stat-value">{value}</div>
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
  const sortedBookings = sortBookings(bookings, sortBy, sortDirection);
  return <div className="table-scroll"><table>
    <thead><tr><BookingSortHeader field="id" label="BOOKING" sortBy={sortBy} direction={sortDirection} onSort={onSort} /><BookingSortHeader field="bookingDate" label="BOOKING DATE" sortBy={sortBy} direction={sortDirection} onSort={onSort} /><th>GUEST</th><th>STAY</th><th>ROOMS</th><th>STATUS</th><th>TOTAL</th></tr></thead>
    <tbody>{sortedBookings.map((booking) => <tr key={booking.id}>
      <td><span className="booking-id">#{booking.id}</span></td>
      <td>{dateLabel(booking.bookingDate)}</td>
      <td><strong>{booking.guestName || "—"}</strong><small className="cell-sub">{booking.guestEmail}</small></td>
      <td>{dateLabel(booking.checkIn)} <span className="muted">→</span> {dateLabel(booking.checkOut)}</td>
      <td>{booking.rooms?.length ?? 0}</td>
      <td><span className={`status-pill ${booking.status}`}>{booking.status}</span></td>
      <td className="amount">{money(booking.totalPrice)}</td>
    </tr>)}</tbody>
  </table></div>;
}

function dateLabel(value: string | null) {
  return value
    ? new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short" }).format(new Date(value))
    : "—";
}
