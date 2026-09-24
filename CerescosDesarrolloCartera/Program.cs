using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// CONTROLADORES
// ==========================================

builder.Services.AddControllers();


// ==========================================
// AUTENTICACION POR COOKIE
// ==========================================

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login.html";
        options.AccessDeniedPath = "/acceso-denegado.html";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        options.SlidingExpiration = true;

        // ----------------------------------
        // SEGURIDAD DE LA COOKIE
        // ----------------------------------

        // No es accesible desde JavaScript (evita robo por XSS).
        options.Cookie.HttpOnly = true;

        // Solo viaja por HTTPS.
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // No se envia desde otros sitios (evita CSRF).
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.Cookie.Name = "Cerescos.Sesion";

        // ----------------------------------
        // RESPUESTAS PARA LA API
        // ----------------------------------
        // Las paginas se redirigen al login,
        // pero las llamadas a /api deben responder
        // 401 / 403 para que el JavaScript lo detecte.
        // ----------------------------------

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);

            return Task.CompletedTask;
        };
    });


// ==========================================
// AUTORIZACION
// ==========================================

builder.Services.AddAuthorization();


// ==========================================
// PASSWORD HASHER
// ==========================================

builder.Services.AddScoped<
    Microsoft.AspNetCore.Identity.IPasswordHasher<
        CerescosDesarrolloCartera.Models.Usuario
    >,
    Microsoft.AspNetCore.Identity.PasswordHasher<
        CerescosDesarrolloCartera.Models.Usuario
    >
>();


// ==========================================
// SWAGGER
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();


var app = builder.Build();


// ==========================================
// SWAGGER (SOLO EN DESARROLLO)
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}


// ==========================================
// HTTPS
// ==========================================

app.UseHttpsRedirection();


// ==========================================
// AUTENTICACION Y AUTORIZACION
// ==========================================

app.UseAuthentication();

app.UseAuthorization();


// ==========================================
// PROTECCION DE LAS PAGINAS HTML
// ==========================================
//
// Las paginas de wwwroot son archivos estaticos:
// sin este filtro cualquiera podria abrir
// cartera.html o jefe.html escribiendo la URL.
//
// Paginas publicas: login.html y registro.html.
//
// ==========================================

var paginasProtegidas =
    new Dictionary<string, string[]>(
        StringComparer.OrdinalIgnoreCase)
    {
        // Sin roles = basta con estar autenticado.
        ["/index.html"] = Array.Empty<string>(),

        ["/trazabilidad.html"] = Array.Empty<string>(),

        ["/solicitud.html"] = new[]
        {
            "Creador"
        },

        ["/jefe.html"] = new[]
        {
            "Jefe de Depósitos",
            "Jefe de Cadenas"
        },

        ["/cartera.html"] = new[]
        {
            "Cartera",
            "Persona de cartera"
        }
    };


app.Use(async (context, next) =>
{
    var ruta =
        context.Request.Path.Value ?? string.Empty;

    if (paginasProtegidas.TryGetValue(
            ruta,
            out var rolesPermitidos))
    {
        // -----------------------------------
        // DEBE TENER SESION ACTIVA
        // -----------------------------------

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/login.html");

            return;
        }

        // -----------------------------------
        // DEBE TENER EL ROL CORRECTO
        // -----------------------------------

        if (rolesPermitidos.Length > 0)
        {
            var rolActual =
                context.User.FindFirst(ClaimTypes.Role)?.Value
                ?? string.Empty;

            if (!rolesPermitidos.Contains(
                    rolActual,
                    StringComparer.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/index.html");

                return;
            }
        }
    }

    await next();
});


// ==========================================
// ARCHIVOS DE WWWROOT
// ==========================================
//
// NO se usa UseDefaultFiles para que la raiz "/"
// no entregue index.html directamente.
//
// ==========================================

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = contexto =>
    {
        // ----------------------------------
        // NO GUARDAR EN CACHE DEL NAVEGADOR
        // ----------------------------------
        //
        // Sin esto, el navegador se queda con la version
        // vieja de un .html, .css o .js despues de un
        // cambio, y el usuario sigue viendo lo anterior
        // aunque el servidor ya tenga lo nuevo.
        //
        // Se conserva la validacion por ETag: si el archivo
        // no cambio, el servidor responde 304 y no lo
        // reenvia, asi que no se pierde rendimiento.
        //
        // ----------------------------------

        contexto.Context.Response.Headers["Cache-Control"] =
            "no-cache, must-revalidate";
    }
});


// ==========================================
// PAGINA DE INICIO
// ==========================================
//
// "/" lleva al login, o al panel si ya hay sesion.
//
// ==========================================

app.MapGet("/", (HttpContext context) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/index.html");
    }

    return Results.Redirect("/login.html");
});


// ==========================================
// CONTROLLERS
// ==========================================

app.MapControllers();


app.Run();
