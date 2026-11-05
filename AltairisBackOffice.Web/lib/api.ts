export interface Hotel {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  active: boolean | null;
  address: string | null;
  city: string | null;
  country: string | null;
  description: string | null;
}

export interface RoomType {
  id: number;
  name: string;
  description: string | null;
  price: number;
  hotelId: number;
}

export interface InventoryCalendarDay {
  date: string;
  capacity: number | null;
  configuredCapacity: number | null;
  available: number | null;
  used: number;
  configured: boolean;
  incomplete: boolean;
  includesFreeSale: boolean;
  status: "available" | "limited" | "unavailable";
  rooms: InventoryRoomCalendarDay[];
}

export interface InventoryRoomCalendarDay {
  roomId: number;
  roomName: string;
  configuredCapacity: number | null;
  available: number | null;
  used: number;
  configured: boolean;
  freeSale: boolean;
}

export interface Booking {
  id: number;
  hotelId: number;
  hotelName: string;
  bookingDate: string | null;
  checkIn: string | null;
  checkOut: string | null;
  status: "booked" | "canceled";
  totalPrice: number | null;
  guestName: string | null;
  guestPhone: string | null;
  guestEmail: string | null;
  rooms: Array<{ roomId: number | null; name: string | null; price: number }>;
}

export interface BookingTrace {
  id: number;
  bookingId: number | null;
  parentId: string | null;
  operation: "quote" | "confirm" | "cancel";
  direction: "request" | "response";
  statusCode: number | null;
  payload: unknown;
  createdAt: string;
}

export interface Page<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Overview {
  arrivals: number;
  departures: number;
  booked: number;
  canceled: number;
  bookingsPerDay: Array<{ date: string; count: number }>;
  recentBookings: Booking[];
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/backend/${path}`, {
    ...init,
    headers: {
      ...(init?.body ? { "content-type": "application/json" } : {}),
      ...init?.headers,
    },
    cache: "no-store",
  });

  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    const raw = await response.text();
    try {
      const body: unknown = raw ? JSON.parse(raw) : null;
      if (typeof body === "string") message = body;
      else if (body && typeof body === "object") {
        const problem = body as { detail?: string; title?: string; message?: string };
        message = problem.detail ?? problem.title ?? problem.message ?? message;
      }
    } catch {
      if (raw) message = raw;
    }
    throw new Error(message);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function localDate(date = new Date()): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
}

export function money(value: number | null | undefined): string {
  return new Intl.NumberFormat("en-GB", { style: "currency", currency: "EUR" }).format(value ?? 0);
}

export function dateLabel(value: string | null | undefined): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric" })
    .format(new Date(value));
}
