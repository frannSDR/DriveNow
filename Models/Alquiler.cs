using System;

namespace DriveNow.Models
{
    public class Alquiler
    {
        public int IdAlquiler { get; set; }

        // NOTA: Este ccodigo de alquiler debe ser generado de manera automatica por algun algoritmo, partiendo de la base 'AAA-0000'
        public string CodigoAlquiler { get; set; } = string.Empty; // ejemplo: "ALQ-0912"   
        public string Cliente { get; set; } = string.Empty;
        public string DniCliente { get; set; } = string.Empty;
        public string Vehiculo { get; set; } = string.Empty;

        // NOTA: Solo se admiten patentes con la estructura argentina 'AA 000 AA'
        public string Patente { get; set; } = string.Empty; 

        public DateTime FechaEntregaPactada { get; set; }
        public DateTime FechaDevolucionPactada { get; set; }
        public DateTime? FechaEntregaReal { get; set; }
        public DateTime? FechaDevolucionReal { get; set; }

        public int CombustibleSalida { get; set; } // porcentaje de ejemplo: 100% / No puede ser una valor negativo ni superior a 100
        public int CombustibleEntrada { get; set; } // porcentaje de ejemplo: 10% / No puede ser un valor negativo
        public int KmSalida { get; set; } // No se admiten valores negativos
        public int KmEntrada { get; set; } // No se admiten valores negativos, ni puede ser menor al valor de salida

        public int HorasMora { get; set; }
        public decimal MontoMora { get; set; }
        public string DanioObservaciones { get; set; } = string.Empty;
        public string RutaEvidenciaDanio { get; set; } = string.Empty;

        public string Estado { get; set; } = "En Curso"; // "En Curso", "Finalizado"

        // Propiedades de formato para la tabla
        public string EntregaTexto => FechaEntregaReal?.ToString("dd/MM/yy HH:mm") ?? FechaEntregaPactada.ToString("dd/MM/yy HH:mm");
        public string DevolucionTexto => FechaDevolucionReal?.ToString("dd/MM/yy HH:mm") ?? FechaDevolucionPactada.ToString("dd/MM/yy HH:mm");
        public string KmTotalesTexto => Estado == "Finalizado" ? $"{Math.Max(0, KmEntrada - KmSalida)} km" : "En curso";
        public string MoraTexto => HorasMora > 0 ? $"+{HorasMora} hrs (${MontoMora:N0})" : "-";
    }
}