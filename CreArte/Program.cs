using Microsoft.EntityFrameworkCore;
using CreArte.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CreArteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultCreArteDB")));


builder.Services.AddSession(options =>
{
    // Tiempo de vida de la cookie de sesión (ajusta a tu gusto)
    options.IdleTimeout = TimeSpan.FromMinutes(60); // para sesiones más largas 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// Add services to the container.
builder.Services.AddControllersWithViews();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseSession(); // <-- IMPORTANTE para HttpContext.Session


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
