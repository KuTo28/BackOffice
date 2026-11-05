# Altairis Backoffice Web

Next.js app for hotel, room type, dated inventory, and booking operations.

## Run locally

1. Install Node.js 20.9 or newer.
2. From this folder, run `npm install`.
3. Copy `.env.example` to `.env.local` and set `ALTAIRIS_API_URL` to the running ASP.NET API origin (default `http://localhost:5054`; use `http://localhost:8080` when the API is run through Docker Compose).
4. Start the app with `npm run dev`, then open `http://localhost:3000`.

The Next.js API route proxies browser requests to the ASP.NET API, so the backend does not need browser CORS configuration.

## Run with Docker Compose

From the project root, run `docker compose up --build`. This starts PostgreSQL, the API, and the web app. Open `http://localhost:3000`; set `WEB_PORT` to change the host port. The web service reaches the API at `http://api:8080` over the Compose network.

## Database update

The API applies the dated room inventory migration at startup. The migration adds daily room capacity and a nullable room reference to booked-room snapshots. Inventory availability is calculated from configured capacity less active booked reservations; canceling a booking changes its status to `canceled`, which releases the reserved capacity without changing the immutable booking details.

## Notes

- Booking status is assigned by the API and is either `booked` or `canceled`.
- Booking details are immutable after creation. Cancellation is the only post-creation operation.
- Inventory is configured by room type and date. Free-sale dates do not enforce a capacity limit.
