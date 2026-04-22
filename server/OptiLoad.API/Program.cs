using Microsoft.AspNetCore.Diagnostics;
using OptiLoad.Core.Services;
using OptiLoad.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);
builder.Services.AddMemoryCache();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OptiLoad API", Version = "v1" });
});

// CORS
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

app.UseResponseCompression();

// Global exception handler
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var msg     = app.Environment.IsDevelopment() && feature != null
        ? feature.Error.Message
        : "Internal server error.";
    await ctx.Response.WriteAsync($"{{\"error\":\"{msg}\"}}");
}));

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("DevFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OptiLoad API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
