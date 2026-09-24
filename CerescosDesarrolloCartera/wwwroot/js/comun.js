/* =========================================================
   CERESCOS — SESIÓN Y UTILIDADES COMPARTIDAS
   =========================================================

   Este archivo lo usan todas las páginas internas.

   Evita repetir en cada HTML el control de sesión,
   el cierre de sesión y el escape de HTML.

========================================================= */

/* =========================================================
   USUARIO AUTENTICADO
   =========================================================

   Devuelve el usuario de la sesión o null.

   El usuario NO se lee de localStorage: se consulta al
   servidor, que lo identifica por la cookie. Así nadie
   puede cambiar su rol ni su id desde el navegador.

   Si no hay sesión válida redirige al login.

========================================================= */

async function obtenerUsuarioSesion() {
    try {
        const respuesta = await fetch("/api/auth/actual", {
            method: "GET",
            credentials: "same-origin",
        });

        if (!respuesta.ok) {
            window.location.href = "login.html";

            return null;
        }

        const datos = await respuesta.json();

        if (!datos) {
            window.location.href = "login.html";

            return null;
        }

        return datos;
    } catch (error) {
        console.error("Error verificando la sesión:", error);

        window.location.href = "login.html";

        return null;
    }
}

/* =========================================================
   SESIÓN EXPIRADA
   =========================================================

   Se usa al consultar la API: si el servidor responde 401
   la sesión caducó y se vuelve al login.

========================================================= */

function sesionExpirada(respuesta) {
    if (respuesta.status === 401) {
        window.location.href = "login.html";

        return true;
    }

    return false;
}

/* =========================================================
   CERRAR SESIÓN
   =========================================================

   Avisa al servidor para que destruya la cookie de sesión
   y luego limpia cualquier dato antiguo del navegador.

========================================================= */

async function cerrarSesion() {
    try {
        await fetch("/api/auth/logout", {
            method: "POST",
            credentials: "same-origin",
        });
    } catch (error) {
        console.log("No fue posible notificar el cierre de sesión.");
    }

    localStorage.removeItem("usuarioCerescos");

    window.location.href = "login.html";
}

/* =========================================================
   SEGURIDAD HTML
   =========================================================

   Escapa el texto antes de insertarlo en la página,
   para que un dato guardado no pueda inyectar código.

========================================================= */

function escapeHtml(valor) {
    return String(valor ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

/* =========================================================
   DOCUMENTO ADJUNTO
   =========================================================

   La solicitud puede traer un archivo. El servidor solo lo
   entrega a quien tiene permiso y siempre como descarga.

========================================================= */

function tieneAdjunto(solicitud) {
    return Boolean(
        solicitud.tieneArchivo ??
            solicitud.TieneArchivo ??
            solicitud.archivoNombre ??
            solicitud.ArchivoNombre,
    );
}

function nombreAdjunto(solicitud) {
    return solicitud.archivoNombre ?? solicitud.ArchivoNombre ?? "documento";
}

/* Devuelve el enlace de descarga, o cadena vacía si no hay archivo.
   El estilo "icono" muestra solo el clip, para las tablas. */

function enlaceAdjunto(id, solicitud, estilo) {
    if (!tieneAdjunto(solicitud)) {
        return "";
    }

    const nombre = escapeHtml(nombreAdjunto(solicitud));

    const esIcono = estilo === "icono";

    return `<a class="${esIcono ? "btn-icono-tabla" : "btn-adjunto"}" href="/api/Solicitudes/${Number(id)}/adjunto" title="Descargar ${nombre}" onclick="event.stopPropagation()">${esIcono ? "📎" : "📎 " + nombre}</a>`;
}

/* =========================================================
   TRAZABILIDAD RESUMIDA
   =========================================================

   Muestra las tres etapas de la solicitud con su estado y
   su fecha: creación, aprobación del jefe y revisión de
   Cartera. La usan la pantalla de Cartera y la del jefe.

========================================================= */

function formatearFechaHora(valor) {
    if (!valor) {
        return null;
    }

    const fecha = new Date(valor);

    if (isNaN(fecha.getTime())) {
        return String(valor);
    }

    return fecha.toLocaleString("es-CO", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });
}

/* Fecha SIN hora.

   En la base, FechaSolicitud es de tipo DATE: solo guarda el
   día, no la hora. Al formatearla con hora siempre aparecía
   "12:00 a. m.", que no es un dato real sino el relleno del
   navegador. Los campos que sí son DATETIME2 (fecha de
   creación, aprobaciones y carga del archivo) siguen
   mostrando la hora. */

function formatearSoloFecha(valor, vacio) {
    if (!valor) {
        return vacio || "Sin fecha";
    }

    const fecha = new Date(valor);

    if (isNaN(fecha.getTime())) {
        return String(valor);
    }

    return fecha.toLocaleDateString("es-CO", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });
}

