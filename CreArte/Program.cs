using CreArte.Controllers; // Para AppSettings en el mismo namespace (si lo dejaste allí)
using CreArte.Data;
using CreArte.Services.Auditoria;
using CreArte.Services.Mail;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

// 🔎 DIAGNÓSTICO CONFIG (antes de Build)
//Console.WriteLine("=== CONFIG DIAG (antes de Build) ===");
//Console.WriteLine($"ASPNETCORE_ENVIRONMENT = {builder.Environment.EnvironmentName}");
//Console.WriteLine($"Smtp:User (cfg) = '{builder.Configuration["Smtp:User"] ?? "<VACÍO>"}'");
//Console.WriteLine($"Smtp:From (cfg) = '{builder.Configuration["Smtp:From"] ?? "<VACÍO>"}'");


//var app = builder.Build();
var app = builder.Build();

//#if DEBUG
//{
//    var smtp = app.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;
//    if (string.IsNullOrWhiteSpace(smtp.User))
//    {
//        Console.WriteLine("[SMTP] Advertencia: Smtp.User está vacío. Revisa appsettings/user-secrets.");
//    }
//    if (string.IsNullOrWhiteSpace(smtp.From))
//    {
//        Console.WriteLine("[SMTP] Advertencia: Smtp.From está vacío. Revisa appsettings/user-secrets.");
//    }
//}
//#endif



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
