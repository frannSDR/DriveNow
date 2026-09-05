namespace DriveNow.Security
{
    public static class PasswordHelper
    {
        // Genera el hash seguro con Salt automático usando BCrypt
        public static string HashearPassword(string passwordPlano)
        {
            return BCrypt.Net.BCrypt.HashPassword(passwordPlano);
        }

        // Valida si la contraseña ingresada coincide con el hash guardado en la base de datos
        public static bool VerificarPassword(string passwordIngresado, string hashAlmacenado)
        {
            if (string.IsNullOrWhiteSpace(passwordIngresado) || string.IsNullOrWhiteSpace(hashAlmacenado))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(passwordIngresado, hashAlmacenado);
            }
            catch
            {
                // Si el formato del hash no es válido
                return false;
            }
        }
    }
}