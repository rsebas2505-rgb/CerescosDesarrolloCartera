using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CerescosDesarrolloCartera.Models;

namespace CerescosDesarrolloCartera.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControladorBase
    {
        private readonly PasswordHasher<Usuario> _passwordHasher = new();

        public AuthController(
            IConfiguration config,
            IWebHostEnvironment entorno)
            : base(config, entorno)
        {
        }

        // =========================================================
        // REGISTRAR USUARIO
        // =========================================================

        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] Usuario usuario)
        {
            if (usuario == null)
            {
                return BadRequest(new
                {
                    mensaje = "No se recibieron datos."
                });
            }

            if (string.IsNullOrWhiteSpace(usuario.Nombre) ||
                string.IsNullOrWhiteSpace(usuario.Apellido) ||
                string.IsNullOrWhiteSpace(usuario.Correo) ||
                string.IsNullOrWhiteSpace(usuario.UsuarioNombre) ||
                string.IsNullOrWhiteSpace(usuario.Password))
            {
                return BadRequest(new
                {
                    mensaje = "Todos los campos son obligatorios."
                });
            }

            usuario.Nombre = usuario.Nombre.Trim();
            usuario.Apellido = usuario.Apellido.Trim();
            usuario.Correo = usuario.Correo.Trim().ToLower();
            usuario.UsuarioNombre = usuario.UsuarioNombre.Trim();

            if (!usuario.Correo.EndsWith(
                    "@cerescos.com",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    mensaje = "El correo debe ser corporativo (@cerescos.com)."
                });
            }

            return await Ejecutar(
                "Error al registrar el usuario.",
                async conn =>
                {
                    // ------------------------------------------
                    // EL CORREO Y EL USUARIO NO SE PUEDEN REPETIR
                    // ------------------------------------------

                    const string comprobarSql = @"
                        SELECT COUNT(*)
                        FROM dbo.Usuarios
                        WHERE Correo = @Correo
                           OR UsuarioNombre = @UsuarioNombre";

                    using var comprobarCmd =
                        new SqlCommand(comprobarSql, conn);

                    comprobarCmd.Parameters.AddWithValue(
                        "@Correo", usuario.Correo);

                    comprobarCmd.Parameters.AddWithValue(
                        "@UsuarioNombre", usuario.UsuarioNombre);

                    object? existe =
                        await comprobarCmd.ExecuteScalarAsync();

                    if (existe != null &&
                        existe != DBNull.Value &&
                        Convert.ToInt32(existe) > 0)
                    {
                        return BadRequest(new
                        {
                            mensaje =
                                "El correo o el nombre de usuario ya está registrado."
                        });
                    }

                    // ------------------------------------------
                    // LA CONTRASEÑA SE GUARDA SIEMPRE CON HASH
                    // ------------------------------------------

                    const string insertarSql = @"
                        INSERT INTO dbo.Usuarios
                        (
                            Nombre, Apellido, Correo, UsuarioNombre,
                            PasswordHash, Rol, Activo, FechaRegistro
                        )
                        VALUES
                        (
                            @Nombre, @Apellido, @Correo, @UsuarioNombre,
                            @PasswordHash, 'Creador', 1, SYSDATETIME()
                        )";

                    using var insertarCmd =
                        new SqlCommand(insertarSql, conn);

                    insertarCmd.Parameters.AddWithValue(
                        "@Nombre", usuario.Nombre);

                    insertarCmd.Parameters.AddWithValue(
                        "@Apellido", usuario.Apellido);

                    insertarCmd.Parameters.AddWithValue(
                        "@Correo", usuario.Correo);

                    insertarCmd.Parameters.AddWithValue(
                        "@UsuarioNombre", usuario.UsuarioNombre);

                    insertarCmd.Parameters.AddWithValue(
                        "@PasswordHash",
                        _passwordHasher.HashPassword(
                            usuario,
                            usuario.Password));

                    await insertarCmd.ExecuteNonQueryAsync();

                    return Ok(new
                    {
                        mensaje = "Usuario registrado correctamente.",
                        rol = "Creador"
                    });
                });
        }

        // =========================================================
        // INICIAR SESIÓN
        // =========================================================

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest login)
        {
            if (login == null ||
                string.IsNullOrWhiteSpace(login.UsuarioNombre) ||
                string.IsNullOrWhiteSpace(login.Password))
            {
                return BadRequest(new
                {
                    mensaje = "Ingrese usuario y contraseña."
                });
            }

            return await Ejecutar(
                "Error al iniciar sesión.",
                async conn =>
                {
                    const string sql = @"
                        SELECT Id, Nombre, Apellido, Correo,
                               UsuarioNombre, PasswordHash, Rol, Activo
                        FROM dbo.Usuarios
                        WHERE UsuarioNombre = @UsuarioNombre";

                    using var cmd = new SqlCommand(sql, conn);

                    cmd.Parameters.AddWithValue(
                        "@UsuarioNombre",
                        login.UsuarioNombre.Trim());

                    Usuario usuario;

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (!await r.ReadAsync())
                        {
                            return Unauthorized(new
                            {
                                mensaje = "Usuario o contraseña incorrectos."
                            });
                        }

                        usuario = new Usuario
                        {
                            Id = Num(r, "Id"),
                            Nombre = Txt(r, "Nombre"),
                            Apellido = Txt(r, "Apellido"),
                            Correo = Txt(r, "Correo"),
                            UsuarioNombre = Txt(r, "UsuarioNombre"),
                            PasswordHash = Txt(r, "PasswordHash"),
                            Rol = Txt(r, "Rol"),
                            Activo = Convert.ToBoolean(r["Activo"])
                        };
                    }

                    if (!usuario.Activo)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "Este usuario se encuentra inactivo."
                        });
                    }

                    var resultado =
                        _passwordHasher.VerifyHashedPassword(
                            usuario,
                            usuario.PasswordHash,
                            login.Password);

                    if (resultado == PasswordVerificationResult.Failed)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "Usuario o contraseña incorrectos."
                        });
                    }

                    // ------------------------------------------
                    // CREAR LA SESIÓN (COOKIE DE AUTENTICACIÓN)
                    // ------------------------------------------
                    //
                    // A partir de aquí el servidor sabe quién es el
                    // usuario en cada petición. El navegador ya no
                    // necesita enviar el usuarioId, y nadie puede
                    // suplantar a otro cambiando un número en la URL.
                    //
                    // ------------------------------------------

                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                        new(ClaimTypes.Name, usuario.UsuarioNombre),
                        new(ClaimTypes.Role, usuario.Rol),
                        new(ClaimTypes.Email, usuario.Correo),
                        new("Nombre", usuario.Nombre),
                        new("Apellido", usuario.Apellido)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,

                        new ClaimsPrincipal(
                            new ClaimsIdentity(
                                claims,
                                CookieAuthenticationDefaults.AuthenticationScheme)),

                        new AuthenticationProperties
                        {
                            IsPersistent = false,

                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                        });

                    return Ok(new
                    {
                        mensaje = "Inicio de sesión correcto.",

                        usuario = new
                        {
                            usuario.Id,
                            usuario.Nombre,
                            usuario.Apellido,
                            usuario.Correo,
                            usuario.UsuarioNombre,
                            usuario.Rol
                        }
                    });
                });
        }

        // =========================================================
        // CONFIGURAR JEFE DE DEPÓSITOS
        // =========================================================
        //
        // Configuración inicial. Solo funciona en Development.
        //
        // =========================================================

        [HttpPost("configurar-jefe-depositos")]
        public async Task<IActionResult> ConfigurarJefeDepositos()
        {
            if (!Entorno.IsDevelopment())
            {
                return NotFound();
            }

            const string usuarioNombre = "Jefe Depositos";
            const string password = "CERESCOS2026+*";
            const string nombre = "Jefe";
            const string apellido = "Depositos";
            const string correo = "jefe.depositos@cerescos.com";
            const string rol = "Jefe de Depósitos";

            return await Ejecutar(
                "Error al configurar el Jefe de Depósitos.",
                async conn =>
                {
                    string passwordHash =
                        _passwordHasher.HashPassword(
                            new Usuario
                            {
                                Nombre = nombre,
                                Apellido = apellido,
                                Correo = correo,
                                UsuarioNombre = usuarioNombre,
                                Rol = rol,
                                Activo = true
                            },
                            password);

                    // Se intenta actualizar; si no existe, se crea.
                    const string actualizarSql = @"
                        UPDATE dbo.Usuarios
                        SET Nombre = @Nombre,
                            Apellido = @Apellido,
                            Correo = @Correo,
                            PasswordHash = @PasswordHash,
                            Rol = @Rol,
                            Activo = 1
                        WHERE UsuarioNombre = @UsuarioNombre";

                    const string insertarSql = @"
                        INSERT INTO dbo.Usuarios
                        (
                            Nombre, Apellido, Correo, UsuarioNombre,
                            PasswordHash, Rol, Activo, FechaRegistro
                        )
                        VALUES
                        (
                            @Nombre, @Apellido, @Correo, @UsuarioNombre,
                            @PasswordHash, @Rol, 1, SYSDATETIME()
                        )";

                    using var actualizarCmd =
                        new SqlCommand(actualizarSql, conn);

                    AgregarDatosJefe(actualizarCmd);

                    bool actualizado =
                        await actualizarCmd.ExecuteNonQueryAsync() > 0;

                    if (!actualizado)
                    {
                        using var insertarCmd =
                            new SqlCommand(insertarSql, conn);

                        AgregarDatosJefe(insertarCmd);

                        await insertarCmd.ExecuteNonQueryAsync();
                    }

                    return Ok(new
                    {
                        mensaje = actualizado
                            ? "El usuario Jefe de Depósitos fue actualizado correctamente."
                            : "Usuario Jefe de Depósitos creado correctamente.",

                        usuario = usuarioNombre,
                        rol,
                        estado = "Activo"
                    });

                    void AgregarDatosJefe(SqlCommand cmd)
                    {
                        cmd.Parameters.AddWithValue("@Nombre", nombre);
                        cmd.Parameters.AddWithValue("@Apellido", apellido);
                        cmd.Parameters.AddWithValue("@Correo", correo);
                        cmd.Parameters.AddWithValue("@UsuarioNombre", usuarioNombre);
                        cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        cmd.Parameters.AddWithValue("@Rol", rol);
                    }
                });
        }

        // =========================================================
        // USUARIO AUTENTICADO ACTUAL
        // =========================================================
        //
        // Las páginas llaman a este endpoint al cargar.
        // Si no hay sesión válida responde 401 y el
        // JavaScript redirige al login.
        //
        // =========================================================

        [Authorize]
        [HttpGet("actual")]
        public IActionResult Actual()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(id))
            {
                return Unauthorized(new
                {
                    mensaje = "No hay una sesión activa."
                });
            }

            return Ok(new
            {
                id = Convert.ToInt32(id),
                nombre = Reclamo("Nombre"),
                apellido = Reclamo("Apellido"),
                correo = Reclamo(ClaimTypes.Email),
                usuarioNombre = Reclamo(ClaimTypes.Name),
                rol = Reclamo(ClaimTypes.Role)
            });

            string Reclamo(string tipo) =>
                User.FindFirst(tipo)?.Value ?? string.Empty;
        }

        // =========================================================
        // CERRAR SESIÓN
        // =========================================================

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok(new
            {
                mensaje = "Sesión cerrada correctamente."
            });
        }
    }

    // =============================================================
    // MODELO PARA LOGIN
    // =============================================================

    public class LoginRequest
    {
        public string UsuarioNombre { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
