using DriveNow.Models;

namespace DriveNow.Services
{
    public interface IFacturaPdfService
    {
        string GenerarFacturaPdf(Alquiler alquiler, string rutaDestino);
    }
}
