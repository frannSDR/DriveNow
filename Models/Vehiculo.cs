using System;
using System.Collections.Generic;
using System.Text;

namespace DriveNow.Models
{
    class Vehiculo
    {
        public string Patente { get; set; } = string.Empty; // Solo se permite del tipo 'AA 000 AA'
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Año { get; set; }
        public string Estado { get; set; } = "Disponible";

        public string MarcaModelo => $"{Marca} {Modelo}";

    }
}
