using System;
using DriveNow.Models;

namespace DriveNow.Security
{
    public class UserSessionService : IUserSessionService
    {
        public static UserSessionService Instance { get; } = new UserSessionService();

        public Empleado? EmpleadoActual { get; private set; }

        public bool EstaAutenticado => EmpleadoActual != null;

        public RolUsuario? RolActual => EmpleadoActual != null ? (RolUsuario)EmpleadoActual.IdRol : null;

        public event Action? SesionCambiada;

        public void IniciarSesion(Empleado empleado)
        {
            EmpleadoActual = empleado ?? throw new ArgumentNullException(nameof(empleado));
            SesionCambiada?.Invoke();
        }

        public void CerrarSesion()
        {
            EmpleadoActual = null;
            SesionCambiada?.Invoke();
        }
    }

    /// <summary>
    /// Fachada estática conveniente para acceder a la sesión activa desde cualquier componente.
    /// </summary>
    public static class UserSession
    {
        public static IUserSessionService Current => UserSessionService.Instance;
    }
}
