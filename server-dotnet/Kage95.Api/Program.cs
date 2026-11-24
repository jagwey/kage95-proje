using System.Reflection;
using System.Threading.RateLimiting;
using Kage95.Api.Data;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Read optional root .env for seamless compatibility with local MySQL settings
LoadDotEnvIfExists(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env")));

var connectionString = BuildConnectionString(builder.Configuration);

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<Kage95DbContext>(options =>
        options.UseMySQL(connectionString));
}

builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<PointsService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KAGE95 Manga API",
        Version = "v1",
        Description = "ASP.NET Core 10 + EF Core + MySQL API: Auth, Hesabım, Liderlik, Katalog, Okuma Listesi, Bölüm Okuyucu, Arkadaşlık/Engelleme ve Admin/Moderatör Paneli."
    });
    options.OperationFilter<Kage95HeaderOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Çok fazla işlem yaptın. Biraz sonra tekrar dene." }, ct);
    };
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(15);
        limiter.PermitLimit = 40;
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("write", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 120;
        limiter.QueueLimit = 0;
    });
});

// Listen on 127.0.0.1:3001 by default unless ASPNETCORE_URLS or TestServer is used
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    && !builder.Environment.IsEnvironment("Testing"))
{
    var host = Environment.GetEnvironmentVariable("HOST") ?? "127.0.0.1";
    var port = Environment.GetEnvironmentVariable("PORT") ?? "3001";
    builder.WebHost.UseUrls($"http://{host}:{port}");
}

var app = builder.Build();

// Ensure uploads directory exists
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsDir);

// Migrate & seed database on startup (skipped when Testing configures its own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Kage95DbContext>();
    await db.Database.MigrateAsync();
    await CatalogSeeder.SeedAsync(db, app.Configuration, app.Environment.ContentRootPath);
}

app.UseMiddleware<ApiExceptionMiddleware>();

// Swagger UI available at /swagger with X-Kage95: 1 automatically attached for write endpoints
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KAGE95 Manga API v1");
    options.DocumentTitle = "KAGE95 — Swagger API";
    options.UseRequestInterceptor("(req) => { if (!['GET','HEAD','OPTIONS'].includes((req.method||'GET').toUpperCase())) { req.headers['X-Kage95'] = '1'; } return req; }");
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

var distDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "dist"));
if (Directory.Exists(distDir))
{
    var distProvider = new PhysicalFileProvider(distDir);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = distProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = distProvider });
}

app.UseRateLimiter();
app.UseMiddleware<SecurityAndSessionMiddleware>();
app.MapControllers();

app.Map("/api/{**catchall}", () => Results.Json(new { error = "API yolu bulunamadı." }, statusCode: 404));

app.Run();

static void LoadDotEnvIfExists(string path)
{
    if (!File.Exists(path)) return;
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var eq = line.IndexOf('=');
        if (eq <= 0) continue;
        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim();
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}

static string BuildConnectionString(IConfiguration config)
{
    var host = Environment.GetEnvironmentVariable("DB_HOST");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var user = Environment.GetEnvironmentVariable("DB_USER");
    if (!string.IsNullOrWhiteSpace(host) || !string.IsNullOrWhiteSpace(dbName) || !string.IsNullOrWhiteSpace(user))
    {
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
        var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
        return $"Server={host ?? "127.0.0.1"};Port={port};Database={dbName ?? "kage95"};User={user ?? "kage95"};Password={pass};CharSet=utf8mb4;";
    }
    return config.GetConnectionString("Default")
        ?? "Server=127.0.0.1;Port=3306;Database=kage95;User=kage95;Password=;CharSet=utf8mb4;";
}

public partial class Program { }
