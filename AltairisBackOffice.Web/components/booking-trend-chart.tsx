type BookingTrendChartProps = {
  data: Array<{ date: string; count: number }>;
  scope?: string;
  anchorDate: string;
  isLoading?: boolean;
  onAnchorDateChange: (date: string) => void;
};

const chartWidth = 760;
const chartHeight = 250;
const left = 42;
const right = 12;
const top = 18;
const bottom = 205;

export function BookingTrendChart({
  data,
  scope = "all properties",
  anchorDate,
  isLoading = false,
  onAnchorDateChange,
}: BookingTrendChartProps) {
  const maximum = Math.max(1, ...data.map((day) => day.count));
  const plotWidth = chartWidth - left - right;
  const plotHeight = bottom - top;
  const slotWidth = plotWidth / Math.max(data.length, 1);
  const barWidth = Math.min(46, slotWidth * 0.52);
  const ticks = [...new Set([0, Math.ceil(maximum / 2), maximum])];
  const today = localDate();
  const rangeStart = shiftDate(anchorDate, -6);
  const isToday = anchorDate === today;

  return <section className="panel booking-trend">
    <div className="panel-heading">
      <div>
        <h2>Bookings created</h2>
        <p>Daily booking volume · {formatRange(rangeStart, anchorDate)} · {scope}{isLoading ? " · Updating…" : ""}</p>
      </div>
      <div className="booking-chart-controls" aria-label="Chart date navigation">
        <button
          className="button button-small"
          type="button"
          aria-label="Show previous 7 days"
          disabled={isLoading}
          onClick={() => onAnchorDateChange(shiftDate(anchorDate, -7))}
        >←</button>
        <input
          aria-label="Chart week ending date"
          type="date"
          value={anchorDate}
          max={today}
          disabled={isLoading}
          onChange={(event) => onAnchorDateChange(event.target.value)}
        />
        <button
          className="button button-small"
          type="button"
          disabled={isLoading || isToday}
          onClick={() => onAnchorDateChange(
            minDate(shiftDate(anchorDate, 7), today),
          )}
        >→</button>
        {!isToday && (
          <button
            className="button button-small"
            type="button"
            disabled={isLoading}
            onClick={() => onAnchorDateChange(today)}
          >Today</button>
        )}
      </div>
    </div>
    <div className="booking-chart-scroll">
      <svg
        className="booking-chart"
        viewBox={`0 0 ${chartWidth} ${chartHeight}`}
        role="img"
        aria-label={`Bar chart of bookings created per day from ${rangeStart} to ${anchorDate} for ${scope}`}
      >
        <title>{`Bookings created per day from ${rangeStart} to ${anchorDate}`}</title>
        {ticks.map((tick) => {
          const y = bottom - tick / maximum * plotHeight;
          return <g key={tick}>
            <line x1={left} x2={chartWidth - right} y1={y} y2={y} className="booking-chart-gridline" />
            <text x={left - 10} y={y + 4} textAnchor="end" className="booking-chart-axis">{tick}</text>
          </g>;
        })}
        {data.map((day, index) => {
          const x = left + index * slotWidth + (slotWidth - barWidth) / 2;
          const barHeight = day.count / maximum * plotHeight;
          const y = bottom - barHeight;
          const label = formatDay(day.date);
          return <g key={day.date}>
            <rect
              x={x}
              y={day.count ? y : bottom - 2}
              width={barWidth}
              height={day.count ? barHeight : 2}
              rx="4"
              className="booking-chart-bar"
            >
              <title>{`${label}: ${day.count} ${day.count === 1 ? "booking" : "bookings"}`}</title>
            </rect>
            <text x={x + barWidth / 2} y={day.count ? Math.max(top - 3, y - 7) : bottom - 8} textAnchor="middle" className="booking-chart-value">{day.count}</text>
            <text x={x + barWidth / 2} y={bottom + 24} textAnchor="middle" className="booking-chart-axis">{label}</text>
          </g>;
        })}
      </svg>
    </div>
  </section>;
}

function formatDay(value: string) {
  return new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short" })
    .format(new Date(`${value}T12:00:00`));
}

function formatRange(start: string, end: string) {
  const formatter = new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
  return `${formatter.format(new Date(`${start}T00:00:00Z`))} – ${formatter.format(new Date(`${end}T00:00:00Z`))}`;
}

function shiftDate(value: string, days: number) {
  const date = new Date(`${value}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function minDate(left: string, right: string) {
  return left < right ? left : right;
}

function localDate(date = new Date()) {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
}
