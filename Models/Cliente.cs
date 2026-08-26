using System;

namespace DriveNow.Models
{
    public class Cliente
    {
        public int IdCliente { get; set; }
        public string Dni { get; set; } = string.Empty; // Solo numeros / minimo 7 caracteres
        public string NombreCompleto { get; set; } = string.Empty; // Solo letras
        public string Telefono { get; set; } = string.Empty; // Minimo 10 carateres / Solo numeros
        public string Correo { get; set; } = string.Empty; // necesario verificar @
        public string NroLicencia { get; set; } = string.Empty; // Solo numeros
        public DateTime VencimientoLicencia { get; set; }
        public string EstadoIdentidad { get; set; } = "Validado"; // "Validado" o "No Validado"
        public string RutaFoto { get; set; } = string.Empty;

        // Propiedad calculada para saber si la licencia está vencida
        public bool LicenciaVencida => VencimientoLicencia.Date < DateTime.Now.Date;

        // Estado operativo para la tabla ("Activo" si está al día, "Inactivo" si está vencida)
        public string Estado => LicenciaVencida ? "Inactivo" : "Activo";
        public string VencimientoFormato => VencimientoLicencia.ToString("MM/yyyy");
    }
}
