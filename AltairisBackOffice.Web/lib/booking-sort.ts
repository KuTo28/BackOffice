import type { Booking } from "@/lib/api";
import type { BookingSortField, SortDirection } from "@/components/booking-sort-header";

export function sortBookings(
  bookings: Booking[],
  field: BookingSortField,
  direction: SortDirection,
): Booking[] {
  return [...bookings].sort((first, second) => {
    const comparison = field === "id"
      ? first.id - second.id
      : (first.bookingDate ?? "").localeCompare(second.bookingDate ?? "");
    return direction === "asc" ? comparison : -comparison;
  });
}
