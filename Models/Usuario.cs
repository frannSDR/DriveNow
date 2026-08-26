using System;
using System.Collections.Generic;
using System.Text;

namespace DriveNow.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public string NombreCompleto { get; set; } = string.Empty; // Solo letras
        public string Email { get; set; } = string.Empty; // verificar @
        public string Password { get; set; } = string.Empty; // usar algun metodo de hashing
        public string Rol { get; set; } = "Operador"; // "Administrador", "Operador"
    }
}
