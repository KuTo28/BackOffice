using System.Text.Json;
using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Persists trace payloads and links checkout operations to their resulting booking.</summary>
public sealed class BookingTraceService(AltairisDbContext dbContext) : IBookingTraceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AltairisDbContext _dbContext = dbContext;

    /// <summary>Serializes and stores one request or response trace.</summary>
    public async Task RecordAsync(
        long? bookingId,
        Guid? parentId,
        string operation,
        string direction,
        int? statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var trace = new BookingTrace
        {
            BookingId = bookingId,
            ParentId = parentId,
            Operation = operation,
            Direction = direction,
            StatusCode = statusCode,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.BookingTraces.Add(trace);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Links all quote/confirm traces for a parent process to the created booking.</summary>
    public async Task LinkProcessToBookingAsync(
        Guid parentId,
        long bookingId,
        CancellationToken cancellationToken)
    {
        await _dbContext.BookingTraces
            .Where(trace => trace.ParentId == parentId && trace.BookingId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(trace => trace.BookingId, bookingId),
                cancellationToken);
    }

    /// <summary>Returns the booking already linked to a checkout process, if any.</summary>
    public Task<long?> GetLinkedBookingIdAsync(
        Guid parentId,
        CancellationToken cancellationToken) =>
        _dbContext.BookingTraces
            .AsNoTracking()
            .Where(trace => trace.ParentId == parentId && trace.BookingId != null)
            .Select(trace => trace.BookingId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Returns ordered traces for a booking, or null when the booking does not exist.</summary>
    public async Task<IReadOnlyList<BookingTraceResponse>?> GetByBookingIdAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Bookings.AnyAsync(booking => booking.Id == bookingId, cancellationToken))
        {
            return null;
        }

        var traces = await _dbContext.BookingTraces
            .AsNoTracking()
            .Where(trace => trace.BookingId == bookingId)
            .OrderBy(trace => trace.CreatedAt)
            .ThenBy(trace => trace.Id)
            .ToListAsync(cancellationToken);

        return traces.Select(trace => new BookingTraceResponse(
            trace.Id,
            trace.BookingId,
            trace.ParentId,
            trace.Operation,
            trace.Direction,
            trace.StatusCode,
            JsonDocument.Parse(trace.Payload).RootElement.Clone(),
            trace.CreatedAt)).ToList();
    }
}
