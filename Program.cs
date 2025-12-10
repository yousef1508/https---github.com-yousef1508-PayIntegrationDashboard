using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Services;
using PayrollIntegrationDashboard.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=payroll_integration.db"));

// App services
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddScoped<ValidationService>();

// GraphQL (HotChocolate)
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddFiltering()
    .AddSorting();

var app = builder.Build();

// Ensure DB/tables exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Integration}/{action=Index}/{id?}");

// GraphQL endpoint
app.MapGraphQL("/graphql");

app.Run();
