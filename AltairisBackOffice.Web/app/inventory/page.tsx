"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, Hotel, InventoryCalendarDay, localDate } from "@/lib/api";
import { Empty, Loading } from "@/components/ui";
import { EntityActionIcon } from "@/components/entity-action-icon";

export default function InventoryPage() {
  const { id } = useParams<{ id?: string }>();
  const scopedHotelId = id ?? "";
  const [hotels, setHotels] = useState<Hotel[]>([]);
  const [rooms, setRooms] = useState<Array<{ id: number; name: string }>>([]);
  const [hotelId, setHotelId] = useState(scopedHotelId);
  const [calendarMonth, setCalendarMonth] = useState(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });
  const [showAllotmentForm, setShowAllotmentForm] = useState(false);
  const [allotmentFrom, setAllotmentFrom] = useState(localDate());
  const [allotmentTo, setAllotmentTo] = useState(localDate(new Date(Date.now() + 13 * 86_400_000)));
  const [allotmentRoomIds, setAllotmentRoomIds] = useState<number[]>([]);
  const [allotmentCapacity, setAllotmentCapacity] = useState("10");
  const [allotmentFreeSale, setAllotmentFreeSale] = useState(false);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [editingDate, setEditingDate] = useState<string | null>(null);
  const [editingRoomId, setEditingRoomId] = useState<number | null>(null);
  const [capacity, setCapacity] = useState("0");
  const [freeSale, setFreeSale] = useState(false);
  const [calendarDays, setCalendarDays] = useState<InventoryCalendarDay[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (scopedHotelId) {
      setHotelId(scopedHotelId);
      api<Hotel>(`Hotel/${scopedHotelId}`)
        .then((hotel) => setHotels([hotel]))
        .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load hotel."));
      return;
    }
    api<{ items: Hotel[] }>("Hotel/search?page=1&pageSize=100")
      .then(({ items }) => {
        setHotels(items);
        if (items.length) setHotelId(String(items[0].id));
      })
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load hotels."));
  }, [scopedHotelId]);

  useEffect(() => {
    if (!hotelId) { setRooms([]); return; }
    api<Array<{ id: number; name: string }>>(`Rooms/hotel/${hotelId}`)
      .then(setRooms)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Could not load room types."));
  }, [hotelId]);

  const load = useCallback(async () => {
    if (!hotelId) { setCalendarDays([]); return; }
    const monthStart = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth(), 1);
    const monthEnd = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth() + 1, 0);
    setLoading(true);
    try {
      const params = new URLSearchParams({
        hotelId,
        from: localDate(monthStart),
        to: localDate(monthEnd),
      });
      setCalendarDays(await api<InventoryCalendarDay[]>(`Inventory/calendar?${params}`));
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load inventory.");
    } finally {
      setLoading(false);
    }
  }, [calendarMonth, hotelId]);
  useEffect(() => { void load(); }, [load]);

  async function saveDay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingRoomId || !editingDate) return;
    setSaving(true);
    setError("");
    setNotice("");
    try {
      await api(`Inventory/room/${editingRoomId}/range`, {
        method: "PUT",
        body: JSON.stringify({ from: editingDate, to: editingDate, capacity: Number(capacity), freeSale }),
      });
      setNotice(`Availability saved for ${editingRoom?.roomName} on ${new Intl.DateTimeFormat("en-GB", { dateStyle: "long" }).format(new Date(`${editingDate}T12:00:00`))}.`);
      setEditingDate(null);
      setEditingRoomId(null);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not save inventory.");
    } finally {
      setSaving(false);
    }
  }

  async function createAllotment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!allotmentRoomIds.length || !hotelId) return;
    setSaving(true);
    setError("");
    setNotice("");
    try {
      await api("Inventory/rooms/range", {
        method: "PUT",
        body: JSON.stringify({
          roomIds: allotmentRoomIds,
          from: allotmentFrom,
          to: allotmentTo,
          capacity: Number(allotmentCapacity),
          freeSale: allotmentFreeSale,
        }),
      });
      setShowAllotmentForm(false);
      setNotice(`Allotment created for ${allotmentRoomIds.length} room type(s) over the selected date range.`);
      setAllotmentRoomIds([]);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not create allotment.");
    } finally {
      setSaving(false);
    }
  }

  const year = calendarMonth.getFullYear();
  const month = calendarMonth.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const leadingDays = (new Date(year, month, 1).getDay() + 6) % 7;
  const monthLabel = new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric" }).format(calendarMonth);
  const editingDay = calendarDays.find((day) => day.date === editingDate);
  const selectedDay = calendarDays.find((day) => day.date === selectedDate);
  const editingRoom = selectedDay?.rooms.find((room) => room.roomId === editingRoomId);
  return <>
    <div className="page-heading">
      <div><p className="eyebrow">AVAILABILITY CONTROL</p><h1>Inventory</h1><p className="page-subtitle">Monitor room availability by date and keep room allotments up to date.</p></div>
    </div>
    {error && <div className="alert alert-error">{error}<button onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
    {notice && <div className="alert alert-success">{notice}<button onClick={() => setNotice("")} aria-label="Dismiss message">×</button></div>}
    <section className="panel">
      <div className="panel-heading inventory-calendar-heading">
        <div><h2>Inventory calendar</h2><p>View available and used rooms. Select a day to inspect or adjust its configured capacity.</p></div>
        <div className="inventory-calendar-actions">
          <div className="calendar-month-picker">
            <button className="button button-small" aria-label="Previous month" onClick={() => { setSelectedDate(null); setEditingDate(null); setCalendarMonth(new Date(year, month - 1, 1)); }}>←</button>
            <strong>{monthLabel}</strong>
            <button className="button button-small" aria-label="Next month" onClick={() => { setSelectedDate(null); setEditingDate(null); setCalendarMonth(new Date(year, month + 1, 1)); }}>→</button>
          </div>
          <button className="button button-primary button-small" disabled={!rooms.length} onClick={() => {
            setAllotmentFrom(localDate());
            setAllotmentTo(localDate(new Date(Date.now() + 13 * 86_400_000)));
            setAllotmentRoomIds([]);
            setAllotmentCapacity("10");
            setAllotmentFreeSale(false);
            setError("");
            setShowAllotmentForm(true);
          }}>＋ Create allotment</button>
        </div>
      </div>
      {!scopedHotelId && <div className="inventory-controls"><label className="field"><span>Hotel</span><select value={hotelId} onChange={(e) => { setSelectedDate(null); setEditingDate(null); setEditingRoomId(null); setHotelId(e.target.value); }}><option value="">Choose hotel</option>{hotels.map((hotel) => <option key={hotel.id} value={hotel.id}>{hotel.name}</option>)}</select></label></div>}
      <div className="inventory-legend"><span><i className="legend-available" /> Available</span><span><i className="legend-limited" /> Limited availability</span><span><i className="legend-unavailable" /> Sold out / not configured</span><span><b className="free-sale-icon" aria-hidden="true">∞</b> Free sale (unlimited)</span></div>
      {loading ? <Loading /> : !hotels.length ? <Empty title="No hotels available" detail="Create a hotel before configuring inventory." /> : !rooms.length ? <Empty title="No room types available" detail="Add room types to this hotel first." /> :
        <div className="calendar-scroll">
          <div className="inventory-calendar" role="grid" aria-label={`Inventory for ${monthLabel}`}>
            {["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"].map((weekday) =>
              <div className="calendar-weekday" role="columnheader" key={weekday}>{weekday}</div>,
            )}
            {Array.from({ length: leadingDays }, (_, index) =>
              <div className="calendar-blank" role="gridcell" aria-hidden="true" key={`blank-${index}`} />,
            )}
            {Array.from({ length: daysInMonth }, (_, index) => {
              const dayNumber = index + 1;
              const day = calendarDays[index];
              if (!day) return null;
              return <div
                className={`calendar-day ${day.status}${day.configured ? "" : " not-configured"}${selectedDate === day.date ? " selected" : ""}`}
                role="gridcell"
                aria-label={`${day.date}: ${day.configured ? `capacity ${day.capacity ?? "unlimited"}, ${day.available ?? "unlimited"} available, ${day.used} used` : "inventory not configured"}`}
                key={day.date}
              >
                <button
                  type="button"
                  className="calendar-day-trigger"
                  aria-expanded={selectedDate === day.date}
                  aria-label={`View inventory breakdown for ${day.date}`}
                  onClick={() => {
                    if (selectedDate === day.date) {
                      setSelectedDate(null);
                      setEditingDate(null);
                      setEditingRoomId(null);
                      return;
                    }
                    setSelectedDate(day.date);
                    setEditingDate(null);
                    setEditingRoomId(null);
                    setError("");
                    setNotice("");
                  }}
                >
                  <span className="calendar-day-top">
                    <span className="calendar-day-number">{dayNumber}</span>
                    <span className="calendar-day-indicators">
                      {day.includesFreeSale && <span className="free-sale-icon" title="Free sale: unlimited sales" aria-label="Free sale: unlimited sales">∞</span>}
                      {day.status === "unavailable" && <span className="calendar-day-state">{!day.configured ? "Not set" : day.incomplete ? "Incomplete" : "Sold out"}</span>}
                    </span>
                  </span>
                  {day.configured ? <span className="calendar-day-metrics">
                    <span><small>Available</small><strong>{day.available ?? "∞"}</strong></span>
                    <span><small>Used</small><strong>{day.used}</strong></span>
                  </span> : <span className="calendar-not-configured">No inventory</span>}
                </button>
              </div>;
            })}
          </div>
        </div>}
    </section>
    {selectedDay && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget && !saving) {
        setSelectedDate(null);
        setEditingDate(null);
        setEditingRoomId(null);
      }
    }}>
      <section className="modal inventory-day-modal" role="dialog" aria-modal="true" aria-labelledby="inventory-day-title">
        <div className="modal-heading inventory-day-modal-heading">
          <div><p className="eyebrow">DAILY INVENTORY</p><h2 id="inventory-day-title">{new Intl.DateTimeFormat("en-GB", { dateStyle: "long" }).format(new Date(`${selectedDay.date}T12:00:00`))}</h2><p className="page-subtitle">Availability by room type</p></div>
          <button type="button" className="icon-button" disabled={saving} aria-label="Close availability details" onClick={() => { setSelectedDate(null); setEditingDate(null); setEditingRoomId(null); }}>×</button>
        </div>
        {error && <div className="alert alert-error">{error}</div>}
        {!selectedDay.rooms.length ? <p className="calendar-editor-hint">No room types available for this hotel.</p> :
          <div className="inventory-room-breakdown">
            {selectedDay.rooms.map((room) => <article className={`inventory-room-row${editingRoomId === room.roomId ? " editing" : ""}`} key={room.roomId}>
              <strong className="inventory-room-name">{room.roomName}</strong>
              <span><small>Available</small><b>{room.freeSale ? "∞" : room.available ?? "—"}</b></span>
              <span><small>Used</small><b>{room.used}</b></span>
              <span><small>Configured capacity</small><b>{room.configured ? room.configuredCapacity : "Not set"}</b></span>
              {room.freeSale && <span className="free-sale-badge"><b className="free-sale-icon" title="Free sale: unlimited sales" aria-label="Free sale: unlimited sales">∞</b> Unlimited</span>}
              <EntityActionIcon action="edit" label={`capacity for ${room.roomName}`} disabled={saving} onClick={() => {
                setEditingDate(selectedDay.date);
                setEditingRoomId(room.roomId);
                setCapacity(String(room.configuredCapacity ?? 0));
                setFreeSale(room.freeSale);
                setError("");
              }} />
            </article>)}
          </div>}
        {editingRoom && editingDay && <form className="calendar-day-editor inventory-day-editor" onSubmit={saveDay}>
          <div className="calendar-day-editor-heading">
            <div><strong>{editingRoom.roomName}</strong><small>{editingRoom.used} room(s) currently used; capacity cannot be reduced below this.</small></div>
            <button type="button" className="icon-button" aria-label="Close capacity editor" onClick={() => { setEditingDate(null); setEditingRoomId(null); }}>×</button>
          </div>
          <label className="calendar-capacity-field">Configured capacity
            <span className="calendar-capacity-control">
              <button type="button" className="button button-small" aria-label="Remove one room from capacity" disabled={Number(capacity) <= editingRoom.used} onClick={() => setCapacity(String(Math.max(editingRoom.used, Number(capacity) - 1)))}>−</button>
              <input required type="number" min={editingRoom.used} max="100000" value={capacity} onChange={(event) => setCapacity(event.target.value)} />
              <button type="button" className="button button-small" aria-label="Add one room to capacity" onClick={() => setCapacity(String(Math.min(100000, Number(capacity) + 1)))}>+</button>
            </span>
          </label>
          <label className="checkbox-field"><input type="checkbox" checked={freeSale} onChange={(event) => setFreeSale(event.target.checked)} /> Free sale (unlimited)</label>
          <div className="calendar-day-editor-actions">
            <button type="button" className="button button-small" onClick={() => { setEditingDate(null); setEditingRoomId(null); }}>Close</button>
            <button type="submit" className="button button-small button-primary" disabled={saving || Number(capacity) < editingRoom.used}>{saving ? "Saving..." : "Save availability"}</button>
          </div>
        </form>}
      </section>
    </div>}
    {showAllotmentForm && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget && !saving) setShowAllotmentForm(false);
    }}>
      <section className="modal modal-compact" role="dialog" aria-modal="true" aria-labelledby="allotment-form-title">
        <div className="modal-heading">
          <div><p className="eyebrow">INVENTORY CONFIGURATION</p><h2 id="allotment-form-title">Create allotment</h2></div>
          <button type="button" className="icon-button" disabled={saving} onClick={() => setShowAllotmentForm(false)} aria-label="Close">×</button>
        </div>
        <p className="page-subtitle">Choose one or more room types and set their capacity for a date range. Existing reservations are protected.</p>
        <form className="form-grid allotment-form" onSubmit={createAllotment}>
          <label className="field"><span>From</span><input required type="date" value={allotmentFrom} onChange={(event) => setAllotmentFrom(event.target.value)} /></label>
          <label className="field"><span>To</span><input required type="date" min={allotmentFrom} value={allotmentTo} onChange={(event) => setAllotmentTo(event.target.value)} /></label>
          <label className="field wide"><span>Room capacity</span><input required type="number" min="0" max="100000" value={allotmentCapacity} onChange={(event) => setAllotmentCapacity(event.target.value)} /></label>
          <label className="checkbox-field wide"><input type="checkbox" checked={allotmentFreeSale} onChange={(event) => setAllotmentFreeSale(event.target.checked)} /> Free sale (unlimited capacity)</label>
          <fieldset className="allotment-room-fieldset wide">
            <legend>Room types</legend>
            <div className="allotment-room-select-actions">
              <span>{allotmentRoomIds.length} of {rooms.length} selected</span>
              <button type="button" className="button button-small" onClick={() => setAllotmentRoomIds(rooms.map((room) => room.id))}>Select all</button>
              <button type="button" className="button button-small" onClick={() => setAllotmentRoomIds([])}>Clear</button>
            </div>
            <div className="allotment-room-list">
              {rooms.map((room) => <label className="room-check" key={room.id}>
                <input type="checkbox" checked={allotmentRoomIds.includes(room.id)} onChange={(event) => setAllotmentRoomIds((current) =>
                  event.target.checked
                    ? [...current, room.id]
                    : current.filter((roomId) => roomId !== room.id),
                )} />
                <span><strong>{room.name}</strong></span>
              </label>)}
            </div>
          </fieldset>
          {error && <div className="alert alert-error wide">{error}</div>}
          <div className="form-actions">
            <button type="button" className="button" disabled={saving} onClick={() => setShowAllotmentForm(false)}>Cancel</button>
            <button type="submit" className="button button-primary" disabled={saving || !allotmentRoomIds.length || !allotmentFrom || !allotmentTo || allotmentFrom > allotmentTo}>{saving ? "Saving..." : "Create allotment"}</button>
          </div>
        </form>
      </section>
    </div>}
  </>;
}
