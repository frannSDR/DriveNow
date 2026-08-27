using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class LoginView : Window
    {
        // Usuarios iniciales en memoria para pruebas
        private static List<Usuario> usuariosRegistrados = new List<Usuario>
        {
            new Usuario { IdUsuario = 1, NombreCompleto = "Admin", Email = "admin@gmail.com", Password = "123ABC", Rol = "Administrador" },
            new Usuario { IdUsuario = 2, NombreCompleto = "Franco Montecino", Email = "frannm1703@gmail.com", Password = "toby91218", Rol = "Operador" }
        };

        // Variable global/estática para conocer el usuario autenticado en toda la app
        public static Usuario UsuarioActual { get; private set; }

        public LoginView()
        {
            InitializeComponent();
            txtLoginEmail.Text = "frannm1703@gmail.com";
            txtLoginPass.Password = "toby91218";
        }

        private void btnIniciarSesion_Click(object sender, RoutedEventArgs e)
        {
            string email = txtLoginEmail.Text.Trim();
            string pass = txtLoginPass.Password.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Complete su correo y contraseña.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var usuario = usuariosRegistrados.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && u.Password == pass);

            if (usuario != null)
            {
                UsuarioActual = usuario;

                // 1. Si el usuario logra loguearse, instanciamos la ventana princial del dashboard
                MainWindow main = new MainWindow();
                main.Show();

                // 2. Y cerramos la ventana de login
                this.Close();
            }
            else
            {
                MessageBox.Show("Correo o contraseña incorrectos.", "Error de Autenticación", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRegistrarUsuario_Click(object sender, RoutedEventArgs e)
        {

            string nombre = txtRegNombre.Text.Trim();
            string email = txtRegEmail.Text.Trim();
            string pass = txtRegPass.Password.Trim();

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Complete todos los campos para registrarse.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (usuariosRegistrados.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("El correo ingresado ya se encuentra registrado.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            usuariosRegistrados.Add(new Usuario
            {
                IdUsuario = usuariosRegistrados.Count + 1,
                NombreCompleto = nombre,
                Email = email,
                Password = pass,
                Rol = "Operador"
            });

            MessageBox.Show("Usuario registrado con éxito. Ya puedes iniciar sesión.", "Registro Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);

            // Volvemos a la vista de login
            pnlRegistro.Visibility = Visibility.Collapsed;
            pnlLogin.Visibility = Visibility.Visible;
            txtLoginEmail.Text = email;
            txtLoginPass.Password = string.Empty;
        }

        private void lnkIrARegistro_MouseDown(object sender, MouseButtonEventArgs e)
        {
            pnlLogin.Visibility = Visibility.Collapsed;
            pnlRegistro.Visibility = Visibility.Visible;
        }

        private void lnkIrALogin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            pnlRegistro.Visibility = Visibility.Collapsed;
            pnlLogin.Visibility = Visibility.Visible;
        }
    }
}