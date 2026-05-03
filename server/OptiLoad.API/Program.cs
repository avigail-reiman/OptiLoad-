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

// Admin authentication and JWT
builder.Services.AddScoped<IAdminService, AdminService>();
var jwtKey = "SuperSecretKeyForJwtSignature123!"; // move to config
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// --- הרצת מבחן אוטומטית על נתוני דוגמה ---
// await OptiLoad.Core.Services.TestDataRunner.RunFromJson("../OptiLoad.Core/TestData/SampleTestData.json");
// Environment.Exit(0);
// --- סוף הרצת מבחן ---

var app = builder.Build();


app.UseResponseCompression();
app.UseAuthentication();

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

// הגשת קבצי client מתיקיית client/ שנמצאת שתי רמות מעל ה-API
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
