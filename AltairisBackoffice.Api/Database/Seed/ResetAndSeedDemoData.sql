-- Destructive: replaces all hotel, room, allotment, inventory, booking, and trace data.
-- Run only against the local/demo database. The transaction preserves old data if setup fails.
BEGIN;

TRUNCATE TABLE
    booking_trace,
    booked_rooms,
    booking,
    room_inventory,
    room,
    allotment,
    hotel
RESTART IDENTITY CASCADE;

INSERT INTO hotel (name, email, phone, active, address, city, country, description)
VALUES
    ('Madrid Horizon Hotel', 'reservations@madridhorizon.example', '+34 910 555 101', true, '18 Calle de Atocha', 'Madrid', 'Spain', 'A full-service city hotel for corporate stays, museum visits and weekend breaks.'),
    ('Barcelona Marina House', 'stay@barcelonamarinahouse.example', '+34 930 555 102', true, '42 Carrer de la Marina', 'Barcelona', 'Spain', 'A contemporary waterfront property with easy access to the old town and convention district.'),
    ('Lisbon Alfama Courtyard', 'welcome@alfamacourtyard.example', '+351 21 555 0103', true, '7 Rua das Escolas Gerais', 'Lisbon', 'Portugal', 'A characterful boutique hotel serving leisure guests and small cultural groups.'),
    ('Porto Riverside Hotel', 'bookings@portoriverside.example', '+351 22 555 0104', true, '26 Rua do Infante', 'Porto', 'Portugal', 'A riverside hotel with flexible rooms for wine tours, business travel and family holidays.'),
    ('Rome Via Veneto Hotel', 'reservations@viavenetorome.example', '+39 06 5550 105', true, '33 Via Piemonte', 'Rome', 'Italy', 'An elegant central base for heritage tours, business events and extended city stays.'),
    ('Paris Left Bank Residence', 'bonjour@leftbankparis.example', '+33 1 55 55 01 06', true, '15 Rue Monge', 'Paris', 'France', 'A refined Left Bank property popular with couples, families and international city-break guests.'),
    ('Berlin Mitte StadtHotel', 'stay@berlinmittehotel.example', '+49 30 5550 107', true, '21 Linienstrasse', 'Berlin', 'Germany', 'A design-led hotel for trade fairs, creative conferences and independent travellers.'),
    ('Amsterdam Canal Quarter', 'reservations@canalquarter.example', '+31 20 555 0108', false, '9 Prinsengracht', 'Amsterdam', 'Netherlands', 'A canal-side hotel temporarily closed to new reservations during a phased refurbishment.'),
    ('Prague Charles Bridge Hotel', 'welcome@charlesbridgeprague.example', '+420 2 5550 109', true, '12 Karlova', 'Prague', 'Czechia', 'A historic-quarter property with room categories for short breaks and guided tour groups.'),
    ('London Kensington Gardens', 'bookings@kensingtongardens.example', '+44 20 5555 0110', false, '28 Queens Gate', 'London', 'United Kingdom', 'A classic London hotel temporarily unavailable while guest floors are being renovated.');

INSERT INTO room (name, description, price, "hotelId")
SELECT
    CASE
        WHEN room_type.room_number = 7 THEN hotel.city || ' Junior Suite'
        WHEN room_type.room_number = 8 THEN hotel.city || ' Signature Suite'
        ELSE room_type.name
    END,
    room_type.description,
    round((room_type.base_price * (0.90 + hotel.id * 0.035))::numeric, 2)::double precision,
    hotel.id
FROM hotel
CROSS JOIN LATERAL (
    VALUES
        (1, 'Classic Single', 'A quiet single-occupancy room with a work desk, ensuite shower and practical storage.', 79.00::numeric),
        (2, 'Standard Double', 'A comfortable double room with a queen bed, ensuite bathroom and tea-making facilities.', 105.00::numeric),
        (3, 'Superior Twin', 'A bright twin room with two separate beds and additional space for luggage.', 119.00::numeric),
        (4, 'Deluxe King', 'A larger king room with upgraded linens, a seating corner and enhanced bathroom amenities.', 149.00::numeric),
        (5, 'Family Room', 'A flexible family layout with a double bed, extra sleeping space and room for four guests.', 179.00::numeric),
        (6, 'Executive Room', 'A business-ready room with a lounge chair, generous desk and premium in-room amenities.', 199.00::numeric),
        (7, 'Junior Suite', 'A suite with a separate sitting area, curated local details and a larger bathroom.', 239.00::numeric),
        (8, 'Signature Suite', 'A spacious top-category suite with a separate lounge and premium views where available.', 319.00::numeric)
) AS room_type(room_number, name, description, base_price)
WHERE room_type.room_number <= 3 + ((hotel.id * 5 + 2) % 6);

CREATE TEMP TABLE demo_room_seed ON COMMIT DROP AS
SELECT
    room.id AS room_id,
    room."hotelId" AS hotel_id,
    row_number() OVER (PARTITION BY room."hotelId" ORDER BY room.id)::integer AS room_number
FROM room;

