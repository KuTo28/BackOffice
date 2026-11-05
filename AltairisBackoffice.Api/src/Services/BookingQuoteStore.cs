using System.Collections.Concurrent;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>In-memory quote state with synchronization and the result of an idempotent confirmation.</summary>
public sealed class BookingQuoteState(BookingQuoteResponse quote)
{
    private ConfirmedBookingResponse? _confirmedBooking;

    /// <summary>The immutable details and expiry time of the quote.</summary>
    public BookingQuoteResponse Quote { get; } = quote;

    /// <summary>Per-quote lock ensuring only one confirmation can create a booking.</summary>
    public SemaphoreSlim ConfirmationLock { get; } = new(1, 1);

    /// <summary>The booking created for this quote, if it has been confirmed.</summary>
    public ConfirmedBookingResponse? ConfirmedBooking
    {
        get => Volatile.Read(ref _confirmedBooking);
        set => Volatile.Write(ref _confirmedBooking, value);
    }
}

/// <summary>Stores active booking quotes in memory rather than persisting temporary quote records.</summary>
public interface IBookingQuoteStore
{
    /// <summary>Adds a quote to the in-memory store.</summary>
    BookingQuoteState Add(BookingQuoteResponse quote);

    /// <summary>Looks up quote state by identifier.</summary>
    bool TryGet(Guid quoteId, out BookingQuoteState? state);
}

/// <summary>Stores quotes for the checkout flow and periodically prunes expired quote state.</summary>
public sealed class BookingQuoteStore : BackgroundService, IBookingQuoteStore
{
    private static readonly TimeSpan RetentionAfterExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<Guid, BookingQuoteState> _quotes = new();
    private readonly ILogger<BookingQuoteStore> _logger;

    public BookingQuoteStore(ILogger<BookingQuoteStore> logger)
    {
        _logger = logger;
    }

    public BookingQuoteState Add(BookingQuoteResponse quote)
    {
        var state = new BookingQuoteState(quote);
        if (!_quotes.TryAdd(quote.QuoteId, state))
        {
            throw new InvalidOperationException("A duplicate booking quote identifier was generated.");
        }

        return state;
    }

    public bool TryGet(Guid quoteId, out BookingQuoteState? state) =>
        _quotes.TryGetValue(quoteId, out state);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cutoff = DateTimeOffset.UtcNow - RetentionAfterExpiry;
            var removedCount = 0;
            foreach (var (quoteId, state) in _quotes)
            {
                if (state.Quote.ExpiresAt < cutoff && _quotes.TryRemove(quoteId, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                _logger.LogDebug(
                    "Pruned {RemovedQuoteCount} expired booking quotes; {QuoteCount} remain in memory.",
                    removedCount,
                    _quotes.Count);
            }
        }
    }
}
