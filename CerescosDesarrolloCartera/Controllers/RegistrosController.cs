using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CerescosDesarrolloCartera.Models;

namespace CerescosDesarrolloCartera.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrosController : ControllerBase
    {
        private readonly IConfiguration _config;

        public RegistrosController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Registro registro)
        {
            if (string.IsNullOrWhiteSpace(registro.Nombre) ||
                string.IsNullOrWhiteSpace(registro.Cedula) ||
                string.IsNullOrWhiteSpace(registro.Correo))
            {
                return BadRequest(new
                {
                    mensaje = "Faltan datos obligatorios."
                });
            }

            if (!registro.Correo.Trim().EndsWith("@cerescos.com",
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    mensaje = "El correo debe ser corporativo (@cerescos.com)."
                });
            }

            var connectionString = _config.GetConnectionString("CerescosDB");

            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = @"
                    INSERT INTO dbo.Registros
                        (Nombre, Cedula, Correo, Estado, FechaSolicitud)
                    VALUES
                        (@Nombre, @Cedula, @Correo, 'Pendiente', SYSDATETIME())";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Nombre", registro.Nombre.Trim());
                cmd.Parameters.AddWithValue("@Cedula", registro.Cedula.Trim());
                cmd.Parameters.AddWithValue("@Correo", registro.Correo.Trim());

                await cmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    mensaje = "Solicitud registrada y enviada para aprobación."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensaje = "Error al guardar la solicitud.",
                    detalle = ex.Message
                });
            }
        }
    }
}