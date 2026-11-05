"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api, Hotel, Page } from "@/lib/api";
import { Empty, Field, Loading, Pager } from "@/components/ui";
import { HotelActiveSwitch } from "@/components/hotel-active-switch";
import { EntityActionIcon } from "@/components/entity-action-icon";

const emptyHotel = {
  name: "", email: "", phone: "", active: true, address: "", city: "", country: "", description: "",
};

export default function HotelsPage() {
  const [hotels, setHotels] = useState<Hotel[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [form, setForm] = useState<typeof emptyHotel>(emptyHotel);
  const [editing, setEditing] = useState<number | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api<Page<Hotel>>(`Hotel/search?query=${encodeURIComponent(search)}&page=${page}&pageSize=10`);
      setHotels(result.items);
      setTotal(result.totalCount);
      if (result.page !== page) setPage(result.page);
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load hotels.");
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => { void load(); }, [load]);

  function beginEdit(hotel: Hotel) {
    setEditing(hotel.id);
    setForm({
      name: hotel.name, email: hotel.email, phone: hotel.phone ?? "", active: hotel.active ?? false,
      address: hotel.address ?? "", city: hotel.city ?? "", country: hotel.country ?? "", description: hotel.description ?? "",
    });
    setShowForm(true);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      await api(editing ? `Hotel/${editing}` : "Hotel", {
        method: editing ? "PUT" : "POST",
        body: JSON.stringify(form),
      });
      setShowForm(false);
      setEditing(null);
      setForm(emptyHotel);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not save hotel.");
    } finally {
      setSaving(false);
    }
  }

  async function toggleActive(hotel: Hotel, active: boolean) {
    try {
      await api(`Hotel/${hotel.id}`, { method: "PUT", body: JSON.stringify({ active }) });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not update hotel.");
    }
  }

  async function remove(hotel: Hotel) {
    if (!window.confirm(`Delete ${hotel.name}?`)) return;
    try {
      await api(`Hotel/${hotel.id}`, { method: "DELETE" });
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not delete hotel.");
    }
  }

  return <>
    <div className="page-heading">
      <div><p className="eyebrow">PROPERTY MANAGEMENT</p><h1>Hotels</h1><p className="page-subtitle">Manage your properties, contact details, and listing status.</p></div>
      <button className="button button-primary" onClick={() => { setEditing(null); setForm(emptyHotel); setShowForm(true); }}>＋ Add hotel</button>
    </div>
    {error && <div className="alert alert-error">{error}<button onClick={() => setError("")} aria-label="Dismiss error">×</button></div>}
    <section className="panel">
      <div className="toolbar">
        <label className="search-box"><span aria-hidden="true">⌕</span><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Search hotels, cities, countries..." /></label>
        <span className="results-count">{total} {total === 1 ? "property" : "properties"}</span>
      </div>
      {loading ? <Loading /> : hotels.length === 0 ? <Empty title="No hotels found" detail={search ? "Try another search." : "Add a hotel to start managing properties."} /> :
        <div className="table-scroll"><table>
          <thead><tr><th>PROPERTY</th><th>LOCATION</th><th>CONTACT</th><th>STATUS</th><th className="right">ACTIONS</th></tr></thead>
          <tbody>{hotels.map((hotel) => <tr key={hotel.id}>
            <td><Link className="hotel-cell hotel-row-link" href={`/hotels/${hotel.id}`}><span className="hotel-avatar">{hotel.name.slice(0, 1).toUpperCase()}</span><span><strong>{hotel.name}</strong><small className="cell-sub">{hotel.address || "Address not provided"}</small></span></Link></td>
            <td>{[hotel.city, hotel.country].filter(Boolean).join(", ") || "—"}</td>
            <td><span>{hotel.email}</span><small className="cell-sub">{hotel.phone || "No phone"}</small></td>
            <td><HotelActiveSwitch active={hotel.active ?? false} hotelName={hotel.name} onChange={(active) => void toggleActive(hotel, active)} /></td>
            <td className="right actions">
              <EntityActionIcon action="edit" label={hotel.name} onClick={() => beginEdit(hotel)} />
              <EntityActionIcon action="delete" label={hotel.name} onClick={() => void remove(hotel)} />
            </td>
          </tr>)}</tbody>
        </table></div>}
      <Pager page={page} total={total} itemLabel="hotels" onChange={setPage} />
    </section>
    {showForm && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowForm(false); }}>
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="hotel-form-title">
        <div className="modal-heading"><div><p className="eyebrow">{editing ? "PROPERTY DETAILS" : "NEW PROPERTY"}</p><h2 id="hotel-form-title">{editing ? "Edit hotel" : "Add a hotel"}</h2></div><button className="icon-button" onClick={() => setShowForm(false)} aria-label="Close">×</button></div>
        <form onSubmit={submit} className="form-grid">
          <Field label="Hotel name" required><input required maxLength={255} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Email" required><input required type="email" maxLength={255} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
          <Field label="Phone"><input maxLength={255} value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></Field>
          <Field label="Address"><input maxLength={255} value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} /></Field>
          <Field label="City"><input maxLength={255} value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} /></Field>
          <Field label="Country"><input maxLength={255} value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} /></Field>
          <Field label="Description" wide><textarea maxLength={255} rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
          <label className="checkbox-field"><input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} /> Hotel is active</label>
          <div className="form-actions"><button type="button" className="button" onClick={() => setShowForm(false)}>Cancel</button><button disabled={saving} className="button button-primary">{saving ? "Saving..." : editing ? "Save changes" : "Create hotel"}</button></div>
        </form>
      </section>
    </div>}
  </>;
}
