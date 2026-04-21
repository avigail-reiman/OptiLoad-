using OptiLoad.Core.Services;
using OptiLoad.Data;

var builder = WebApplication.CreateBuilder(args);

// ── שירותים ──
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OptiLoad API", Version = "v1" });
});

// CORS – allow file:// and localhost frontends in development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Dependency Injection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
builder.Services.AddSingleton<DatabaseService>(new DatabaseService(connectionString));
builder.Services.AddSingleton<IPackingRepository>(sp => sp.GetRequiredService<DatabaseService>());
builder.Services.AddScoped<PackingService>(sp =>
    new PackingService(sp.GetRequiredService<IPackingRepository>()));

var app = builder.Build();

// ── Pipeline ──
app.UseDefaultFiles();   // /  →  /index.html
app.UseStaticFiles();    // מגיש קבצים מ-wwwroot
app.UseCors("DevFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OptiLoad API v1");
        c.RoutePrefix = "swagger"; // Swagger זמין ב-/swagger
    });
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
