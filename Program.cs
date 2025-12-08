using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.GraphQL;
using PayrollIntegrationDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Domain services
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddScoped<ValidationService>();
builder.Services.AddHostedService<BackgroundSyncService>();

// GraphQL (HotChocolate)
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddFiltering()
    .AddSorting();

var app = builder.Build();

// Ensure DB exists
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

// GraphQL endpoint
app.MapGraphQL("/graphql");

// MVC routing – dashboard as default page
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Integration}/{action=Index}/{id?}");

app.Run();
