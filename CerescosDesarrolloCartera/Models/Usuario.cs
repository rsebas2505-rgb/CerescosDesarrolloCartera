using System.ComponentModel.DataAnnotations.Schema;

namespace CerescosDesarrolloCartera.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Apellido { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;

        public string UsuarioNombre { get; set; } = string.Empty;

        // =====================================================
        // CONTRASEÑA
        // =====================================================
        // Esta propiedad se utiliza únicamente para recibir
        // la contraseña escrita en el formulario.
        //
        // NO se guarda directamente en SQL.
        // En SQL solamente guardaremos PasswordHash.
        // =====================================================

        [NotMapped]
        public string Password { get; set; } = string.Empty;

        // Contraseña encriptada/hash almacenada en SQL
        public string PasswordHash { get; set; } = string.Empty;

        // Rol del usuario
        public string Rol { get; set; } = "Creador";

        // Usuario activo/inactivo
        public bool Activo { get; set; } = true;

        // Fecha de creación
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}