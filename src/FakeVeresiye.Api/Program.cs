using System.Globalization;
using System.Text;
using FakeVeresiye.Api;
using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Services.Customers;
using FakeVeresiye.Api.Services.Import;
using FakeVeresiye.Api.Services.Statements;
using FakeVeresiye.Api.Services.Transactions;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
QuestPDF.Settings.License = LicenseType.Community;

// EF Core stores `decimal` as TEXT in SQLite and sorts/compares it through a collation that
// calls decimal.Parse against the ambient culture. On a machine set to tr-TR that parse throws
// on "2650.0", so pin the ambient culture to invariant. Code that needs Turkish formatting
// (PDF export, Excel import) uses an explicit CultureInfo("tr-TR") and is unaffected.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Serilog swallows sink errors by default (e.g. a permission-denied writing the file) — a
// bind-mounted log directory that Docker auto-created as root, because nobody pre-created and
// chown'd it, fails exactly that way: no exception, no crash, just a missing file. Route
// Serilog's own diagnostics to stderr so that shows up in `docker logs` instead of vanishing.
SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog] {msg}"));

// User-action log (customer/transaction/import mutations — see the log calls in
// CustomerService, TransactionService and BackupImportService) alongside the normal
// framework/diagnostic logging. One file per day, kept for 30 days.
//
// The path is configurable (ActionLog:Path, default "logs/actions-.log") because it needs to
// land somewhere durable and writable: in Docker, the working directory is a root-owned image
// layer, not a mounted volume, so the container overrides this to a path under the
// /var/log/fakeveresiye volume (see Dockerfile / compose.yaml) — same reasoning as
// ConnectionStrings:Default pointing at /data.
//
// EF Core logs every SQL statement and ASP.NET Core logs every request at Information level;
// without the override below those would drown out the handful of action lines that actually
// matter, so framework code (anything under "Microsoft") only logs Warning and up.
var actionLogPath = builder.Configuration["ActionLog:Path"] ?? "logs/actions-.log";
builder.Host.UseSerilog((context, services, cfg) => cfg
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        actionLogPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Central exception -> HTTP response mapping (see ApiExceptionHandler) instead of a
// try/catch in every action that can fail on bad input.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Customers / transactions
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// Import
builder.Services.AddSingleton<IPreviewStore, PreviewStore>();
builder.Services.AddScoped<Veresiye5BackupReader>();
builder.Services.AddScoped<GenericExcelImporter>();
builder.Services.AddScoped<BackupImportService>();
builder.Services.AddScoped<ExcelLedgerImporter>();

// Statements / reports
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<StatementExcelExporter>();
builder.Services.AddScoped<StatementPdfExporter>();

// No CORS: the SPA is same-origin in production (served from wwwroot) and, in development,
// the Vite dev server proxies /api to this host (see vite.config.ts), so the browser never
// makes a cross-origin request. Add a policy here only if a browser client is pointed
// straight at this API from another origin.

var app = builder.Build();

// Apply pending EF Core migrations on startup rather than EnsureCreated(), so the schema has
// a real history and can be evolved (adding/renaming columns, etc.) without wiping data.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Any non-API route falls through to the SPA bundle in wwwroot (populated on publish).
app.MapFallbackToFile("index.html");

app.Run();
