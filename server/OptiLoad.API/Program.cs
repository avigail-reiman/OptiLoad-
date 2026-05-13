using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using OptiLoad.Core.Services;
using OptiLoad.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OptiLoad API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
        policy.WithOrigins(
                "http://localhost:5098",
                "http://127.0.0.1:5098",
                "http://localhost:5500",
                "http://127.0.0.1:5500")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-User-Token")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
builder.Services.AddSingleton<DatabaseService>(new DatabaseService(connectionString));
builder.Services.AddSingleton<IPackingRepository>(sp => sp.GetRequiredService<DatabaseService>());
builder.Services.AddSingleton<IAdminRepository>(sp => sp.GetRequiredService<DatabaseService>());
builder.Services.AddSingleton<ISnapshotRepository>(sp => sp.GetRequiredService<DatabaseService>());
builder.Services.AddSingleton<ISessionRepository>(sp  => sp.GetRequiredService<DatabaseService>());
builder.Services.AddScoped<PackingService>(sp =>
    new PackingService(sp.GetRequiredService<IPackingRepository>()));

builder.Services.AddScoped<IAdminService, AdminService>(sp =>
    new AdminService(sp.GetRequiredService<IAdminRepository>()));
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured (Jwt:Key).");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("JWT key must be at least 32 characters.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

var app = builder.Build();

app.UseResponseCompression();
app.UseCors("DevFrontend");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminSvc = scope.ServiceProvider.GetRequiredService<IAdminService>();
        await adminSvc.SeedDefaultAdminIfEmptyAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Startup] SeedDefaultAdmin failed: {ex}");
    }
}

app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    var msg = "Internal server error.";
    await ctx.Response.WriteAsync($"{{\"error\":\"{msg}\"}}");
}));

var clientPath = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath, "..", "..", "client"));

if (Directory.Exists(clientPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath),
        RequestPath  = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath),
        RequestPath  = ""
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

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
