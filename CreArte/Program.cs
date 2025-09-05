using CreArte.Data;
using CreArte.Services.Auditoria;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CreArteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultCreArteDB")));


builder.Services.AddSession(options =>
{
    // Tiempo de vida de la cookie de sesión (ajusta a tu gusto)
    options.IdleTimeout = TimeSpan.FromMinutes(8); // para sesiones más largas 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// Add services to the container.
builder.Services.AddControllersWithViews();

// Program.cs (minimal hosting) o Startup.ConfigureServices
builder.Services.AddHttpContextAccessor(); // necesario para CurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

//builder.Services
//    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Auth/Login";        // ajusta a tu ruta
//        options.AccessDeniedPath = "/Auth/Denied";
//        // Opcional: nombre de la cookie, expiración, etc.
//    });
// 3) Cookie Authentication (LO CLAVE para que User.Identity tenga datos)
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login";               // si no autenticado
        options.AccessDeniedPath = "/Login/Login";        // acceso denegado
        options.ExpireTimeSpan = TimeSpan.FromHours(8);   // duración cookie
        options.SlidingExpiration = true;                 // renovar con uso
        // options.Cookie.Name = "CreArte.Auth";           // opcional: nombre del cookie
    });

builder.Services.AddAuthorization();
// MVC
builder.Services.AddControllersWithViews();
// Razor Pages

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession(); // <-- IMPORTANTE para HttpContext.Session


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Login}/{id?}");

app.Run();
