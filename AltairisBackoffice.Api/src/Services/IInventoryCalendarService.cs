using AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Outcome of an inventory range update.</summary>
public enum InventoryUpdateStatus
{
    Updated,
    RoomNotFound,
    CapacityConflict,
    ConcurrentUpdate
}

/// <summary>Inventory update outcome with details when existing bookings prevent the change.</summary>
public sealed record InventoryUpdateResult(
    InventoryUpdateStatus Status,
    DateOnly? ConflictDate = null,
    int Booked = 0);

/// <summary>Reads inventory calendars and applies validated room inventory updates.</summary>
public interface IInventoryCalendarService
{
    /// <summary>Gets room-level capacity and booked counts for a hotel date range.</summary>
    Task<IReadOnlyList<InventoryDayResponse>> GetInventoryAsync(
        long hotelId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>Updates the inventory of one room type over an inclusive date range.</summary>
    Task<InventoryUpdateResult> UpdateInventoryAsync(
        long roomId,
        InventoryUpdateCommand command,
        CancellationToken cancellationToken);

    /// <summary>Updates several room types atomically over an inclusive date range.</summary>
    Task<InventoryUpdateResult> UpdateInventoryAsync(
        IReadOnlyCollection<long> roomIds,
        InventoryUpdateCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets daily hotel inventory aggregates, optionally for a single room type.</summary>
    Task<IReadOnlyList<InventoryCalendarDayResponse>?> GetCalendarAsync(
        long hotelId,
        long? roomId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
