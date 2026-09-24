using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CerescosDesarrolloCartera.Models;

namespace CerescosDesarrolloCartera.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SolicitudesController : ControladorBase
    {
        public SolicitudesController(
            IConfiguration config,
            IWebHostEnvironment entorno)
            : base(config, entorno)
        {
        }

        // ============================================================
        // USUARIO DE LA SESIÓN
        // ============================================================
        //
        // El id SIEMPRE se toma de la cookie de autenticación.
        //
        // Nunca se confía en el usuarioId que envía el navegador,
        // porque cualquiera podría cambiarlo en la URL y consultar
        // o aprobar solicitudes de otra persona.
        //
        // ============================================================

        private int UsuarioActualId() =>
            int.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out int id)
                ? id
                : 0;

        // ============================================================
        // DOCUMENTOS ADJUNTOS
        // ============================================================
        //
        // Los archivos NO se guardan en wwwroot: si estuvieran ahi,
        // cualquiera podria descargarlos escribiendo la URL, sin
        // iniciar sesion. Se guardan fuera y solo se entregan por
        // el endpoint de abajo, que valida rol y permisos.
        //
        // ============================================================

        private const long TamanoMaximoArchivo = 20 * 1024 * 1024;

        private string CarpetaAdjuntos() =>
            Path.Combine(Entorno.ContentRootPath, "Adjuntos");

        private void BorrarAdjunto(string? nombreAlmacenado)
        {
            if (string.IsNullOrWhiteSpace(nombreAlmacenado))
            {
                return;
            }

            try
            {
                var ruta = Path.Combine(CarpetaAdjuntos(), nombreAlmacenado);

                if (System.IO.File.Exists(ruta))
                {
                    System.IO.File.Delete(ruta);
                }
            }
            catch
            {
                // Si no se puede borrar, no vale la pena tumbar la peticion.
            }
        }

        // ============================================================
        // QUIEN PUEDE VER UNA SOLICITUD
        // ============================================================
        //
        // Creador : solo las suyas.
        // Jefe    : solo las asignadas a el.
        // Cartera : las que el jefe ya proceso.
        //
        // ============================================================

        private static bool PuedeVerSolicitud(
            UsuarioDatos usuario,
            int usuarioCreadorId,
            int? jefeUsuarioId,
            string estadoJefe)
        {
            if (EsRol(usuario.Rol, "Creador"))
            {
                return usuarioCreadorId == usuario.Id;
            }

            if (EsRol(usuario.Rol, RolesJefe))
            {
                return jefeUsuarioId == usuario.Id;
            }

            if (EsRol(usuario.Rol, RolesCartera))
            {
                return estadoJefe.Equals(
                           "Aprobado",
                           StringComparison.OrdinalIgnoreCase) ||
                       estadoJefe.Equals(
                           "Rechazado",
                           StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        // ============================================================
        // CONSULTA BASE DE SOLICITUDES
        // ============================================================

        private const string SelectSolicitudes = @"
            SELECT
                s.*,

                creador.Nombre        AS CreadorNombre,
                creador.Apellido      AS CreadorApellido,
                creador.UsuarioNombre AS CreadorUsuario,
                creador.Correo        AS CreadorCorreo,

                jefe.Nombre           AS JefeNombre,
                jefe.Apellido         AS JefeApellido,
                jefe.UsuarioNombre    AS JefeUsuario,
                jefe.Correo           AS JefeCorreo,

                cartera.Nombre        AS CarteraNombre,
                cartera.Apellido      AS CarteraApellido,
                cartera.UsuarioNombre AS CarteraUsuario,
                cartera.Correo        AS CarteraCorreo

            FROM dbo.Solicitudes s

            INNER JOIN dbo.Usuarios creador
                ON creador.Id = s.UsuarioCreadorId

            LEFT JOIN dbo.Usuarios jefe
                ON jefe.Id = s.JefeUsuarioId

            LEFT JOIN dbo.Usuarios cartera
                ON cartera.Id = s.CarteraUsuarioId";

        private static object Mapear(SqlDataReader r) => new
        {
            id = Num(r, "Id"),
            usuarioCreadorId = Num(r, "UsuarioCreadorId"),
            canal = Txt(r, "Canal"),
            nombreCliente = Txt(r, "NombreCliente"),
            fechaSolicitud = Fec(r, "FechaSolicitud"),
            tipoSolicitud = Txt(r, "TipoSolicitud"),
            detalleSolicitud = Txt(r, "DetalleSolicitud"),

            estadoJefe = Txt(r, "EstadoJefe"),
            jefeUsuarioId = NumNulo(r, "JefeUsuarioId"),
            fechaAprobacionJefe = FecNula(r, "FechaAprobacionJefe"),
            observacionJefe = TxtNulo(r, "ObservacionJefe"),

            estadoCartera = Txt(r, "EstadoCartera"),
            carteraUsuarioId = NumNulo(r, "CarteraUsuarioId"),
            fechaAprobacionCartera = FecNula(r, "FechaAprobacionCartera"),
            observacionCartera = TxtNulo(r, "ObservacionCartera"),

            estadoGeneral = Txt(r, "EstadoGeneral"),
            fechaCreacion = Fec(r, "FechaCreacion"),

            // Documento adjunto (puede no existir).
            archivoNombre = TxtNulo(r, "ArchivoNombre"),
            archivoTipo = TxtNulo(r, "ArchivoTipo"),
            archivoTamano = r["ArchivoTamano"] == DBNull.Value
                ? (long?)null
                : Convert.ToInt64(r["ArchivoTamano"]),
            archivoFecha = FecNula(r, "ArchivoFecha"),
            tieneArchivo = r["ArchivoAlmacenado"] != DBNull.Value,

            creadorNombre = Txt(r, "CreadorNombre"),
            creadorApellido = Txt(r, "CreadorApellido"),
            creadorUsuario = Txt(r, "CreadorUsuario"),

            jefeNombre = Txt(r, "JefeNombre"),
            jefeApellido = Txt(r, "JefeApellido"),
            jefeUsuario = Txt(r, "JefeUsuario")
        };

        private static async Task<List<object>> ListarSolicitudes(
            SqlConnection conn,
            string filtro,
            params SqlParameter[] parametros)
        {
            using var cmd = new SqlCommand(
                $"{SelectSolicitudes} WHERE {filtro} ORDER BY s.FechaCreacion DESC",
                conn);

            cmd.Parameters.AddRange(parametros);

            using var r = await cmd.ExecuteReaderAsync();

            var solicitudes = new List<object>();

            while (await r.ReadAsync())
            {
                solicitudes.Add(Mapear(r));
            }

            return solicitudes;
        }

        // ============================================================
        // CREAR SOLICITUD
        // ============================================================

        // Recibe multipart/form-data porque la solicitud puede
        // traer un documento adjunto. El archivo es opcional.
        [HttpPost]
        [RequestSizeLimit(TamanoMaximoArchivo + 1048576)]
        public async Task<IActionResult> Crear(
            [FromForm] Solicitud solicitud,
            IFormFile? documento)
        {
            if (solicitud == null)
            {
                return BadRequest(new
                {
                    mensaje = "No se recibieron datos de la solicitud."
                });
            }

            // El creador es el usuario de la sesión,
            // no el que venga en el cuerpo de la petición.
            solicitud.UsuarioCreadorId = UsuarioActualId();

            if (solicitud.UsuarioCreadorId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario creador."
                });
            }

            if (string.IsNullOrWhiteSpace(solicitud.Canal))
            {
                return BadRequest(new
                {
                    mensaje = "Debe seleccionar un canal."
                });
            }

            if (string.IsNullOrWhiteSpace(solicitud.NombreCliente))
            {
                return BadRequest(new
                {
                    mensaje = "Debe ingresar el nombre del cliente."
                });
            }

            if (string.IsNullOrWhiteSpace(solicitud.TipoSolicitud))
            {
                return BadRequest(new
                {
                    mensaje = "Debe seleccionar al menos un tipo de solicitud."
                });
            }

            if (string.IsNullOrWhiteSpace(solicitud.DetalleSolicitud))
            {
                return BadRequest(new
                {
                    mensaje = "Debe ingresar el detalle de la solicitud."
                });
            }

            solicitud.Canal = solicitud.Canal.Trim().ToLower();

            bool esDepositos =
                solicitud.Canal == "depositos" ||
                solicitud.Canal == "depósitos";

            if (!esDepositos && solicitud.Canal != "cadenas")
            {
                return BadRequest(new
                {
                    mensaje = "El canal seleccionado no es válido. Debe ser Depósitos o Cadenas."
                });
            }

            // ----------------------------------------------------
            // DOCUMENTO ADJUNTO
            // ----------------------------------------------------
            //
            // Se acepta cualquier tipo de archivo. Lo unico que se
            // limita es el tamaño, para no llenar el disco.
            //
            // El nombre original NUNCA se usa como nombre en disco:
            // se guarda con un identificador propio para evitar que
            // alguien manipule la ruta con nombres como "..\".
            //
            // ----------------------------------------------------

            if (documento != null && documento.Length > TamanoMaximoArchivo)
            {
                return BadRequest(new
                {
                    mensaje =
                        $"El documento supera el tamaño máximo permitido ({TamanoMaximoArchivo / 1048576} MB)."
                });
            }

            return await Ejecutar(
                "Error al crear la solicitud.",
                async conn =>
                {
                    var creador =
                        await BuscarUsuario(conn, solicitud.UsuarioCreadorId);

                    if (creador == null)
                    {
                        return BadRequest(new
                        {
                            mensaje = "El usuario creador no existe."
                        });
                    }

                    if (!creador.Activo)
                    {
                        return BadRequest(new
                        {
                            mensaje = "El usuario se encuentra inactivo."
                        });
                    }

                    if (!EsRol(creador.Rol, "Creador"))
                    {
                        return Forbid();
                    }

                    // --------------------------------------------
                    // JEFE QUE APRUEBA SEGÚN EL CANAL
                    // --------------------------------------------

                    string rolJefe = esDepositos
                        ? "Jefe de Depósitos"
                        : "Jefe de Cadenas";

                    const string buscarJefeSql = @"
                        SELECT TOP 1 Id
                        FROM dbo.Usuarios
                        WHERE Rol = @RolJefe AND Activo = 1
                        ORDER BY Id";

                    using var buscarJefeCmd =
                        new SqlCommand(buscarJefeSql, conn);

                    buscarJefeCmd.Parameters.AddWithValue("@RolJefe", rolJefe);

                    object? resultadoJefe =
                        await buscarJefeCmd.ExecuteScalarAsync();

                    if (resultadoJefe == null || resultadoJefe == DBNull.Value)
                    {
                        return StatusCode(500, new
                        {
                            mensaje =
                                $"No existe un usuario activo con el rol '{rolJefe}'."
                        });
                    }

                    string canalGuardar = esDepositos
                        ? "Depósitos"
                        : "Cadenas";

                    DateTime fechaSolicitud =
                        solicitud.FechaSolicitud == default
                            ? DateTime.Today
                            : solicitud.FechaSolicitud.Date;

                    // --------------------------------------------
                    // GUARDAR EL ARCHIVO EN DISCO
                    // --------------------------------------------
                    // Se guarda antes de insertar: si algo falla al
                    // copiarlo, no queda una solicitud apuntando a
                    // un documento inexistente.
                    // --------------------------------------------

                    string? nombreAlmacenado = null;

                    if (documento != null && documento.Length > 0)
                    {
                        var carpeta = CarpetaAdjuntos();

                        Directory.CreateDirectory(carpeta);

                        var extension = Path.GetExtension(documento.FileName);

                        if (extension != null && extension.Length > 20)
                        {
                            extension = extension.Substring(0, 20);
                        }

                        foreach (var c in Path.GetInvalidFileNameChars())
                        {
                            extension = extension?.Replace(c.ToString(), string.Empty);
                        }

                        nombreAlmacenado =
                            Guid.NewGuid().ToString("N") + extension;

                        using var destino =
                            System.IO.File.Create(
                                Path.Combine(carpeta, nombreAlmacenado));

                        await documento.CopyToAsync(destino);
                    }

                    const string insertarSql = @"
                        INSERT INTO dbo.Solicitudes
                        (
                            UsuarioCreadorId, Canal, NombreCliente, FechaSolicitud,
                            TipoSolicitud, DetalleSolicitud, EstadoJefe, JefeUsuarioId,
                            EstadoCartera, EstadoGeneral, FechaCreacion,
                            ArchivoNombre, ArchivoAlmacenado, ArchivoTipo,
                            ArchivoTamano, ArchivoFecha
                        )
                        VALUES
                        (
                            @UsuarioCreadorId, @Canal, @NombreCliente, @FechaSolicitud,
                            @TipoSolicitud, @DetalleSolicitud, 'Pendiente', @JefeUsuarioId,
                            'Pendiente', 'Pendiente', SYSDATETIME(),
                            @ArchivoNombre, @ArchivoAlmacenado, @ArchivoTipo,
                            @ArchivoTamano, @ArchivoFecha
                        );

                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using var insertarCmd =
                        new SqlCommand(insertarSql, conn);

                    insertarCmd.Parameters.AddWithValue(
                        "@UsuarioCreadorId", solicitud.UsuarioCreadorId);

                    insertarCmd.Parameters.AddWithValue(
                        "@Canal", canalGuardar);

                    insertarCmd.Parameters.AddWithValue(
                        "@NombreCliente", solicitud.NombreCliente.Trim());

                    insertarCmd.Parameters.AddWithValue(
                        "@FechaSolicitud", fechaSolicitud);

                    insertarCmd.Parameters.AddWithValue(
                        "@TipoSolicitud", solicitud.TipoSolicitud.Trim());

                    insertarCmd.Parameters.AddWithValue(
                        "@DetalleSolicitud", solicitud.DetalleSolicitud.Trim());

                    insertarCmd.Parameters.AddWithValue(
                        "@JefeUsuarioId", Convert.ToInt32(resultadoJefe));

                    insertarCmd.Parameters.AddWithValue(
                        "@ArchivoNombre",
                        nombreAlmacenado == null
                            ? DBNull.Value
                            : Path.GetFileName(documento!.FileName));

                    insertarCmd.Parameters.AddWithValue(
                        "@ArchivoAlmacenado",
                        (object?)nombreAlmacenado ?? DBNull.Value);

                    insertarCmd.Parameters.AddWithValue(
                        "@ArchivoTipo",
                        nombreAlmacenado == null
                            ? DBNull.Value
                            : (object?)documento!.ContentType ?? DBNull.Value);

                    insertarCmd.Parameters.AddWithValue(
                        "@ArchivoTamano",
                        nombreAlmacenado == null
                            ? DBNull.Value
                            : documento!.Length);

                    insertarCmd.Parameters.AddWithValue(
                        "@ArchivoFecha",
                        nombreAlmacenado == null
                            ? DBNull.Value
                            : DateTime.Now);

                    object? nuevoId;

                    try
                    {
                        nuevoId = await insertarCmd.ExecuteScalarAsync();
                    }
                    catch
                    {
                        // Si no se pudo guardar la solicitud, se borra
                        // el archivo para no dejar basura en el disco.
                        BorrarAdjunto(nombreAlmacenado);

                        throw;
                    }

                    return Ok(new
                    {
                        mensaje = "Solicitud creada correctamente.",

                        solicitudId =
                            nuevoId == null || nuevoId == DBNull.Value
                                ? 0
                                : Convert.ToInt32(nuevoId),

                        canal = canalGuardar,
                        jefeAsignado = rolJefe,
                        estadoJefe = "Pendiente",
                        estadoCartera = "Pendiente",
                        estadoGeneral = "Pendiente",

                        archivoNombre = nombreAlmacenado == null
                            ? null
                            : Path.GetFileName(documento!.FileName)
                    });
                });
        }

        // ============================================================
        // CONSULTAS POR ROL
        // ============================================================

        private async Task<IActionResult> Consultar(
            string mensajeError,
            string[] rolesPermitidos,
            Func<int, (string filtro, SqlParameter[] parametros)> construirFiltro,
            bool incluirUsuario = true)
        {
            int usuarioId = UsuarioActualId();

            if (usuarioId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario."
                });
            }

            return await Ejecutar(
                mensajeError,
                async conn =>
                {
                    var usuario = await BuscarUsuario(conn, usuarioId);

                    if (usuario == null)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "El usuario no existe."
                        });
                    }

                    if (!usuario.Activo)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "El usuario se encuentra inactivo."
                        });
                    }

                    if (!EsRol(usuario.Rol, rolesPermitidos))
                    {
                        return Forbid();
                    }

                    var (filtro, parametros) = construirFiltro(usuarioId);

                    var solicitudes =
                        await ListarSolicitudes(conn, filtro, parametros);

                    if (!incluirUsuario)
                    {
                        return Ok(solicitudes);
                    }

                    return Ok(new
                    {
                        usuario = new
                        {
                            id = usuario.Id,
                            nombre = usuario.Nombre,
                            apellido = usuario.Apellido,
                            usuarioNombre = usuario.UsuarioNombre,
                            correo = usuario.Correo,
                            rol = usuario.Rol
                        },

                        solicitudes
                    });
                });
        }

        // Solicitudes pendientes del jefe.
        [HttpGet("jefe")]
        public Task<IActionResult> ObtenerSolicitudesJefe() =>
            Consultar(
                "Error al consultar las solicitudes.",
                RolesJefe,
                id => (
                    "s.JefeUsuarioId = @UsuarioId AND s.EstadoJefe = 'Pendiente'",
                    new[] { new SqlParameter("@UsuarioId", id) }));

        // Historial completo del jefe.
        [HttpGet("jefe/historial")]
        public Task<IActionResult> ObtenerHistorialJefe() =>
            Consultar(
                "Error al consultar el historial.",
                RolesJefe,
                id => (
                    "s.JefeUsuarioId = @UsuarioId",
                    new[] { new SqlParameter("@UsuarioId", id) }));

        // Solicitudes del creador (responde un arreglo simple).
        [HttpGet("creador")]
        public Task<IActionResult> ObtenerSolicitudesCreador() =>
            Consultar(
                "Error al consultar las solicitudes del creador.",
                new[] { "Creador" },
                id => (
                    "s.UsuarioCreadorId = @UsuarioId",
                    new[] { new SqlParameter("@UsuarioId", id) }),
                incluirUsuario: false);

        // ============================================================
        // SOLICITUDES DE CARTERA
        // ============================================================
        //
        // Cartera visualiza todo lo que el jefe ya procesó:
        // aprobadas pendientes de Cartera, aprobadas por Cartera,
        // rechazadas por Cartera y rechazadas por el jefe.
        //
        // No muestra las que siguen pendientes del jefe.
        //
        // ============================================================

        [HttpGet("cartera")]
        public Task<IActionResult> ObtenerSolicitudesCartera() =>
            Consultar(
                "Error al consultar las solicitudes de Cartera.",
                RolesCartera,
                _ => (
                    "s.EstadoJefe IN ('Aprobado', 'Rechazado')",
                    Array.Empty<SqlParameter>()));

        // ============================================================
        // DECISIÓN DEL JEFE
        // ============================================================

        private async Task<IActionResult> DecidirJefe(
            int id,
            RespuestaJefe respuesta,
            bool aprobar)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "Solicitud inválida."
                });
            }

            if (respuesta == null)
            {
                return BadRequest(new
                {
                    mensaje = "No se recibieron datos."
                });
            }

            // Quien aprueba o rechaza es el usuario de la sesión.
            respuesta.UsuarioId = UsuarioActualId();

            if (respuesta.UsuarioId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario."
                });
            }

            string estadoJefeNuevo = aprobar ? "Aprobado" : "Rechazado";

            string estadoCarteraNuevo = aprobar ? "Pendiente" : "Rechazado";

            string estadoGeneralNuevo = aprobar
                ? "En revisión de Cartera"
                : "Rechazado";

            return await Ejecutar(
                aprobar
                    ? "Error al aprobar la solicitud."
                    : "Error al rechazar la solicitud.",
                async conn =>
                {
                    var usuario =
                        await BuscarUsuario(conn, respuesta.UsuarioId);

                    if (usuario == null ||
                        !usuario.Activo ||
                        !EsRol(usuario.Rol, RolesJefe))
                    {
                        return Forbid();
                    }

                    const string validarSql = @"
                        SELECT JefeUsuarioId, EstadoJefe
                        FROM dbo.Solicitudes
                        WHERE Id = @SolicitudId";

                    using var validarCmd =
                        new SqlCommand(validarSql, conn);

                    validarCmd.Parameters.AddWithValue("@SolicitudId", id);

                    int? jefeUsuarioId;
                    string estadoJefe;

                    using (var r = await validarCmd.ExecuteReaderAsync())
                    {
                        if (!await r.ReadAsync())
                        {
                            return NotFound(new
                            {
                                mensaje = "La solicitud no existe."
                            });
                        }

                        jefeUsuarioId = NumNulo(r, "JefeUsuarioId");
                        estadoJefe = Txt(r, "EstadoJefe");
                    }

                    if (!jefeUsuarioId.HasValue ||
                        jefeUsuarioId.Value != respuesta.UsuarioId)
                    {
                        return Forbid();
                    }

                    if (!estadoJefe.Equals(
                            "Pendiente",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            mensaje = "Esta solicitud ya fue procesada."
                        });
                    }

                    const string actualizarSql = @"
                        UPDATE dbo.Solicitudes
                        SET
                            EstadoJefe = @EstadoJefe,
                            FechaAprobacionJefe = SYSDATETIME(),
                            ObservacionJefe = @Observacion,
                            EstadoCartera = @EstadoCartera,
                            EstadoGeneral = @EstadoGeneral
                        WHERE Id = @SolicitudId
                          AND JefeUsuarioId = @UsuarioId
                          AND EstadoJefe = 'Pendiente'";

                    using var actualizarCmd =
                        new SqlCommand(actualizarSql, conn);

                    actualizarCmd.Parameters.AddWithValue(
                        "@SolicitudId", id);

                    actualizarCmd.Parameters.AddWithValue(
                        "@UsuarioId", respuesta.UsuarioId);

                    actualizarCmd.Parameters.AddWithValue(
                        "@EstadoJefe", estadoJefeNuevo);

                    actualizarCmd.Parameters.AddWithValue(
                        "@EstadoCartera", estadoCarteraNuevo);

                    actualizarCmd.Parameters.AddWithValue(
                        "@EstadoGeneral", estadoGeneralNuevo);

                    actualizarCmd.Parameters.AddWithValue(
                        "@Observacion",
                        string.IsNullOrWhiteSpace(respuesta.Observacion)
                            ? DBNull.Value
                            : respuesta.Observacion.Trim());

                    if (await actualizarCmd.ExecuteNonQueryAsync() == 0)
                    {
                        return BadRequest(new
                        {
                            mensaje = aprobar
                                ? "No fue posible aprobar la solicitud."
                                : "No fue posible rechazar la solicitud."
                        });
                    }

                    return Ok(new
                    {
                        mensaje = aprobar
                            ? "Solicitud aprobada correctamente."
                            : "Solicitud rechazada correctamente.",

                        solicitudId = id,
                        estadoJefe = estadoJefeNuevo,
                        estadoCartera = estadoCarteraNuevo,
                        estadoGeneral = estadoGeneralNuevo
                    });
                });
        }

        [HttpPut("{id}/aprobar-jefe")]
        public Task<IActionResult> AprobarPorJefe(
            int id,
            [FromBody] RespuestaJefe respuesta) =>
            DecidirJefe(id, respuesta, aprobar: true);

        [HttpPut("{id}/rechazar-jefe")]
        public Task<IActionResult> RechazarPorJefe(
            int id,
            [FromBody] RespuestaJefe respuesta) =>
            DecidirJefe(id, respuesta, aprobar: false);

        // ============================================================
        // DECISIÓN DE CARTERA
        // ============================================================

        private async Task<IActionResult> DecidirCartera(
            int id,
            RespuestaCartera respuesta,
            bool aprobar)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "Solicitud inválida."
                });
            }

            if (respuesta == null)
            {
                return BadRequest(new
                {
                    mensaje = "No se recibieron datos."
                });
            }

            // Quien aprueba o rechaza es el usuario de la sesión.
            respuesta.UsuarioId = UsuarioActualId();

            if (respuesta.UsuarioId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario de Cartera."
                });
            }

            if (!aprobar &&
                string.IsNullOrWhiteSpace(respuesta.Observacion))
            {
                return BadRequest(new
                {
                    mensaje =
                        "Debe ingresar una observación para rechazar la solicitud."
                });
            }

            string estadoNuevo = aprobar ? "Aprobado" : "Rechazado";

            return await Ejecutar(
                aprobar
                    ? "Error al aprobar la solicitud desde Cartera."
                    : "Error al rechazar la solicitud desde Cartera.",
                async conn =>
                {
                    var usuario =
                        await BuscarUsuario(conn, respuesta.UsuarioId);

                    if (usuario == null ||
                        !usuario.Activo ||
                        !EsRol(usuario.Rol, RolesCartera))
                    {
                        return Forbid();
                    }

                    const string validarSql = @"
                        SELECT EstadoJefe, EstadoCartera
                        FROM dbo.Solicitudes
                        WHERE Id = @SolicitudId";

                    using var validarCmd =
                        new SqlCommand(validarSql, conn);

                    validarCmd.Parameters.AddWithValue("@SolicitudId", id);

                    string estadoJefe;
                    string estadoCartera;

                    using (var r = await validarCmd.ExecuteReaderAsync())
                    {
                        if (!await r.ReadAsync())
                        {
                            return NotFound(new
                            {
                                mensaje = "La solicitud no existe."
                            });
                        }

                        estadoJefe = Txt(r, "EstadoJefe");
                        estadoCartera = Txt(r, "EstadoCartera");
                    }

                    if (!estadoJefe.Equals(
                            "Aprobado",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            mensaje =
                                "La solicitud todavía no ha sido aprobada por el jefe."
                        });
                    }

                    if (!estadoCartera.Equals(
                            "Pendiente",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            mensaje =
                                "Esta solicitud ya fue procesada por Cartera."
                        });
                    }

                    const string actualizarSql = @"
                        UPDATE dbo.Solicitudes
                        SET
                            EstadoCartera = @Estado,
                            CarteraUsuarioId = @UsuarioId,
                            FechaAprobacionCartera = SYSDATETIME(),
                            ObservacionCartera = @Observacion,
                            EstadoGeneral = @Estado
                        WHERE Id = @SolicitudId
                          AND EstadoJefe = 'Aprobado'
                          AND EstadoCartera = 'Pendiente'";

                    using var actualizarCmd =
                        new SqlCommand(actualizarSql, conn);

                    actualizarCmd.Parameters.AddWithValue(
                        "@SolicitudId", id);

                    actualizarCmd.Parameters.AddWithValue(
                        "@UsuarioId", respuesta.UsuarioId);

                    actualizarCmd.Parameters.AddWithValue(
                        "@Estado", estadoNuevo);

                    actualizarCmd.Parameters.AddWithValue(
                        "@Observacion",
                        string.IsNullOrWhiteSpace(respuesta.Observacion)
                            ? DBNull.Value
                            : respuesta.Observacion.Trim());

                    if (await actualizarCmd.ExecuteNonQueryAsync() == 0)
                    {
                        return BadRequest(new
                        {
                            mensaje = aprobar
                                ? "No fue posible aprobar la solicitud."
                                : "No fue posible rechazar la solicitud."
                        });
                    }

                    return Ok(new
                    {
                        mensaje = aprobar
                            ? "Solicitud aprobada por Cartera correctamente."
                            : "Solicitud rechazada por Cartera correctamente.",

                        solicitudId = id,
                        estadoJefe = "Aprobado",
                        estadoCartera = estadoNuevo,
                        estadoGeneral = estadoNuevo
                    });
                });
        }

        [HttpPut("{id}/aprobar-cartera")]
        public Task<IActionResult> AprobarPorCartera(
            int id,
            [FromBody] RespuestaCartera respuesta) =>
            DecidirCartera(id, respuesta, aprobar: true);

        [HttpPut("{id}/rechazar-cartera")]
        public Task<IActionResult> RechazarPorCartera(
            int id,
            [FromBody] RespuestaCartera respuesta) =>
            DecidirCartera(id, respuesta, aprobar: false);

        // ============================================================
        // DESCARGAR EL DOCUMENTO ADJUNTO
        // ============================================================
        //
        // Lo pueden consultar el creador de la solicitud, el jefe
        // asignado y Cartera, con las mismas reglas de la
        // trazabilidad.
        //
        // El archivo se entrega SIEMPRE como descarga y con tipo
        // generico: asi un archivo HTML o SVG subido por alguien
        // no puede ejecutarse dentro del sitio.
        //
        // ============================================================

        [HttpGet("{id}/adjunto")]
        public async Task<IActionResult> DescargarAdjunto(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "Solicitud inválida."
                });
            }

            int usuarioId = UsuarioActualId();

            if (usuarioId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario."
                });
            }

            return await Ejecutar(
                "Error al descargar el documento.",
                async conn =>
                {
                    var usuario = await BuscarUsuario(conn, usuarioId);

                    if (usuario == null || !usuario.Activo)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "El usuario no existe o está inactivo."
                        });
                    }

                    const string sql = @"
                        SELECT UsuarioCreadorId, JefeUsuarioId, EstadoJefe,
                               ArchivoNombre, ArchivoAlmacenado, ArchivoTipo
                        FROM dbo.Solicitudes
                        WHERE Id = @SolicitudId";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@SolicitudId", id);

                    int usuarioCreadorId;
                    int? jefeUsuarioId;
                    string estadoJefe;
                    string? nombre;
                    string? almacenado;

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (!await r.ReadAsync())
                        {
                            return NotFound(new
                            {
                                mensaje = "La solicitud no existe."
                            });
                        }

                        usuarioCreadorId = Num(r, "UsuarioCreadorId");
                        jefeUsuarioId = NumNulo(r, "JefeUsuarioId");
                        estadoJefe = Txt(r, "EstadoJefe");
                        nombre = TxtNulo(r, "ArchivoNombre");
                        almacenado = TxtNulo(r, "ArchivoAlmacenado");
                    }

                    if (!PuedeVerSolicitud(
                            usuario,
                            usuarioCreadorId,
                            jefeUsuarioId,
                            estadoJefe))
                    {
                        return Forbid();
                    }

                    if (string.IsNullOrWhiteSpace(almacenado))
                    {
                        return NotFound(new
                        {
                            mensaje = "Esta solicitud no tiene documento adjunto."
                        });
                    }

                    // Solo el nombre del archivo, nunca una ruta que
                    // venga de la base, para que no pueda apuntar
                    // fuera de la carpeta de adjuntos.
                    var ruta = Path.Combine(
                        CarpetaAdjuntos(),
                        Path.GetFileName(almacenado));

                    if (!System.IO.File.Exists(ruta))
                    {
                        return NotFound(new
                        {
                            mensaje = "El documento ya no se encuentra almacenado."
                        });
                    }

                    var contenido =
                        await System.IO.File.ReadAllBytesAsync(ruta);

                    return File(
                        contenido,
                        "application/octet-stream",
                        string.IsNullOrWhiteSpace(nombre)
                            ? $"solicitud-{id}"
                            : nombre);
                });
        }

        // ============================================================
        // TRAZABILIDAD DE UNA SOLICITUD
        // ============================================================

        [HttpGet("trazabilidad/{id}")]
        public async Task<IActionResult> ObtenerTrazabilidad(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "Solicitud inválida."
                });
            }

            int usuarioId = UsuarioActualId();

            if (usuarioId <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "No se identificó el usuario."
                });
            }

            return await Ejecutar(
                "Error al consultar la trazabilidad.",
                async conn =>
                {
                    var usuario = await BuscarUsuario(conn, usuarioId);

                    if (usuario == null)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "El usuario no existe."
                        });
                    }

                    if (!usuario.Activo)
                    {
                        return Unauthorized(new
                        {
                            mensaje = "El usuario se encuentra inactivo."
                        });
                    }

                    using var cmd = new SqlCommand(
                        $"{SelectSolicitudes} WHERE s.Id = @SolicitudId",
                        conn);

                    cmd.Parameters.AddWithValue("@SolicitudId", id);

                    using var r = await cmd.ExecuteReaderAsync();

                    if (!await r.ReadAsync())
                    {
                        return NotFound(new
                        {
                            mensaje = "La solicitud no existe."
                        });
                    }

                    int usuarioCreadorId = Num(r, "UsuarioCreadorId");
                    int? jefeUsuarioId = NumNulo(r, "JefeUsuarioId");
                    string estadoJefe = Txt(r, "EstadoJefe");
                    string estadoGeneral = Txt(r, "EstadoGeneral");

                    if (!PuedeVerSolicitud(
                            usuario,
                            usuarioCreadorId,
                            jefeUsuarioId,
                            estadoJefe))
                    {
                        return Forbid();
                    }

                    return Ok(new
                    {
                        solicitud = new
                        {
                            id = Num(r, "Id"),
                            canal = Txt(r, "Canal"),
                            nombreCliente = Txt(r, "NombreCliente"),
                            fechaSolicitud = Fec(r, "FechaSolicitud"),
                            tipoSolicitud = Txt(r, "TipoSolicitud"),
                            detalleSolicitud = Txt(r, "DetalleSolicitud"),
                            fechaCreacion = Fec(r, "FechaCreacion"),
                            estadoGeneral
                        },

                        creador = new
                        {
                            id = usuarioCreadorId,
                            nombre = $"{Txt(r, "CreadorNombre")} {Txt(r, "CreadorApellido")}".Trim(),
                            usuario = Txt(r, "CreadorUsuario"),
                            correo = Txt(r, "CreadorCorreo")
                        },

                        jefe = new
                        {
                            id = jefeUsuarioId,
                            nombre = $"{Txt(r, "JefeNombre")} {Txt(r, "JefeApellido")}".Trim(),
                            usuario = Txt(r, "JefeUsuario"),
                            correo = Txt(r, "JefeCorreo")
                        },

                        aprobacionJefe = new
                        {
                            estado = estadoJefe,
                            fecha = FecNula(r, "FechaAprobacionJefe"),
                            observacion = TxtNulo(r, "ObservacionJefe")
                        },

                        cartera = new
                        {
                            id = NumNulo(r, "CarteraUsuarioId"),
                            nombre = $"{Txt(r, "CarteraNombre")} {Txt(r, "CarteraApellido")}".Trim(),
                            usuario = Txt(r, "CarteraUsuario"),
                            correo = Txt(r, "CarteraCorreo")
                        },

                        aprobacionCartera = new
                        {
                            estado = Txt(r, "EstadoCartera"),
                            fecha = FecNula(r, "FechaAprobacionCartera"),
                            observacion = TxtNulo(r, "ObservacionCartera")
                        },

                        // El documento adjunto tambien hace parte
                        // de la trazabilidad de la solicitud.
                        documento = new
                        {
                            existe = r["ArchivoAlmacenado"] != DBNull.Value,
                            nombre = TxtNulo(r, "ArchivoNombre"),
                            tipo = TxtNulo(r, "ArchivoTipo"),
                            tamano = r["ArchivoTamano"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(r["ArchivoTamano"]),
                            fecha = FecNula(r, "ArchivoFecha")
                        },

                        estadoGeneral
                    });
                });
        }
    }

    // ================================================================
    // RESPUESTA DEL JEFE
    // ================================================================

    public class RespuestaJefe
    {
        public int UsuarioId { get; set; }

        public string? Observacion { get; set; }
    }

    // ================================================================
    // RESPUESTA DE CARTERA
    // ================================================================

    public class RespuestaCartera
    {
        public int UsuarioId { get; set; }

        public string? Observacion { get; set; }
    }
}
