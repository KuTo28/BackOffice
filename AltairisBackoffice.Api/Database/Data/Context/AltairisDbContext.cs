using System;
using System.Collections.Generic;
using AltairisBackoffice.Api.Database.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.Database.Data.Context;

/// <summary>EF Core database access and relational mappings for the backoffice domain.</summary>
public partial class AltairisDbContext : DbContext
{
    public AltairisDbContext(DbContextOptions<AltairisDbContext> options)
        : base(options)
    {
    }

    /// <summary>Room allotment configurations.</summary>
    public virtual DbSet<Allotment> Allotments { get; set; }

    /// <summary>Room snapshots attached to confirmed bookings.</summary>
    public virtual DbSet<BookedRoom> BookedRooms { get; set; }

    /// <summary>Hotel bookings.</summary>
    public virtual DbSet<Booking> Bookings { get; set; }

    /// <summary>Persisted booking workflow request and response traces.</summary>
    public virtual DbSet<BookingTrace> BookingTraces { get; set; }

    /// <summary>Hotel profiles.</summary>
    public virtual DbSet<Hotel> Hotels { get; set; }

    /// <summary>Hotel room types.</summary>
    public virtual DbSet<Room> Rooms { get; set; }

    /// <summary>Per-room, per-night inventory configuration.</summary>
    public virtual DbSet<RoomInventory> RoomInventories { get; set; }

    /// <summary>Configures entity-to-table mappings, keys, defaults, and relationships.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Allotment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("allotment_pkey");

            entity.ToTable("allotment");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FreeSale)
                .HasDefaultValue(true)
                .HasColumnName("free_sale");
            entity.Property(e => e.NumAdults).HasColumnName("num_adults");
            entity.Property(e => e.NumBabies).HasColumnName("num_babies");
            entity.Property(e => e.NumChildren).HasColumnName("num_children");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<BookedRoom>(entity =>
        {
            // The existing booked_rooms table has no declared primary key.
            entity
                .HasNoKey()
                .ToTable("booked_rooms");

            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.RoomId).HasColumnName("room_id");

            entity.HasOne(d => d.Booking).WithMany()
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("booked_rooms_booking_id_fkey");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("booking_pkey");

            entity.ToTable("booking");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BookingDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("booking_date");
            entity.Property(e => e.CheckIn)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("check_in");
            entity.Property(e => e.CheckOut)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("check_out");
            entity.Property(e => e.GuestEmail)
                .HasMaxLength(255)
                .HasColumnName("guest_email");
            entity.Property(e => e.GuestName)
                .HasMaxLength(255)
                .HasColumnName("guest_name");
            entity.Property(e => e.GuestPhone)
                .HasMaxLength(255)
                .HasColumnName("guest_phone");
            entity.Property(e => e.HotelId).HasColumnName("hotel_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasColumnName("status");
            entity.Property(e => e.TotalPrice).HasColumnName("total_price");

            entity.HasOne(d => d.Hotel).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.HotelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("booking_hotel_id_fkey");
        });

        modelBuilder.Entity<BookingTrace>(entity =>
        {
            entity.HasKey(trace => trace.Id).HasName("booking_trace_pkey");

            entity.ToTable("booking_trace");

            entity.Property(trace => trace.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(trace => trace.BookingId).HasColumnName("booking_id");
            entity.Property(trace => trace.ParentId).HasColumnName("parent_id");
            entity.Property(trace => trace.Operation)
                .HasMaxLength(32)
                .HasColumnName("operation");
            entity.Property(trace => trace.Direction)
                .HasMaxLength(16)
                .HasColumnName("direction");
            entity.Property(trace => trace.StatusCode).HasColumnName("status_code");
            entity.Property(trace => trace.Payload)
                .HasColumnType("jsonb")
                .HasColumnName("payload");
            entity.Property(trace => trace.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.HasOne<Booking>()
                .WithMany()
                .HasForeignKey(trace => trace.BookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_booking_trace_booking");
        });

        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Hotel_pkey");

            entity.ToTable("hotel");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Country)
                .HasMaxLength(255)
                .HasColumnName("country");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .HasColumnName("phone");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Room_pkey");

            entity.ToTable("room");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AllotmentId).HasColumnName("allotment_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.HotelId).HasColumnName("hotelId");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price).HasColumnName("price");

            entity.HasOne(d => d.Allotment).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.AllotmentId)
                .HasConstraintName("fk_room_allotment");

            entity.HasOne(d => d.Hotel).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.HotelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("room_hotelId_fkey");
        });

        modelBuilder.Entity<RoomInventory>(entity =>
        {
            entity.HasKey(e => new { e.RoomId, e.Date });

            entity.ToTable("room_inventory");

            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.FreeSale).HasColumnName("free_sale");

            entity.HasOne(e => e.Room)
                .WithMany()
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
