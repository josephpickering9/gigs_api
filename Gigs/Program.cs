using System.Text.Json.Serialization;
using Auth0.AspNetCore.Authentication;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Gigs.Exceptions;
using Gigs.Filters;
using Gigs.Services;
using Gigs.Services.Image;
using Gigs.Types;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Database>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.CommandTimeout(300))
);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpClient();

// Repositories
builder.Services.AddScoped<Gigs.Repositories.IGigRepository, Gigs.Repositories.GigRepository>();
builder.Services.AddScoped<Gigs.Repositories.IArtistRepository, Gigs.Repositories.ArtistRepository>();
builder.Services.AddScoped<Gigs.Repositories.IVenueRepository, Gigs.Repositories.VenueRepository>();
builder.Services.AddScoped<Gigs.Repositories.IDashboardRepository, Gigs.Repositories.DashboardRepository>();

// Services
builder.Services.AddScoped<IGigService, GigService>();
builder.Services.AddScoped<IArtistService, ArtistService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<Gigs.Services.AI.IAiEnrichmentService, Gigs.Services.AI.AiEnrichmentService>();
builder.Services.AddScoped<Gigs.Services.Calendar.IGoogleCalendarService, Gigs.Services.Calendar.GoogleCalendarService>();




builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gigs API", Version = "v1" });

    c.OperationFilter<SwaggerFileOperationFilter>();
    c.SchemaFilter<EnumDescriptionSchemaFilter>();
    c.SupportNonNullableReferenceTypes();

    // Expose strongly-typed IDs as simple uuid strings in OpenAPI
    c.MapType<GigId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });
    c.MapType<ArtistId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });
    c.MapType<VenueId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });
    c.MapType<PersonId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });
    c.MapType<GigArtistId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });
    c.MapType<SongId>(() => new OpenApiSchema { Type = "string", Format = "uuid" });

});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new IdJsonConverterFactory());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddAuthorization();
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"] ?? "";
    options.ClientId = builder.Configuration["Auth0:ClientId"] ?? "";
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth0:Domain"];
    options.Audience = builder.Configuration["Auth0:Audience"];
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var (statusCode, message) = exceptionHandlerPathFeature?.Error switch
        {
            NotFoundException notFoundException => (StatusCodes.Status404NotFound, notFoundException.Message),
            ConflictException conflictException => (StatusCodes.Status409Conflict, conflictException.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(message);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}
