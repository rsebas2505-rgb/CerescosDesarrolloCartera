namespace CerescosDesarrolloCartera.Models
{
    public class Registro
    {
        // Datos del usuario autenticado
        public int UsuarioId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Cedula { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;


        // Datos de la solicitud
        public string Canal { get; set; } = string.Empty;

        public string NombreCliente { get; set; } = string.Empty;

        public DateTime FechaSolicitud { get; set; }

        public List<string> TipoSolicitud { get; set; } = new();

        public string DetalleSolicitud { get; set; } = string.Empty;
    }
}
