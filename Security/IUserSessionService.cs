using System;
using DriveNow.Models;

namespace DriveNow.Security
{
    public interface IUserSessionService
    {
        Empleado? EmpleadoActual { get; }
        bool EstaAutenticado { get; }
        RolUsuario? RolActual { get; }

        void IniciarSesion(Empleado empleado);
        void CerrarSesion();

        event Action? SesionCambiada;
    }
}
