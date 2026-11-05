using AltairisBackoffice.Api.Database.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AltairisBackoffice.Api.Database.Migrations;

[DbContext(typeof(AltairisDbContext))]
[Migration("20261105172000_AddDatedRoomInventory")]
public sealed class AddDatedRoomInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE booked_rooms
                ADD COLUMN IF NOT EXISTS room_id bigint NULL;

            CREATE TABLE IF NOT EXISTS room_inventory (
                room_id bigint NOT NULL,
                date date NOT NULL,
                capacity integer NOT NULL CHECK (capacity >= 0),
                free_sale boolean NOT NULL DEFAULT false,
                CONSTRAINT pk_room_inventory PRIMARY KEY (room_id, date),
                CONSTRAINT fk_room_inventory_room FOREIGN KEY (room_id)
                    REFERENCES room (id) ON DELETE CASCADE
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS room_inventory;
            ALTER TABLE booked_rooms DROP COLUMN IF EXISTS room_id;
            """);
    }
}
