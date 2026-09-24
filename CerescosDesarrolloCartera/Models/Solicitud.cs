using System.ComponentModel.DataAnnotations;

namespace CerescosDesarrolloCartera.Models
{
    public class Solicitud
    {
        // ==========================================
        // IDENTIFICACIÓN
        // ==========================================

        public int Id { get; set; }

        [Display(Name = "Usuario creador")]
        public int UsuarioCreadorId { get; set; }


        // ==========================================
        // INFORMACIÓN DE LA SOLICITUD
        // ==========================================

        [Required(ErrorMessage = "Seleccione un canal")]
        [Display(Name = "Canal")]
        public string Canal { get; set; } = string.Empty;


        [Required(ErrorMessage = "Ingrese el nombre del cliente")]
        [Display(Name = "Nombre del cliente")]
        public string NombreCliente { get; set; } = string.Empty;


        [Display(Name = "Fecha de solicitud")]
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;


        [Required(ErrorMessage = "Seleccione al menos un tipo de solicitud")]
        [Display(Name = "Tipo de solicitud")]
        public string TipoSolicitud { get; set; } = string.Empty;


        [Required(ErrorMessage = "Especifique el detalle de la solicitud")]
        [Display(Name = "Detalle de la solicitud")]
        public string DetalleSolicitud { get; set; } = string.Empty;


        // ==========================================
        // APROBACIÓN DEL JEFE
        // ==========================================

        [Display(Name = "Estado del jefe")]
        public string EstadoJefe { get; set; } = "Pendiente";


        [Display(Name = "Jefe responsable")]
        public int? JefeUsuarioId { get; set; }


        [Display(Name = "Fecha de aprobación del jefe")]
        public DateTime? FechaAprobacionJefe { get; set; }


        [Display(Name = "Observación del jefe")]
        public string? ObservacionJefe { get; set; }


        // ==========================================
        // APROBACIÓN DE CARTERA
        // ==========================================

        [Display(Name = "Estado de Cartera")]
        public string EstadoCartera { get; set; } = "Pendiente";


        [Display(Name = "Usuario de Cartera")]
        public int? CarteraUsuarioId { get; set; }


        [Display(Name = "Fecha de aprobación de Cartera")]
        public DateTime? FechaAprobacionCartera { get; set; }


        [Display(Name = "Observación de Cartera")]
        public string? ObservacionCartera { get; set; }


        // ==========================================
        // ESTADO GENERAL
        // ==========================================

        [Display(Name = "Estado general")]
        public string EstadoGeneral { get; set; } = "Pendiente";


        // ==========================================
        // FECHA DE CREACIÓN
        // ==========================================

        [Display(Name = "Fecha de creación")]
        public DateTime FechaCreacion { get; set; }
    }
}