function estadoEtapa(valor) {
    const texto = String(valor ?? "Pendiente")
        .trim()
        .toLowerCase();

    if (texto === "aprobado") {
        return { texto: "Aprobado", clase: "etapa-aprobado" };
    }

    if (texto === "rechazado") {
        return { texto: "Rechazado", clase: "etapa-rechazado" };
    }

    return { texto: "Pendiente", clase: "etapa-pendiente" };
}

function lineaTiempoHTML(solicitud) {
    const id = solicitud.id ?? solicitud.Id;

    const creacion = formatearFechaHora(solicitud.fechaCreacion ?? solicitud.FechaCreacion);

    const jefe = estadoEtapa(solicitud.estadoJefe ?? solicitud.EstadoJefe);

    const fechaJefe = formatearFechaHora(
        solicitud.fechaAprobacionJefe ?? solicitud.FechaAprobacionJefe,
    );

    const jefeRechazo = jefe.texto === "Rechazado";

    /* Si el jefe rechazó, la etapa de Cartera nunca ocurre:
       mostrarla como "Rechazado" confundiría, porque Cartera
       no intervino. */

    const cartera = jefeRechazo
        ? { texto: "No aplica", clase: "etapa-anulado" }
        : estadoEtapa(solicitud.estadoCartera ?? solicitud.EstadoCartera);

    const fechaCartera = jefeRechazo
        ? null
        : formatearFechaHora(
              solicitud.fechaAprobacionCartera ?? solicitud.FechaAprobacionCartera,
          );

    const general = solicitud.estadoGeneral ?? solicitud.EstadoGeneral ?? "Pendiente";

    function etapa(titulo, estado, fecha, nota) {
        return `
            <li class="etapa ${estado.clase}">
                <div class="etapa-encabezado">
                    <span class="etapa-titulo">${escapeHtml(titulo)}</span>
                    <span class="etapa-estado">${escapeHtml(estado.texto)}</span>
                </div>
                <div class="etapa-fecha">${escapeHtml(fecha || nota || "Sin fecha registrada")}</div>
            </li>
        `;
    }

    return `
        <div class="linea-tiempo-solicitud">
            <div class="linea-tiempo-titulo">Trazabilidad de la solicitud</div>

            <ul class="etapas-solicitud">
                ${etapa("1. Creación", { texto: "Registrada", clase: "etapa-creada" }, creacion)}
                ${etapa("2. Aprobación del jefe", jefe, fechaJefe, "Pendiente de revisión")}
                ${etapa(
                    "3. Revisión de Cartera",
                    cartera,
                    fechaCartera,
                    jefeRechazo ? "El jefe rechazó la solicitud" : "Pendiente de revisión",
                )}
            </ul>

            <div class="linea-tiempo-pie">
                <span class="resultado-final">
                    Resultado final: <strong>${escapeHtml(general)}</strong>
                </span>

                <a class="enlace-trazabilidad" href="trazabilidad.html?id=${Number(id)}">
                    Ver trazabilidad completa
                </a>
            </div>
        </div>
    `;
}

/* =========================================================
   BOTÓN DE CERRAR SESIÓN
   =========================================================

   Se conecta solo si el botón no trae su propio onclick,
   para no ejecutar el cierre dos veces.

========================================================= */

document.addEventListener("DOMContentLoaded", function () {
    const boton = document.getElementById("btnCerrar");

    if (boton && !boton.getAttribute("onclick")) {
        boton.addEventListener("click", cerrarSesion);
    }
});
