using CreArte.Controllers; // Para AppSettings en el mismo namespace (si lo dejaste allí)
using CreArte.Data;
using CreArte.Services.Auditoria;
using CreArte.Services.Mail;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rotativa.AspNetCore;

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


// App (BaseUrl)
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

// =============================
// 4) Inyección de dependencias
// =============================
// SMTP
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<EnvioCorreoSMTP>();
builder.Services.AddSingleton<PlantillaEnvioCorreo>();
// Singleton: plantilla sin estado; si guardaras logs/contadores, usa Scoped
//builder.Services.AddScoped<CreArteDbContext>(); // para servicios que lo usen

// Program.cs (minimal hosting) o Startup.ConfigureServices
builder.Services.AddHttpContextAccessor(); // necesario para CurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

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

//var app = builder.Build();
var app = builder.Build();

// Configuración de Rotativa: le pasamos el WebRoot y la carpeta "Rotativa"
RotativaConfiguration.Setup(builder.Environment.WebRootPath, "Rotativa");

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
    pattern: "{controller=Login}/{action=Login}/{id?}"
    );

app.Run();
