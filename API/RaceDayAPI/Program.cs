using Microsoft.OpenApi.Any;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDayAPI.Data;
using RaceDayAPI.Security;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Stops [ApiController] demanding navigation properties (e.g. Event.Organizer) in request bodies
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
})
    .AddJsonOptions(options =>
    {
        // Stops "object cycle" errors when entities reference each other (Event -> Categories -> Event)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Database connection
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RaceDayConnection")));

// Server-side session management: the session data (user id and role) is kept on the server,
// the browser only receives a session id cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// Authentication scheme that reads the logged-in user and role from the session
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay Event Management API",
        Version = "v1",
        Description = "API for participants and organisers to manage race events, categories, enrolments and results. Log in through the Auth endpoints; the server-side session cookie is then sent automatically by the browser."
    });

    // Show finish times as "hh:mm:ss" in Swagger instead of the confusing {"ticks": 0} object
    options.MapType<TimeSpan>(() => new OpenApiSchema
    {
        Type = "string",
        Example = new OpenApiString("00:52:30")
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Order matters: Session -> Authentication -> Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();