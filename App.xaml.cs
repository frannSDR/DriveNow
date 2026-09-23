using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DriveNow.Data;
using DriveNow.Security;
using DriveNow.Services;

using QuestPDF.Infrastructure;

namespace DriveNow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configurar licencia comunitaria y fuentes de QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Contexto de Base de Datos
            services.AddDbContext<DriveNowDbContext>();

            // Sesión y Seguridad
            services.AddSingleton<IUserSessionService>(UserSessionService.Instance);

            // Capa de Servicios de Negocio
            services.AddTransient<IClienteService, ClienteService>();
            services.AddTransient<IVehiculoService, VehiculoService>();
            services.AddTransient<IReservaService, ReservaService>();
            services.AddTransient<IAlquilerService, AlquilerService>();
            services.AddTransient<IFacturaPdfService, FacturaPdfService>();
        }
    }
}
