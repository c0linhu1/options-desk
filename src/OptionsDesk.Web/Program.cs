using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OptionsDesk.Data;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Services;

// Views render numbers into data attributes that TypeScript parses, so pin the culture.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services.AddDbContext<DeskDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Desk")));
builder.Services.Configure<DeskOptions>(builder.Configuration.GetSection("Desk"));
builder.Services.AddSingleton(builder.Configuration.GetSection("RiskLimits").Get<RiskLimits>() ?? new RiskLimits());
builder.Services.AddSingleton<IValuationClock, SystemValuationClock>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MarketDataService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<TradeBookingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<DeskDbContext>());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
