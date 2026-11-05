using System.Data;
using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;
using AltairisBackoffice.Api.src.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides booking search, availability, checkout, cancellation, and trace operations.</summary>
[ApiController]
[Route("api/[controller]")]
public class BookingController(
    AltairisDbContext dbContext,
    IOverviewService overviewService,
    IBookingCheckoutService checkoutService,
    IHotelInventoryCache inventoryCache,
    IBookingTraceService traceService) : ControllerBase
{
    private readonly AltairisDbContext _dbContext = dbContext;
    private readonly IOverviewService _overviewService = overviewService;
    private readonly IBookingCheckoutService _checkoutService = checkoutService;
    private readonly IHotelInventoryCache _inventoryCache = inventoryCache;
    private readonly IBookingTraceService _traceService = traceService;

    /// <summary>Gets cached room availability for a hotel stay.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <param name="checkIn">The first night of the stay.</param>
    /// <param name="checkOut">The checkout date, which is not included as a night.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>Availability by room type for every night in the requested stay.</returns>
    /// <response code="200">Availability was returned.</response>
    /// <response code="400">The hotel identifier or stay dates are invalid.</response>
    /// <response code="404">The hotel does not exist.</response>
    /// <response code="409">The hotel is inactive.</response>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] long hotelId,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var result = await _checkoutService.GetAvailabilityAsync(
            hotelId, checkIn, checkOut, cancellationToken);
        return result.Status switch
        {
            BookingCheckoutStatus.Success => Ok(result.Response),
            BookingCheckoutStatus.HotelNotFound => NotFound(),
            BookingCheckoutStatus.HotelInactive => Conflict("The selected hotel is inactive."),
            _ => BadRequest("The stay dates or hotel identifier are invalid.")
        };
    }

    /// <summary>Validates the selected rooms against current database inventory and creates a short-lived quote.</summary>
    /// <remarks>The quote is held in memory for 30 seconds; it is not persisted as a booking until confirmation.</remarks>
    /// <param name="request">The hotel, stay dates, and distinct room types to quote.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The quote identifier, expiry time, room prices, and total price.</returns>
    /// <response code="201">A quote was created.</response>
    /// <response code="400">The stay or room selection is invalid.</response>
    /// <response code="404">The hotel does not exist.</response>
    /// <response code="409">The hotel is inactive or one or more room types are unavailable.</response>
    [HttpPost("quote")]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateBookingQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var quoteId = Guid.NewGuid();
        await _traceService.RecordAsync(
            null, quoteId, "quote", "request", null, request, cancellationToken);

        var result = await _checkoutService.CreateQuoteAsync(request, quoteId, cancellationToken);
        var message = result.Status switch
        {
            BookingCheckoutStatus.Success => null,
            BookingCheckoutStatus.HotelNotFound => "The selected hotel does not exist.",
            BookingCheckoutStatus.HotelInactive => "The selected hotel is inactive.",
            BookingCheckoutStatus.Unavailable => "One or more selected room types are unavailable.",
            BookingCheckoutStatus.InvalidRoomSelection => "Every selected room type must belong to the selected hotel.",
            _ => "Select distinct room types for a stay between 1 and 366 nights."
        };
        var statusCode = result.Status switch
        {
            BookingCheckoutStatus.Success => StatusCodes.Status201Created,
            BookingCheckoutStatus.HotelNotFound => StatusCodes.Status404NotFound,
            BookingCheckoutStatus.HotelInactive or BookingCheckoutStatus.Unavailable =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        IActionResult actionResult = result.Status switch
        {
            BookingCheckoutStatus.Success => CreatedAtAction(
                nameof(GetQuote), new { quoteId }, result.Response),
            BookingCheckoutStatus.HotelNotFound => NotFound(message),
            BookingCheckoutStatus.HotelInactive or BookingCheckoutStatus.Unavailable => Conflict(message),
            _ => BadRequest(message)
        };
        await _traceService.RecordAsync(
            null,
            quoteId,
            "quote",
            "response",
            statusCode,
            result.Response ?? (object)new { message },
            cancellationToken);
        return actionResult;
    }

    /// <summary>Gets a quote before it expires or after it has been confirmed.</summary>
    /// <param name="quoteId">The quote identifier returned when the quote was created.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The quote details.</returns>
    /// <response code="200">The quote was found.</response>
    /// <response code="404">The quote does not exist.</response>
    /// <response code="410">The unconfirmed quote has expired.</response>
    [HttpGet("quote/{quoteId:guid}")]
    public async Task<IActionResult> GetQuote(Guid quoteId, CancellationToken cancellationToken)
    {
        var result = await _checkoutService.GetQuoteAsync(quoteId, cancellationToken);
        return result.Status switch
        {
            BookingCheckoutStatus.Success => Ok(result.Response),
            BookingCheckoutStatus.QuoteNotFound => NotFound(),
            BookingCheckoutStatus.QuoteExpired => StatusCode(
                StatusCodes.Status410Gone, "The booking quote has expired."),
            _ => Problem()
        };
    }

    /// <summary>Revalidates and confirms a quote, creating a booking.</summary>
    /// <remarks>Confirmation is idempotent for a quote that has already produced a booking.</remarks>
    /// <param name="quoteId">The quote identifier to confirm.</param>
    /// <param name="request">The guest details to store with the booking.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The created booking, or the existing booking when already confirmed.</returns>
    /// <response code="200">The quote had already been confirmed.</response>
    /// <response code="201">The booking was created.</response>
    /// <response code="400">The quote or guest details are invalid.</response>
    /// <response code="404">The quote or hotel does not exist.</response>
    /// <response code="409">The hotel is inactive or availability changed.</response>
    /// <response code="410">The quote has expired.</response>
    [HttpPost("quote/{quoteId:guid}/confirm")]
    public async Task<IActionResult> ConfirmQuote(
        Guid quoteId,
        [FromBody] ConfirmBookingQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var linkedBookingId = await _traceService.GetLinkedBookingIdAsync(quoteId, cancellationToken);
        await _traceService.RecordAsync(
            linkedBookingId, quoteId, "confirm", "request", null, request, cancellationToken);
        var result = await _checkoutService.ConfirmAsync(quoteId, request, cancellationToken);
        var message = result.Status switch
        {
            BookingCheckoutStatus.Success or BookingCheckoutStatus.AlreadyConfirmed => null,
            BookingCheckoutStatus.QuoteNotFound => "The booking quote does not exist.",
            BookingCheckoutStatus.QuoteExpired => "The booking quote has expired.",
            BookingCheckoutStatus.HotelNotFound => "The selected hotel no longer exists.",
            BookingCheckoutStatus.HotelInactive => "The selected hotel is inactive.",
            BookingCheckoutStatus.Unavailable => "Availability changed after the quote was created.",
            BookingCheckoutStatus.ConcurrentUpdate => "Availability changed during confirmation. Retry with a new quote.",
            _ => "The quote contains an invalid room selection."
        };
        var statusCode = result.Status switch
        {
            BookingCheckoutStatus.Success => StatusCodes.Status201Created,
            BookingCheckoutStatus.AlreadyConfirmed => StatusCodes.Status200OK,
            BookingCheckoutStatus.QuoteNotFound or BookingCheckoutStatus.HotelNotFound =>
                StatusCodes.Status404NotFound,
            BookingCheckoutStatus.QuoteExpired => StatusCodes.Status410Gone,
            BookingCheckoutStatus.HotelInactive or BookingCheckoutStatus.Unavailable or
                BookingCheckoutStatus.ConcurrentUpdate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        IActionResult actionResult = result.Status switch
        {
            BookingCheckoutStatus.Success => CreatedAtAction(
                nameof(GetBooking), new { id = result.Response!.BookingId }, result.Response),
            BookingCheckoutStatus.AlreadyConfirmed => Ok(result.Response),
            BookingCheckoutStatus.QuoteNotFound or BookingCheckoutStatus.HotelNotFound => NotFound(message),
            BookingCheckoutStatus.QuoteExpired => StatusCode(statusCode, message),
            BookingCheckoutStatus.HotelInactive or BookingCheckoutStatus.Unavailable or
                BookingCheckoutStatus.ConcurrentUpdate => Conflict(message),
            _ => BadRequest(message)
        };
        var bookingId = result.Response?.BookingId;
        await _traceService.RecordAsync(
            bookingId,
            quoteId,
            "confirm",
            "response",
            statusCode,
            result.Response ?? (object)new { message },
            cancellationToken);
        if (result.Status == BookingCheckoutStatus.Success && bookingId.HasValue)
        {
            await _traceService.LinkProcessToBookingAsync(quoteId, bookingId.Value, cancellationToken);
        }

        return actionResult;
    }

    /// <summary>Gets the request and response traces associated with a booking.</summary>
    /// <param name="id">The booking identifier.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>Trace entries ordered by creation time, including operation, direction, status, and payload.</returns>
    /// <response code="200">The booking traces were returned; the list may be empty.</response>
    /// <response code="404">The booking does not exist.</response>
    [HttpGet("{id:long}/traces")]
    public async Task<IActionResult> GetBookingTraces(long id, CancellationToken cancellationToken)
    {
        var traces = await _traceService.GetByBookingIdAsync(id, cancellationToken);
        return traces is null ? NotFound() : Ok(traces);
    }

    /// <summary>Searches bookings with optional filters, sorting, and pagination.</summary>
    /// <param name="hotelId">Optional hotel identifier.</param>
    /// <param name="date">Optional check-in date filter.</param>
    /// <param name="bookingDate">Optional booking-creation date filter.</param>
    /// <param name="status">Optional booking status: <c>booked</c> or <c>canceled</c>.</param>
    /// <param name="sortBy">Sort field: <c>id</c> or <c>bookingDate</c>.</param>
    /// <param name="sortDirection">Sort direction: <c>asc</c> or <c>desc</c>.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of results per page, capped at 100.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching booking page, its total count, and pagination values.</returns>
    /// <response code="200">The matching bookings were returned.</response>
    /// <response code="400">The status or sort options are invalid.</response>
    [HttpGet]
    public async Task<ActionResult<object>> GetBookings(
        [FromQuery] long? hotelId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? bookingDate,
        [FromQuery] string? status,
        [FromQuery] string sortBy = "bookingDate",
        [FromQuery] string sortDirection = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidStatusFilter(status))
        {
            return BadRequest("Status must be 'booked' or 'canceled'.");
        }

        if (!IsValidSort(sortBy, sortDirection))
        {
            return BadRequest("Sort must be by 'id' or 'bookingDate' with direction 'asc' or 'desc'.");
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _dbContext.Bookings.AsNoTracking().AsQueryable();
        if (hotelId.HasValue)
        {
            query = query.Where(booking => booking.HotelId == hotelId.Value);
        }

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(booking => booking.CheckIn >= start && booking.CheckIn < end);
        }

        if (bookingDate.HasValue)
        {
            var start = bookingDate.Value.ToDateTime(TimeOnly.MinValue);
            var end = bookingDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(booking => booking.BookingDate >= start && booking.BookingDate < end);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(booking => booking.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize)));
        var sortedQuery = (sortBy.ToLowerInvariant(), sortDirection.ToLowerInvariant()) switch
        {
            ("id", "asc") => query.OrderBy(booking => booking.Id),
            ("id", "desc") => query.OrderByDescending(booking => booking.Id),
            ("bookingdate", "asc") => query.OrderBy(booking => booking.BookingDate).ThenBy(booking => booking.Id),
            _ => query.OrderByDescending(booking => booking.BookingDate).ThenByDescending(booking => booking.Id)
        };
        var bookings = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(booking => new
            {
                Booking = booking,
                HotelName = booking.Hotel.Name
            })
            .ToListAsync(cancellationToken);

        var ids = bookings.Select(item => item.Booking.Id).ToList();
        var bookedRooms = await _dbContext.BookedRooms
            .AsNoTracking()
            .Where(room => ids.Contains(room.BookingId))
            .Select(room => new
            {
                room.BookingId,
                room.RoomId,
                room.Name,
                room.Price
            })
            .ToListAsync(cancellationToken);

        var items = bookings.Select(item => new
        {
            item.Booking.Id,
            item.Booking.HotelId,
            item.HotelName,
            item.Booking.BookingDate,
            item.Booking.CheckIn,
            item.Booking.CheckOut,
            item.Booking.Status,
            item.Booking.TotalPrice,
            item.Booking.GuestName,
            item.Booking.GuestPhone,
            item.Booking.GuestEmail,
            Rooms = bookedRooms
                .Where(room => room.BookingId == item.Booking.Id)
                .Select(room => new { room.RoomId, room.Name, room.Price })
        });

        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>Gets a booking and its booked room snapshots.</summary>
    /// <param name="id">The booking identifier.</param>
    /// <returns>The booking, hotel name, guest details, dates, status, and booked rooms.</returns>
    /// <response code="200">The booking was found.</response>
    /// <response code="404">The booking does not exist.</response>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> GetBooking(long id)
    {
        var booking = await _dbContext.Bookings
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                Booking = item,
                HotelName = item.Hotel.Name
            })
            .FirstOrDefaultAsync();
        if (booking is null)
        {
            return NotFound();
        }

        var rooms = await _dbContext.BookedRooms
            .AsNoTracking()
            .Where(room => room.BookingId == id)
            .Select(room => new { room.RoomId, room.Name, room.Description, room.Price })
            .ToListAsync();

        return Ok(new
        {
            booking.Booking.Id,
            booking.Booking.HotelId,
            booking.HotelName,
            booking.Booking.BookingDate,
            booking.Booking.CheckIn,
            booking.Booking.CheckOut,
            booking.Booking.Status,
            booking.Booking.TotalPrice,
            booking.Booking.GuestName,
            booking.Booking.GuestPhone,
            booking.Booking.GuestEmail,
            Rooms = rooms
        });
    }

    /// <summary>Gets all bookings for a hotel, ordered from newest to oldest.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <returns>The hotel's bookings.</returns>
    /// <response code="200">The bookings were returned; the list may be empty.</response>
    [HttpGet("hotel/{hotelId:long}")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetBookingsByHotel(long hotelId)
    {
        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.HotelId == hotelId)
            .OrderByDescending(booking => booking.BookingDate)
            .ToListAsync();
        return Ok(bookings);
    }

    /// <summary>Gets operational booking counts for a business date.</summary>
    /// <param name="date">The date used to count arrivals and departures.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>Arrival, departure, booked, and canceled counts.</returns>
    /// <response code="200">The summary was returned.</response>
    [HttpGet("summary")]
    public async Task<ActionResult<OverviewSummaryResponse>> GetSummary(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        return Ok(await _overviewService.GetSummaryAsync(date, cancellationToken));
    }

    /// <summary>Cancels a booking; bookings cannot otherwise be modified.</summary>
    /// <remarks>Cancellation is safe to retry. Each attempt and its response are recorded in the booking traces.</remarks>
    /// <param name="id">The booking identifier.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when the booking is canceled or was already canceled.</returns>
    /// <response code="204">The booking is canceled.</response>
    /// <response code="404">The booking does not exist.</response>
    /// <response code="409">A concurrent booking update prevented cancellation.</response>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelBooking(long id, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Bookings.AnyAsync(booking => booking.Id == id, cancellationToken))
        {
            return NotFound();
        }

        await _traceService.RecordAsync(
            id, null, "cancel", "request", null, new { bookingId = id }, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var updated = await _dbContext.Bookings
                .Where(booking => booking.Id == id && booking.Status == "booked")
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(booking => booking.Status, "canceled"),
                    cancellationToken);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await _dbContext.Bookings
                    .AsNoTracking()
                    .Where(booking => booking.Id == id)
                    .Select(booking => booking.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                var statusCode = existing == "canceled"
                    ? StatusCodes.Status204NoContent
                    : StatusCodes.Status404NotFound;
                await _traceService.RecordAsync(
                    id,
                    null,
                    "cancel",
                    "response",
                    statusCode,
                    new { bookingId = id, status = existing },
                    cancellationToken);
                return existing == "canceled" ? NoContent() : NotFound();
            }

            var hotelId = await _dbContext.Bookings
                .AsNoTracking()
                .Where(booking => booking.Id == id)
                .Select(booking => booking.HotelId)
                .SingleAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _traceService.RecordAsync(
                id,
                null,
                "cancel",
                "response",
                StatusCodes.Status204NoContent,
                new { bookingId = id, status = "canceled" },
                cancellationToken);
            await _inventoryCache.RefreshHotelAsync(hotelId, CancellationToken.None);
            return NoContent();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            await _traceService.RecordAsync(
                id,
                null,
                "cancel",
                "response",
                StatusCodes.Status409Conflict,
                new { message = "Booking status changed while canceling. Reload the booking and try again." },
                cancellationToken);
            return Conflict("Booking status changed while canceling. Reload the booking and try again.");
        }
    }

    private static bool IsValidStatusFilter(string? status) =>
        string.IsNullOrWhiteSpace(status) || status is "booked" or "canceled";

    private static bool IsValidSort(string sortBy, string sortDirection) =>
        sortBy.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        sortBy.Equals("bookingDate", StringComparison.OrdinalIgnoreCase)
            ? sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
              sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
            : false;

}
