using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CerescosDesarrolloCartera.Controllers
{
    // ================================================================
    // CONTROLADOR BASE
    // ================================================================
    //
    // Reúne lo que todos los controladores necesitan:
    //
    // - Abrir y cerrar la conexión a SQL.
    // - Capturar los errores de forma uniforme.
    // - Leer campos del reader sin repetir conversiones.
    // - Consultar y validar un usuario.
    //
    // ================================================================

    public abstract class ControladorBase : ControllerBase
    {
        protected readonly IConfiguration Config;
        protected readonly IWebHostEnvironment Entorno;

        protected ControladorBase(
            IConfiguration config,
            IWebHostEnvironment entorno)
        {
            Config = config;
            Entorno = entorno;
        }

        // ============================================================
        // EJECUCIÓN CON CONEXIÓN Y MANEJO DE ERRORES
        // ============================================================

        protected async Task<IActionResult> Ejecutar(
            string mensajeError,
            Func<SqlConnection, Task<IActionResult>> accion)
        {
            var cadena = Config.GetConnectionString("CerescosDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                return StatusCode(500, new
                {
                    mensaje = "No se encontró la conexión CerescosDB."
                });
            }

            try
            {
                using var conn = new SqlConnection(cadena);
                await conn.OpenAsync();

                return await accion(conn);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensaje = mensajeError,

                    // El detalle técnico solo se expone en desarrollo.
                    detalle = Entorno.IsDevelopment()
                        ? ex.Message
                        : null
                });
            }
        }

        // ============================================================
        // LECTURA DE CAMPOS
        // ============================================================

        protected static string Txt(SqlDataReader r, string c) =>
            r[c]?.ToString() ?? string.Empty;

        protected static string? TxtNulo(SqlDataReader r, string c) =>
            r[c] == DBNull.Value ? null : r[c].ToString();

        protected static int Num(SqlDataReader r, string c) =>
            Convert.ToInt32(r[c]);

        protected static int? NumNulo(SqlDataReader r, string c) =>
            r[c] == DBNull.Value ? null : Convert.ToInt32(r[c]);

        protected static DateTime Fec(SqlDataReader r, string c) =>
            Convert.ToDateTime(r[c]);

        protected static DateTime? FecNula(SqlDataReader r, string c) =>
            r[c] == DBNull.Value ? null : Convert.ToDateTime(r[c]);

        // ============================================================
        // ROLES
        // ============================================================

        protected static readonly string[] RolesJefe =
        {
            "Jefe de Depósitos",
            "Jefe de Cadenas"
        };

        protected static readonly string[] RolesCartera =
        {
            "Cartera",
            "Persona de cartera"
        };

        protected static bool EsRol(string rol, params string[] roles) =>
            roles.Any(r => string.Equals(
                r,
                rol,
                StringComparison.OrdinalIgnoreCase));

        // ============================================================
        // USUARIO DE BASE DE DATOS
        // ============================================================

        protected sealed record UsuarioDatos(
            int Id,
            string Nombre,
            string Apellido,
            string UsuarioNombre,
            string Correo,
            string Rol,
            bool Activo);

        protected static async Task<UsuarioDatos?> BuscarUsuario(
            SqlConnection conn,
            int usuarioId)
        {
            const string sql = @"
                SELECT Id, Nombre, Apellido, UsuarioNombre, Correo, Rol, Activo
                FROM dbo.Usuarios
                WHERE Id = @UsuarioId";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

            using var r = await cmd.ExecuteReaderAsync();

            return await r.ReadAsync()
                ? new UsuarioDatos(
                    Num(r, "Id"),
                    Txt(r, "Nombre"),
                    Txt(r, "Apellido"),
                    Txt(r, "UsuarioNombre"),
                    Txt(r, "Correo"),
                    Txt(r, "Rol"),
                    Convert.ToBoolean(r["Activo"]))
                : null;
        }
    }
}
