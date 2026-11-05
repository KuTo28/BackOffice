"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, Hotel, RoomType } from "@/lib/api";
import { Empty, Field, Loading } from "@/components/ui";
import { EntityActionIcon } from "@/components/entity-action-icon";

const blankRoom = { name: "", description: "", price: "", hotelId: "" };

export default function RoomTypesPage() {
  const { id } = useParams<{ id?: string }>();
  const scopedHotelId = id ?? "";
  const [hotels, setHotels] = useState<Hotel[]>([]);
  const [hotelId, setHotelId] = useState(scopedHotelId);
  const [rooms, setRooms] = useState<RoomType[]>([]);
  const [form, setForm] = useState(blankRoom);
  const [editing, setEditing] = useState<number | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

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

  const load = useCallback(async () => {
    if (!hotelId) {
      setRooms([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      setRooms(await api<RoomType[]>(`Rooms/hotel/${hotelId}`));
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load room types.");
    } finally {
      setLoading(false);
    }
  }, [hotelId]);
  useEffect(() => { void load(); }, [load]);

  function startEdit(room: RoomType) {
    setEditing(room.id);
    setForm({ name: room.name, description: room.description ?? "", price: String(room.price), hotelId: String(room.hotelId) });
    setShowForm(true);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      const body = { name: form.name, description: form.description, price: Number(form.price), hotelId: Number(form.hotelId) };
      await api(editing ? `Rooms/${editing}` : "Rooms", { method: editing ? "PUT" : "POST", body: JSON.stringify(body) });
      setShowForm(false);
      setEditing(null);
      setForm({ ...blankRoom, hotelId });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not save room type.");
    } finally {
      setSaving(false);
    }
  }

  async function remove(room: RoomType) {
    if (!window.confirm(`Delete ${room.name}?`)) return;
    try {
      await api(`Rooms/${room.id}`, { method: "DELETE" });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not delete room type.");
    }
  }

  return <>
    <div className="page-heading">
      <div><p className="eyebrow">PROPERTY CONFIGURATION</p><h1>Room types</h1><p className="page-subtitle">Maintain the room categories and base rates offered by each hotel.</p></div>
      <button className="button button-primary" disabled={!hotelId} onClick={() => { setEditing(null); setForm({ ...blankRoom, hotelId }); setShowForm(true); }}>＋ Add room type</button>
    </div>
    {error && <div className="alert alert-error">{error}<button onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
    <section className="panel">
      <div className="toolbar">
        {!scopedHotelId && <label className="field hotel-select"><span>Selected property</span><select value={hotelId} onChange={(event) => setHotelId(event.target.value)}><option value="">Select a hotel</option>{hotels.map((hotel) => <option value={hotel.id} key={hotel.id}>{hotel.name}</option>)}</select></label>}
        <span className="results-count">{rooms.length} room {rooms.length === 1 ? "type" : "types"}</span>
      </div>
      {loading ? <Loading /> : !hotels.length ? <Empty title="No hotels available" detail="Create a hotel before adding room types." /> : rooms.length === 0 ? <Empty title="No room types yet" detail="Add the first room category for this property." /> :
        <div className="room-grid">{rooms.map((room, index) => <article className="room-card" key={room.id}>
          <div className={`room-card-visual visual-${index % 4}`}><span className="room-visual-icon">▦</span><span className="room-number">ROOM TYPE {String(index + 1).padStart(2, "0")}</span></div>
          <div className="room-card-body">
            <div className="room-card-title"><div><h3>{room.name}</h3><span className="muted">{room.description || "No description"}</span></div><strong>{new Intl.NumberFormat("en-GB", { style: "currency", currency: "EUR" }).format(room.price)}<small> / night</small></strong></div>
            <div className="room-card-footer"><span className="muted">ID #{room.id}</span><div className="actions"><EntityActionIcon action="edit" label={room.name} onClick={() => startEdit(room)} /><EntityActionIcon action="delete" label={room.name} onClick={() => void remove(room)} /></div></div>
          </div>
        </article>)}</div>}
    </section>
    {showForm && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowForm(false); }}>
      <section className="modal modal-compact" role="dialog" aria-modal="true" aria-labelledby="room-form-title">
        <div className="modal-heading"><div><p className="eyebrow">ROOM CONFIGURATION</p><h2 id="room-form-title">{editing ? "Edit room type" : "Add a room type"}</h2></div><button className="icon-button" onClick={() => setShowForm(false)} aria-label="Close">×</button></div>
        <form onSubmit={submit} className="form-grid">
          {!scopedHotelId && <Field label="Hotel" required wide><select required value={form.hotelId} onChange={(e) => setForm({ ...form, hotelId: e.target.value })}><option value="">Choose hotel</option>{hotels.map((hotel) => <option key={hotel.id} value={hotel.id}>{hotel.name}</option>)}</select></Field>}
          <Field label="Room type name" required wide><input required maxLength={255} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. Deluxe King" /></Field>
          <Field label="Description" wide><textarea rows={3} maxLength={255} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
          <Field label="Price per night (€)" required wide><input required type="number" min="0" step="0.01" value={form.price} onChange={(e) => setForm({ ...form, price: e.target.value })} /></Field>
          <div className="form-actions"><button type="button" className="button" onClick={() => setShowForm(false)}>Cancel</button><button disabled={saving} className="button button-primary">{saving ? "Saving..." : editing ? "Save changes" : "Create room type"}</button></div>
        </form>
      </section>
    </div>}
  </>;
}