INSERT INTO allotment (value, num_adults, num_children, num_babies, free_sale)
SELECT
    CASE WHEN room_number IN (1, 7) THEN NULL
         ELSE 2 + ((room_number + hotel_id) % 5)
    END,
    2,
    CASE WHEN room_number IN (5, 8) THEN 2
         WHEN room_number = 7 THEN 1 ELSE 0
    END,
    CASE WHEN room_number IN (5, 8) THEN 1 ELSE 0 END,
    room_number IN (1, 7)
FROM demo_room_seed;

WITH numbered_rooms AS (
    SELECT id, row_number() OVER (ORDER BY id) AS row_number
    FROM room
),
numbered_allotments AS (
    SELECT id, row_number() OVER (ORDER BY id) AS row_number
    FROM allotment
)
UPDATE room
SET allotment_id = numbered_allotments.id
FROM numbered_rooms
JOIN numbered_allotments USING (row_number)
WHERE room.id = numbered_rooms.id;

DO $$
DECLARE
    period_start date := (date_trunc('month', CURRENT_DATE) - interval '2 months')::date;
    period_end date := (date_trunc('month', CURRENT_DATE) + interval '4 months' - interval '1 day')::date;
BEGIN
    INSERT INTO room_inventory (room_id, date, capacity, free_sale)
    SELECT
        room.id,
        calendar.day::date,
        CASE
            WHEN calendar.day::date BETWEEN CURRENT_DATE - 14 AND CURRENT_DATE + 14
                THEN 8 + ((room.id + extract(doy FROM calendar.day)::integer) % 4)::integer
            WHEN (extract(month FROM calendar.day)::integer = 12 AND extract(day FROM calendar.day)::integer >= 24)
              OR (extract(month FROM calendar.day)::integer = 1 AND extract(day FROM calendar.day)::integer <= 2)
              OR (extract(month FROM calendar.day)::integer = 2 AND extract(day FROM calendar.day)::integer BETWEEN 12 AND 16)
                THEN 1 + ((room.id + extract(doy FROM calendar.day)::integer) % 2)::integer
            WHEN extract(isodow FROM calendar.day)::integer IN (5, 6)
                THEN 2 + ((room.id + extract(doy FROM calendar.day)::integer) % 4)::integer
            ELSE 3 + ((room.id * 3 + extract(doy FROM calendar.day)::integer) % 5)::integer
        END,
        (
            (demo_room_seed.room_number = 1 AND calendar.day::date >= CURRENT_DATE
                AND extract(month FROM calendar.day)::integer IN (11, 12, 1, 2))
            OR (demo_room_seed.room_number = 2 AND calendar.day::date >= CURRENT_DATE
                AND extract(isodow FROM calendar.day)::integer IN (5, 6)
                AND extract(month FROM calendar.day)::integer IN (12, 1, 2))
            OR ((demo_room_seed.room_number + extract(doy FROM calendar.day)::integer + hotel.id) % 31 = 0)
        )
    FROM room
    JOIN demo_room_seed ON demo_room_seed.room_id = room.id
    JOIN hotel ON hotel.id = room."hotelId"
    CROSS JOIN LATERAL generate_series(period_start, period_end, interval '1 day') AS calendar(day)
    WHERE NOT (
        calendar.day::date NOT BETWEEN CURRENT_DATE - 14 AND CURRENT_DATE + 14
        AND (
            (extract(doy FROM calendar.day)::integer + hotel.id * 11) % 53 = 0
            OR (extract(doy FROM calendar.day)::integer * 3 + room.id * 7) % 41 = 0
        )
    );
END
$$;

DO $$
BEGIN
    IF (SELECT count(*) FROM hotel) <> 10 THEN
        RAISE EXCEPTION 'Expected 10 hotels.';
    END IF;

    IF EXISTS (
        SELECT hotel.id
        FROM hotel
        LEFT JOIN room ON room."hotelId" = hotel.id
        GROUP BY hotel.id
        HAVING count(room.id) NOT BETWEEN 3 AND 8
    ) THEN
        RAISE EXCEPTION 'Every hotel must have between 3 and 8 room types.';
    END IF;

    IF (SELECT count(*) FROM room WHERE allotment_id IS NOT NULL) <> (SELECT count(*) FROM room) THEN
        RAISE EXCEPTION 'Every room type must be linked to an allotment.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM room_inventory WHERE free_sale)
       OR NOT EXISTS (SELECT 1 FROM room_inventory WHERE NOT free_sale)
    THEN
        RAISE EXCEPTION 'Both Free Sale and limited inventory must be configured.';
    END IF;
END
$$;

COMMIT;

SELECT 'hotel' AS entity, count(*) AS records FROM hotel
UNION ALL SELECT 'room', count(*) FROM room
UNION ALL SELECT 'allotment', count(*) FROM allotment
UNION ALL SELECT 'room_inventory', count(*) FROM room_inventory
UNION ALL SELECT 'booking', count(*) FROM booking
UNION ALL SELECT 'booked_rooms', count(*) FROM booked_rooms
UNION ALL SELECT 'booking_trace', count(*) FROM booking_trace
ORDER BY entity;

SELECT min(date) AS inventory_from, max(date) AS inventory_to,
       count(*) FILTER (WHERE free_sale) AS free_sale_days,
       count(*) FILTER (WHERE NOT free_sale) AS limited_inventory_days
FROM room_inventory;
