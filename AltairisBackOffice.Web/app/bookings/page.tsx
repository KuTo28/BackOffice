"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";
import { useParams } from "next/navigation";
import { api, Booking, BookingTrace, dateLabel, Hotel, money, Page, RoomType } from "@/lib/api";
import { Empty, Field, Loading, Pager } from "@/components/ui";
import { BookingSortField, BookingSortHeader, SortDirection } from "@/components/booking-sort-header";

const emptyForm = { hotelId: "", guestName: "", guestEmail: "", guestPhone: "", checkIn: "", checkOut: "" };

export default function BookingsPage() {
  const { id } = useParams<{ id?: string }>();
  const scopedHotelId = id ?? "";
  const [hotels, setHotels] = useState<Hotel[]>([]);
  const [rooms, setRooms] = useState<RoomType[]>([]);
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [hotelFilter, setHotelFilter] = useState(scopedHotelId);
  const [dateFilter, setDateFilter] = useState("");
  const [bookingDateFilter, setBookingDateFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [sortBy, setSortBy] = useState<BookingSortField>("bookingDate");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [form, setForm] = useState(emptyForm);
  const [selectedRooms, setSelectedRooms] = useState<number[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [detail, setDetail] = useState<Booking | null>(null);
  const [traces, setTraces] = useState<BookingTrace[]>([]);
  const [loadingTraces, setLoadingTraces] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (scopedHotelId) {
      setHotelFilter(scopedHotelId);
      api<Hotel>(`Hotel/${scopedHotelId}`)
        .then((hotel) => setHotels([hotel]))
        .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load hotel."));
      return;
    }
    api<{ items: Hotel[] }>("Hotel/search?page=1&pageSize=100")
      .then(({ items }) => setHotels(items))
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load hotels."));
  }, [scopedHotelId]);

  const load = useCallback(async (requestedPage = page) => {
    setLoading(true);
    const params = new URLSearchParams({ page: String(requestedPage), pageSize: "10" });
    if (scopedHotelId || hotelFilter) params.set("hotelId", scopedHotelId || hotelFilter);
    if (dateFilter) params.set("date", dateFilter);
    if (bookingDateFilter) params.set("bookingDate", bookingDateFilter);
    if (statusFilter) params.set("status", statusFilter);
    params.set("sortBy", sortBy);
    params.set("sortDirection", sortDirection);
    try {
      const result = await api<Page<Booking>>(`Booking?${params}`);
      setBookings(result.items);
      setTotal(result.totalCount);
      if (result.page !== requestedPage) setPage(result.page);
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load bookings.");
    } finally {
      setLoading(false);
    }
  }, [bookingDateFilter, dateFilter, hotelFilter, page, scopedHotelId, sortBy, sortDirection, statusFilter]);
  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!form.hotelId) { setRooms([]); setSelectedRooms([]); return; }
    api<RoomType[]>(`Rooms/hotel/${form.hotelId}`)
      .then((items) => { setRooms(items); setSelectedRooms([]); })
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load room types."));
  }, [form.hotelId]);

  function openCreate() {
    setForm({ ...emptyForm, hotelId: scopedHotelId || hotelFilter || (hotels[0] ? String(hotels[0].id) : "") });
    setSelectedRooms([]);
    setShowForm(true);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedRooms.length) {
      setError("Select at least one room type.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      await api("Booking", {
        method: "POST",
        body: JSON.stringify({
          hotelId: Number(form.hotelId),
          checkIn: `${form.checkIn}T00:00:00`,
          checkOut: `${form.checkOut}T00:00:00`,
          guestName: form.guestName,
          guestEmail: form.guestEmail,
          guestPhone: form.guestPhone,
          bookedRoomIds: selectedRooms,
        }),
      });
      setShowForm(false);
      setPage(1);
      await load(1);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not create booking.");
    } finally {
      setSaving(false);
    }
  }

  async function cancel(booking: Booking) {
    if (!window.confirm(`Cancel booking #${booking.id}? This will release the room inventory for its stay.`)) return;
    try {
      await api(`Booking/${booking.id}/cancel`, { method: "POST" });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not cancel booking.");
    }
  }

  async function viewDetails(booking: Booking) {
    setDetail(booking);
    setTraces([]);
    setLoadingTraces(true);
    try {
      const [bookingDetails, bookingTraces] = await Promise.all([
        api<Booking>(`Booking/${booking.id}`),
        api<BookingTrace[]>(`Booking/${booking.id}/traces`),
      ]);
      setDetail(bookingDetails);
      setTraces(bookingTraces);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load booking details.");
    } finally {
      setLoadingTraces(false);
    }
  }

  const maxPage = Math.max(1, Math.ceil(total / 10));
  const selectedHotel = hotels.find((hotel) => String(hotel.id) === form.hotelId);
  function changeSort(field: BookingSortField) {
    setSortDirection((current) => sortBy === field ? (current === "asc" ? "desc" : "asc") : "desc");
    setSortBy(field);
    setPage(1);
  }

  return <>
    <div className="page-heading">
      <div><p className="eyebrow">GUEST OPERATIONS</p><h1>Bookings</h1><p className="page-subtitle">Track stays, check daily arrivals, and manage booking status.</p></div>
      <button className="button button-primary" onClick={openCreate} disabled={!hotels.length}>＋ Create booking</button>
    </div>
    {error && <div className="alert alert-error">{error}<button onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
    <section className="panel">
      <div className="booking-filters">
        {!scopedHotelId && <label className="field"><span>Hotel</span><select value={hotelFilter} onChange={(e) => { setHotelFilter(e.target.value); setPage(1); }}><option value="">All hotels</option>{hotels.map((hotel) => <option key={hotel.id} value={hotel.id}>{hotel.name}</option>)}</select></label>}
        <label className="field"><span>Check-in date</span><input type="date" value={dateFilter} onChange={(e) => { setDateFilter(e.target.value); setPage(1); }} /></label>
        <label className="field"><span>Booking date</span><input type="date" value={bookingDateFilter} onChange={(e) => { setBookingDateFilter(e.target.value); setPage(1); }} /></label>
        <label className="field"><span>Status</span><select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}><option value="">All statuses</option><option value="booked">Booked</option><option value="canceled">Canceled</option></select></label>
        <div className="booking-count"><strong>{total}</strong><span>bookings</span></div>
      </div>
      {loading ? <Loading /> : !hotels.length ? <Empty title="No hotels available" detail="Create a hotel before taking bookings." /> : bookings.length === 0 ? <Empty title="No bookings found" detail="Change the filters or create a booking." /> :
        <div className="table-scroll"><table>
          <thead><tr><BookingSortHeader field="id" label="BOOKING" sortBy={sortBy} direction={sortDirection} onSort={changeSort} /><BookingSortHeader field="bookingDate" label="BOOKING DATE" sortBy={sortBy} direction={sortDirection} onSort={changeSort} /><th>GUEST</th><th>PROPERTY</th><th>STAY DATES</th><th>ROOMS</th><th>STATUS</th><th>TOTAL</th><th className="booking-actions-cell">ACTIONS</th></tr></thead>
          <tbody>{bookings.map((booking) => <tr key={booking.id}>
            <td><button className="booking-id booking-id-button" onClick={() => void viewDetails(booking)} aria-label={`View booking ${booking.id} details`}>#{booking.id}</button></td>
            <td>{dateLabel(booking.bookingDate)}</td>
            <td><strong>{booking.guestName || "—"}</strong><small className="cell-sub">{booking.guestEmail}</small></td>
            <td>{booking.hotelName}</td>
            <td><strong>{dateLabel(booking.checkIn)}</strong><small className="cell-sub">to {dateLabel(booking.checkOut)}</small></td>
            <td>{booking.rooms?.map((room) => room.name).filter(Boolean).join(", ") || booking.rooms?.length || "—"}</td>
            <td><span className={`status-pill ${booking.status}`}>{booking.status}</span></td>
            <td className="amount">{money(booking.totalPrice)}</td>
            <td className="booking-actions-cell"><div className="actions booking-actions"><button className="button button-small" onClick={() => void viewDetails(booking)}>Details</button>{booking.status === "booked" && <button className="button button-small button-cancel" onClick={() => void cancel(booking)}>Cancel</button>}</div></td>
          </tr>)}</tbody>
        </table></div>}
      <Pager page={page} total={total} itemLabel="bookings" onChange={(next) => setPage(Math.min(maxPage, next))} />
    </section>
    {showForm && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowForm(false); }}>
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="booking-form-title">
        <div className="modal-heading"><div><p className="eyebrow">NEW RESERVATION</p><h2 id="booking-form-title">Create booking</h2></div><button className="icon-button" onClick={() => setShowForm(false)} aria-label="Close">×</button></div>
        <form onSubmit={submit} className="form-grid">
          {!scopedHotelId && <Field label="Hotel" required wide><select required value={form.hotelId} onChange={(e) => setForm({ ...form, hotelId: e.target.value })}><option value="">Choose hotel</option>{hotels.map((hotel) => <option key={hotel.id} value={hotel.id}>{hotel.name}</option>)}</select></Field>}
          <Field label="Guest name" required><input required maxLength={255} value={form.guestName} onChange={(e) => setForm({ ...form, guestName: e.target.value })} /></Field>
          <Field label="Email" required><input type="email" required maxLength={255} value={form.guestEmail} onChange={(e) => setForm({ ...form, guestEmail: e.target.value })} /></Field>
          <Field label="Phone" required><input required maxLength={255} value={form.guestPhone} onChange={(e) => setForm({ ...form, guestPhone: e.target.value })} /></Field>
          <Field label="Check-in" required><input type="date" required value={form.checkIn} onChange={(e) => setForm({ ...form, checkIn: e.target.value })} /></Field>
          <Field label="Check-out" required><input type="date" required min={form.checkIn || undefined} value={form.checkOut} onChange={(e) => setForm({ ...form, checkOut: e.target.value })} /></Field>
          <div className="field wide"><span>Room types <b className="required-mark">*</b></span>
            {rooms.length ? <div className="room-check-list">{rooms.map((room) => <label className="room-check" key={room.id}>
              <input type="checkbox" checked={selectedRooms.includes(room.id)} onChange={(event) => setSelectedRooms(event.target.checked ? [...selectedRooms, room.id] : selectedRooms.filter((id) => id !== room.id))} />
              <span><strong>{room.name}</strong><small>{room.description || "No description"}</small></span><b>{money(room.price)} <small>/ night</small></b>
            </label>)}</div> : <div className="inline-notice">{form.hotelId ? "No room types for this property." : "Choose a hotel to see its room types."}</div>}
          </div>
          <div className="booking-form-note wide"><span className="status-pill booked">Booked</span><span>Bookings are immutable after creation. You can cancel a booking to release its inventory.</span></div>
          <div className="form-actions"><button type="button" className="button" onClick={() => setShowForm(false)}>Cancel</button><button disabled={saving || !selectedHotel || !rooms.length} className="button button-primary">{saving ? "Creating..." : "Confirm booking"}</button></div>
        </form>
      </section>
    </div>}
    {detail && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setDetail(null); }}>
      <section className="modal booking-detail-modal" role="dialog" aria-modal="true" aria-labelledby="booking-detail-title">
        <div className="modal-heading"><div><p className="eyebrow">BOOKING DETAILS · #{detail.id}</p><h2 id="booking-detail-title">{detail.guestName || "Guest booking"}</h2></div><button className="icon-button" onClick={() => setDetail(null)} aria-label="Close">×</button></div>
        <div className="detail-grid">
          <Detail label="Status"><span className={`status-pill ${detail.status}`}>{detail.status}</span></Detail>
          <Detail label="Property">{detail.hotelName}</Detail>
          <Detail label="Check-in">{dateLabel(detail.checkIn)}</Detail>
          <Detail label="Check-out">{dateLabel(detail.checkOut)}</Detail>
          <Detail label="Email">{detail.guestEmail}</Detail>
          <Detail label="Phone">{detail.guestPhone}</Detail>
          <Detail label="Total">{money(detail.totalPrice)}</Detail>
          <Detail label="Room types">{detail.rooms?.map((room) => room.name).filter(Boolean).join(", ") || "—"}</Detail>
        </div>
        <section className="booking-traces" aria-labelledby="booking-traces-title">
          <div className="booking-traces-heading">
            <div><p className="eyebrow">REQUEST / RESPONSE HISTORY</p><h3 id="booking-traces-title">Operation traces</h3></div>
            <span>{traces.length} entries</span>
          </div>
          {loadingTraces ? <div className="booking-traces-empty">Loading traces...</div> : traces.length === 0
            ? <div className="booking-traces-empty">No traces are associated with this booking.</div>
            : <div className="booking-traces-list">{traces.map((trace) => {
              const isRequest = trace.direction === "request";
              return <article className={`booking-trace ${isRequest ? "request" : "response"}`} key={trace.id}>
                {!isRequest && <span className="booking-trace-arrow" aria-label="Response">←</span>}
                <div className="booking-trace-content">
                  <div className="booking-trace-meta">
                    <strong>{trace.operation} · {trace.direction}</strong>
                    <span>{trace.statusCode ? `HTTP ${trace.statusCode} · ` : ""}{dateLabel(trace.createdAt)} {new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit" }).format(new Date(trace.createdAt))}</span>
                  </div>
                  <pre>{JSON.stringify(trace.payload, null, 2)}</pre>
                </div>
                {isRequest && <span className="booking-trace-arrow" aria-label="Request">→</span>}
              </article>;
            })}</div>}
        </section>
        <div className="form-actions"><button className="button" onClick={() => setDetail(null)}>Close</button>{detail.status === "booked" && <button className="button button-cancel" onClick={() => { setDetail(null); void cancel(detail); }}>Cancel booking</button>}</div>
      </section>
    </div>}
  </>;
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return <div className="detail-item"><span>{label}</span><strong>{children}</strong></div>;
}
