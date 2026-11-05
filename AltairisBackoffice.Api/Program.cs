using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.src.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AltairisDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddControllers();
builder.Services.AddScoped<IInventoryCalendarService, InventoryCalendarService>();
builder.Services.AddScoped<IOverviewService, OverviewService>();
builder.Services.AddScoped<IBookingCheckoutService, BookingCheckoutService>();
builder.Services.AddScoped<IBookingTraceService, BookingTraceService>();
builder.Services.AddSingleton<BookingQuoteStore>();
builder.Services.AddSingleton<IBookingQuoteStore>(services =>
    services.GetRequiredService<BookingQuoteStore>());
builder.Services.AddHostedService(services =>
    services.GetRequiredService<BookingQuoteStore>());
builder.Services.AddSingleton<HotelInventoryCache>();
builder.Services.AddSingleton<IHotelInventoryCache>(services =>
    services.GetRequiredService<HotelInventoryCache>());
builder.Services.AddHostedService(services =>
    services.GetRequiredService<HotelInventoryCache>());

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AltairisDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.MapScalarApiReference(options =>
        options.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json"));
    app.MapGet("/", () => Results.Redirect("/scalar/v1"));
}

app.MapControllers();

app.Run();