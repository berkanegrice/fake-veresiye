using System.Globalization;
using System.Text;
using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Services.Import;
using FakeVeresiye.Api.Services.Statements;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
QuestPDF.Settings.License = LicenseType.Community;

// EF Core stores `decimal` as TEXT in SQLite and sorts/compares it through a collation that
// calls decimal.Parse against the ambient culture. On a machine set to tr-TR that parse throws
// on "2650.0", so pin the ambient culture to invariant. Code that needs Turkish formatting
// (PDF export, Excel import) uses an explicit CultureInfo("tr-TR") and is unaffected.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

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

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Any non-API route falls through to the SPA bundle in wwwroot (populated on publish).
app.MapFallbackToFile("index.html");

app.Run();
