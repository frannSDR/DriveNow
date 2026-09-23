using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace DriveNow.Views
{
    public class CategoriaFinancieraDto
    {
        public string Categoria { get; set; } = string.Empty;
        public int CantidadAlquileres { get; set; }
        public decimal TotalRecaudado { get; set; }
    }

    public class ContratoLiquidadoDto
    {
        public string CodigoAlquiler { get; set; } = string.Empty;
        public string CodigoReserva { get; set; } = string.Empty;
        public string Operador { get; set; } = string.Empty;

        // Cliente
        public string Cliente { get; set; } = string.Empty;
        public string DniCliente { get; set; } = string.Empty;
        public string TelefonoCliente { get; set; } = string.Empty;
        public string EmailCliente { get; set; } = string.Empty;

        // Vehículo
        public string Vehiculo { get; set; } = string.Empty;
        public string Patente { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal TarifaDiariaCategoria { get; set; }

        // Fechas y Tiempos
        public DateTime? FechaPactadaRetiro { get; set; }
        public DateTime? FechaPactadaDevolucion { get; set; }
        public DateTime FechaEntregaReal { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public int DiasPactados { get; set; }
        public double DiasReales { get; set; }
        public string EstadoRetorno => MontoMora > 0 ? "Con Demora" : "En Término";

        // Telemetría & Inspección
        public int KilometrajeSalida { get; set; }
        public int? KilometrajeEntrada { get; set; }
        public int KmRecorridos => KilometrajeEntrada.HasValue ? Math.Max(0, KilometrajeEntrada.Value - KilometrajeSalida) : 0;
        public decimal CombustibleSalida { get; set; }
        public decimal? CombustibleEntrada { get; set; }
        public int CantidadDanos { get; set; }
        public string DanosTexto => CantidadDanos > 0 ? $"{CantidadDanos} daño(s)" : "Sin daños";

        // Financiero
        public decimal CostoBase { get; set; }
        public decimal MontoMora { get; set; }
        public decimal TotalCobrado => CostoBase + MontoMora;
    }

    public partial class ReportesView : UserControl
    {
        private List<ContratoLiquidadoDto> _contratosLiquidados = new();

        public ReportesView()
        {
            InitializeComponent();
            // Por defecto, muestra el mes actual completo
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
            Loaded += async (s, e) => await CargarMetricasFinancierasAsync();
        }

        private void TabVista_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || pnlGraficos == null || pnlTablas == null) return;

            if (rbVistaGraficos.IsChecked == true)
            {
                pnlGraficos.Visibility = Visibility.Visible;
                pnlTablas.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlGraficos.Visibility = Visibility.Collapsed;
                pnlTablas.Visibility = Visibility.Visible;
            }
        }

        private async void btnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            await CargarMetricasFinancierasAsync();
        }

        private async void FiltroFechas_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await CargarMetricasFinancierasAsync();
            }
        }

        private void AplicarTemaOscuroBase(CartesianChart chart)
        {
            chart.Background = System.Windows.Media.Brushes.Transparent;
            chart.TooltipBackgroundPaint = new SolidColorPaint(new SKColor(15, 23, 42, 245)); // Slate 900
            chart.TooltipTextPaint = new SolidColorPaint(SKColor.Parse("#F8FAFC"));             // Slate 50
            chart.TooltipTextSize = 12;
            chart.LegendTextPaint = new SolidColorPaint(SKColor.Parse("#CBD5E1"));              // Slate 300
            chart.LegendTextSize = 11;
        }

        private void AplicarTemaOscuroBase(PieChart chart)
        {
            chart.Background = System.Windows.Media.Brushes.Transparent;
            chart.TooltipBackgroundPaint = new SolidColorPaint(new SKColor(15, 23, 42, 245));
            chart.TooltipTextPaint = new SolidColorPaint(SKColor.Parse("#F8FAFC"));
            chart.TooltipTextSize = 12;
            chart.LegendTextPaint = new SolidColorPaint(SKColor.Parse("#CBD5E1"));
            chart.LegendTextSize = 11;
        }

        private async Task CargarMetricasFinancierasAsync()
        {
            if (!dpDesde.SelectedDate.HasValue || !dpHasta.SelectedDate.HasValue) return;

            DateTime fechaInicio = dpDesde.SelectedDate.Value.Date;
            DateTime fechaFin = dpHasta.SelectedDate.Value.Date.AddDays(1).AddTicks(-1);

            try
            {
                using var context = new DriveNowDbContext();

                // Obtenemos los contratos finalizados en el período con toda su jerarquía
                var contratos = await context.Alquileres
                    .Include(a => a.Danos)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Empleado)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo!)
                            .ThenInclude(v => v.Categoria)
                    .Where(a => a.FechaEntregaDevolucion.HasValue &&
                                a.FechaEntregaDevolucion.Value >= fechaInicio &&
                                a.FechaEntregaDevolucion.Value <= fechaFin)
                    .ToListAsync();

                // 1. Cálculos de KPIs financieros generales
                decimal totalBase = contratos.Sum(a => a.CostoTotal);
                decimal totalMora = contratos.Sum(a => a.MontoMora);
                decimal facturacionTotal = totalBase + totalMora;
                int cantidadContratos = contratos.Count;
                decimal ticketPromedio = cantidadContratos > 0 ? facturacionTotal / cantidadContratos : 0;

                lblIngresosTotales.Text = facturacionTotal.ToString("C");
                lblIngresosBase.Text = totalBase.ToString("C");
                lblIngresosMora.Text = totalMora.ToString("C");
                lblTicketPromedio.Text = ticketPromedio.ToString("C");
                lblTotalContratos.Text = $"{cantidadContratos} contrato(s) liquidado(s)";

                // 2. Desglose por Categoría
                var categoriasDb = await context.Categorias.ToListAsync();
                var agrupadoCat = categoriasDb.Select(cat =>
                {
                    var contratosCat = contratos.Where(c => c.Reserva?.Vehiculo?.IdCategoria == cat.IdCategoria).ToList();
                    return new CategoriaFinancieraDto
                    {
                        Categoria = cat.Nombre,
                        CantidadAlquileres = contratosCat.Count,
                        TotalRecaudado = contratosCat.Sum(c => c.CostoTotal + c.MontoMora)
                    };
                }).OrderByDescending(dto => dto.TotalRecaudado).ToList();

                dgvRendimientoCat.ItemsSource = agrupadoCat;

                // 3. Detalle de Contratos para la tabla de auditoría y exportación CSV
                _contratosLiquidados = contratos.Select(a =>
                {
                    var res = a.Reserva;
                    var cli = res?.Cliente;
                    var veh = res?.Vehiculo;
                    var cat = veh?.Categoria;
                    var emp = res?.Empleado;

                    double diasEfectivos = 1.0;
                    if (a.FechaEntregaDevolucion.HasValue && a.FechaEntregaReal != default)
                    {
                        diasEfectivos = Math.Max(1.0, (a.FechaEntregaDevolucion.Value - a.FechaEntregaReal).TotalDays);
                    }
                    else if (res != null)
                    {
                        diasEfectivos = Math.Max(1.0, (double)res.TotalDias);
                    }

                    return new ContratoLiquidadoDto
                    {
                        CodigoAlquiler = $"ALQ-{a.IdAlquiler:D4}",
                        CodigoReserva = res != null ? $"RES-{res.IdReserva:D4}" : "-",
                        Operador = emp?.NombreCompleto ?? "Administración",

                        Cliente = cli?.NombreCompleto ?? "N/A",
                        DniCliente = cli?.Dni ?? "-",
                        TelefonoCliente = !string.IsNullOrWhiteSpace(cli?.Telefono) ? cli.Telefono : "-",
                        EmailCliente = !string.IsNullOrWhiteSpace(cli?.Correo) ? cli.Correo : "-",

                        Vehiculo = veh != null ? $"{veh.Marca} {veh.Modelo}" : "N/A",
                        Patente = veh?.Patente ?? "-",
                        Categoria = cat?.Nombre ?? "General",
                        TarifaDiariaCategoria = cat?.TarifaDiaria ?? 0,

                        FechaPactadaRetiro = res?.FechaDesde,
                        FechaPactadaDevolucion = res?.FechaHasta,
                        FechaEntregaReal = a.FechaEntregaReal,
                        FechaDevolucion = a.FechaEntregaDevolucion,
                        DiasPactados = res?.TotalDias ?? 1,
                        DiasReales = Math.Round(diasEfectivos, 1),

                        KilometrajeSalida = a.KilometrajeSalida,
                        KilometrajeEntrada = a.KilometrajeEntrada,
                        CombustibleSalida = a.CombustibleSalida,
                        CombustibleEntrada = a.CombustibleEntrada,
                        CantidadDanos = a.Danos?.Count ?? 0,

                        CostoBase = a.CostoTotal,
                        MontoMora = a.MontoMora
                    };
                }).ToList();

                dgvContratosCerrados.ItemsSource = _contratosLiquidados;

                // -------------------------------------------------------------
                // 4. INFERENCIA ESTADÍSTICA Y PROBABILIDADES
                // -------------------------------------------------------------
                // A) Probabilidad empírica de retorno con mora P(Mora)
                int contratosConMora = contratos.Count(c => c.MontoMora > 0);
                double probMora = cantidadContratos > 0 ? ((double)contratosConMora / cantidadContratos) * 100.0 : 0.0;
                lblProbMora.Text = $"{probMora:F1}%";
                lblDetalleMora.Text = $"{contratosConMora} de {cantidadContratos} contratos con mora";

                // B) Probabilidad de siniestros / daños reportados P(Daño | Retorno)
                int contratosConDanos = contratos.Count(c => c.Danos != null && c.Danos.Count > 0);
                double probDanos = cantidadContratos > 0 ? ((double)contratosConDanos / cantidadContratos) * 100.0 : 0.0;
                lblProbDanos.Text = $"{probDanos:F1}%";
                lblDetalleDanos.Text = $"{contratosConDanos} contratos con peritaje de daños";

                // C) Duración media de los contratos (x̄) y desviación estándar muestral (s)
                var duraciones = contratos.Select(c =>
                {
                    if (c.FechaEntregaDevolucion.HasValue && c.FechaEntregaReal != default)
                    {
                        double d = (c.FechaEntregaDevolucion.Value.Date - c.FechaEntregaReal.Date).TotalDays;
                        return Math.Max(1.0, d);
                    }
                    return (double)(c.Reserva?.TotalDias ?? 1);
                }).ToList();

                double mediaDias = duraciones.Count > 0 ? duraciones.Average() : 0.0;
                double desvioDias = 0.0;
                if (duraciones.Count > 1)
                {
                    double sumaCuadrados = duraciones.Sum(d => Math.Pow(d - mediaDias, 2));
                    desvioDias = Math.Sqrt(sumaCuadrados / (duraciones.Count - 1));
                }
                lblMediaDias.Text = $"{mediaDias:F1} días";
                lblDesvioDias.Text = $"Desv. estándar s = ±{desvioDias:F1} d";

                // D) Categoría Modal (Mo): la categoría de mayor frecuencia de contratación
                var catMasAlquilada = agrupadoCat.OrderByDescending(c => c.CantidadAlquileres).FirstOrDefault();
                if (catMasAlquilada != null && catMasAlquilada.CantidadAlquileres > 0 && cantidadContratos > 0)
                {
                    double pctModa = ((double)catMasAlquilada.CantidadAlquileres / cantidadContratos) * 100.0;
                    lblModaCategoria.Text = catMasAlquilada.Categoria;
                    lblDetalleModa.Text = $"{catMasAlquilada.CantidadAlquileres} alquileres ({pctModa:F0}% freq. relativa)";
                }
                else
                {
                    lblModaCategoria.Text = "Sin datos";
                    lblDetalleModa.Text = "No hay registros en el período";
                }

                // -------------------------------------------------------------
                // 5. GRÁFICO 1: EVOLUCIÓN TEMPORAL DE INGRESOS (Tema Oscuro Slate)
                // -------------------------------------------------------------
                AplicarTemaOscuroBase(chartEvolucion);

                var ingresosPorDia = contratos
                    .GroupBy(c => c.FechaEntregaDevolucion!.Value.Date)
                    .ToDictionary(g => g.Key, g => (double)g.Sum(c => c.CostoTotal + c.MontoMora));

                List<string> etiquetasFechas = new();
                List<double> valoresSerie = new();

                int totalDias = (fechaFin.Date - fechaInicio.Date).Days + 1;
                if (totalDias > 0 && totalDias <= 45)
                {
                    // Rango acotado: generamos día por día para observar la serie completa
                    for (int i = 0; i < totalDias; i++)
                    {
                        var dia = fechaInicio.Date.AddDays(i);
                        etiquetasFechas.Add(dia.ToString("dd/MM"));
                        valoresSerie.Add(ingresosPorDia.TryGetValue(dia, out double val) ? val : 0.0);
                    }
                }
                else
                {
                    // Rango extendido: mostramos solo los días con actividad ordenada cronológicamente
                    foreach (var kvp in ingresosPorDia.OrderBy(k => k.Key))
                    {
                        etiquetasFechas.Add(kvp.Key.ToString("dd/MM"));
                        valoresSerie.Add(kvp.Value);
                    }
                }

                if (valoresSerie.Count == 0)
                {
                    etiquetasFechas.Add(DateTime.Today.ToString("dd/MM"));
                    valoresSerie.Add(0.0);
                }

                chartEvolucion.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = valoresSerie.ToArray(),
                        Name = "Facturación Diaria ($)",
                        Stroke = new SolidColorPaint(SKColor.Parse("#38BDF8")) { StrokeThickness = 3.5f },
                        Fill = new SolidColorPaint(new SKColor(56, 189, 248, 25)), // Resplandor cian suave translúcido
                        GeometrySize = 8,
                        GeometryStroke = new SolidColorPaint(SKColor.Parse("#38BDF8")) { StrokeThickness = 2.5f },
                        GeometryFill = new SolidColorPaint(SKColor.Parse("#0F172A")), // Núcleo oscuro brillante
                        YToolTipLabelFormatter = pt => $"Facturación: ${pt.Model:N2}"
                    }
                };

                chartEvolucion.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = etiquetasFechas,
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                        TextSize = 11,
                        SeparatorsPaint = new SolidColorPaint(new SKColor(51, 65, 85, 120)) { StrokeThickness = 1 } // Cuadrícula slate sutil
                    }
                };

                chartEvolucion.YAxes = new Axis[]
                {
                    new Axis
                    {
                        Labeler = val => $"${val:N0}",
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                        TextSize = 11,
                        SeparatorsPaint = new SolidColorPaint(new SKColor(51, 65, 85, 120)) { StrokeThickness = 1 }
                    }
                };

                // -------------------------------------------------------------
                // 6. GRÁFICO 2: ESTADO Y DISPONIBILIDAD DE FLOTA (Gráfico de Dona Oscuro)
                // -------------------------------------------------------------
                AplicarTemaOscuroBase(chartFlota);

                var vehiculosFlota = await context.Vehiculos
                    .Include(v => v.Estado)
                    .Where(v => v.Activo)
                    .ToListAsync();

                int totalVehiculos = vehiculosFlota.Count;
                var estadosAgrupados = vehiculosFlota
                    .GroupBy(v => v.Estado?.Nombre ?? "Sin Estado")
                    .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList();

                var pieSeriesList = new List<ISeries>();
                foreach (var item in estadosAgrupados)
                {
                    double probEmpirica = totalVehiculos > 0 ? (double)item.Cantidad / totalVehiculos : 0.0;
                    double pct = probEmpirica * 100.0;

                    // Colores semánticos vibrantes para contraste sobre fondo oscuro
                    SKColor colorSlice = item.Estado.ToLower() switch
                    {
                        var s when s.Contains("disponible") => SKColor.Parse("#10B981"), // Esmeralda vibrante
                        var s when s.Contains("alquiler") || s.Contains("alquilado") => SKColor.Parse("#3B82F6"), // Azul eléctrico
                        var s when s.Contains("taller") || s.Contains("mantenimiento") => SKColor.Parse("#F59E0B"), // Ámbar cálido
                        _ => SKColor.Parse("#8B5CF6") // Púrpura
                    };

                    pieSeriesList.Add(new PieSeries<double>
                    {
                        Values = new double[] { item.Cantidad },
                        Name = $"{item.Estado} ({item.Cantidad} u. - {pct:F0}%)",
                        Fill = new SolidColorPaint(colorSlice),
                        Stroke = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 3 }, // Separación limpia entre sectores
                        InnerRadius = 65, // Dona refinada
                        ToolTipLabelFormatter = pt => $"{item.Estado}: {pt.Model} vehículo(s) [P = {probEmpirica:P1}]"
                    });
                }

                chartFlota.Series = pieSeriesList;
                chartFlota.LegendPosition = LiveChartsCore.Measure.LegendPosition.Right;

                // -------------------------------------------------------------
                // 7. GRÁFICO 3: RENDIMIENTO Y DEMANDA POR CATEGORÍA (Eje Doble Oscuro)
                // -------------------------------------------------------------
                AplicarTemaOscuroBase(chartCategorias);

                var nombresCategorias = agrupadoCat.Select(c => c.Categoria).ToArray();
                var montosRecaudados = agrupadoCat.Select(c => (double)c.TotalRecaudado).ToArray();
                var cantidadesAlq = agrupadoCat.Select(c => (double)c.CantidadAlquileres).ToArray();

                chartCategorias.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = montosRecaudados,
                        Name = "Facturación ($)",
                        Fill = new SolidColorPaint(SKColor.Parse("#3B82F6")), // Azul eléctrico
                        Rx = 4,
                        Ry = 4,
                        MaxBarWidth = 30,
                        ScalesYAt = 0,
                        YToolTipLabelFormatter = pt => $"Facturación: ${pt.Model:N2}"
                    },
                    new ColumnSeries<double>
                    {
                        Values = cantidadesAlq,
                        Name = "Alquileres (Frecuencia)",
                        Fill = new SolidColorPaint(SKColor.Parse("#10B981")), // Esmeralda vibrante
                        Rx = 4,
                        Ry = 4,
                        MaxBarWidth = 30,
                        ScalesYAt = 1,
                        YToolTipLabelFormatter = pt => $"Alquileres: {pt.Model:N0} contrato(s)"
                    }
                };

                chartCategorias.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = nombresCategorias,
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#CBD5E1")), // Letra clara para nombres de categorías
                        TextSize = 11,
                        SeparatorsPaint = new SolidColorPaint(new SKColor(51, 65, 85, 100)) { StrokeThickness = 1 }
                    }
                };

                chartCategorias.YAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "Facturación ($)",
                        NamePaint = new SolidColorPaint(SKColor.Parse("#60A5FA")), // Azul claro legible
                        Labeler = val => $"${val:N0}",
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                        TextSize = 10,
                        SeparatorsPaint = new SolidColorPaint(new SKColor(51, 65, 85, 100)) { StrokeThickness = 1 }
                    },
                    new Axis
                    {
                        Name = "Alquileres (Unid.)",
                        NamePaint = new SolidColorPaint(SKColor.Parse("#34D399")), // Verde menta brillante
                        Position = LiveChartsCore.Measure.AxisPosition.End,
                        Labeler = val => $"{val:N0}",
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                        TextSize = 10,
                        SeparatorsPaint = null // Evitar duplicar cuadrículas
                    }
                };

                chartCategorias.LegendPosition = LiveChartsCore.Measure.LegendPosition.Top;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al calcular reportes financieros y gráficos: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportarCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_contratosLiquidados == null || _contratosLiquidados.Count == 0)
            {
                MessageBox.Show("No hay contratos liquidados en el período seleccionado para exportar.", "Exportación CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar Reporte Completo de Auditoría de Contratos",
                Filter = "Archivos CSV delimitados por punto y coma (*.csv)|*.csv",
                FileName = $"Reporte_Auditoria_DriveNow_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();

                    // Encabezados enriquecidos para auditoría integral (27 columnas)
                    sb.AppendLine("Codigo_Contrato;Codigo_Reserva;Operador_Atencion;Cliente;DNI_Cliente;Telefono_Cliente;Email_Cliente;Vehiculo;Patente;Categoria_Vehicular;Tarifa_Diaria;Fecha_Pactada_Inicio;Fecha_Pactada_Fin;Fecha_Retiro_Real;Fecha_Devolucion_Real;Dias_Pactados;Dias_Efectivos;Estado_Retorno;Km_Salida;Km_Entrada;Km_Totales_Recorridos;Nivel_Combustible_Salida;Nivel_Combustible_Entrada;Inspeccion_Danos;Costo_Base_Tarifa;Recargo_Mora;Total_Facturado");

                    foreach (var c in _contratosLiquidados)
                    {
                        string fPactadaIni = c.FechaPactadaRetiro.HasValue ? c.FechaPactadaRetiro.Value.ToString("yyyy-MM-dd HH:mm") : "-";
                        string fPactadaFin = c.FechaPactadaDevolucion.HasValue ? c.FechaPactadaDevolucion.Value.ToString("yyyy-MM-dd HH:mm") : "-";
                        string fRetiroReal = c.FechaEntregaReal != default ? c.FechaEntregaReal.ToString("yyyy-MM-dd HH:mm") : "-";
                        string fDevolucionReal = c.FechaDevolucion.HasValue ? c.FechaDevolucion.Value.ToString("yyyy-MM-dd HH:mm") : "-";
                        string kmEntrada = c.KilometrajeEntrada.HasValue ? c.KilometrajeEntrada.Value.ToString() : "-";
                        string combEntrada = c.CombustibleEntrada.HasValue ? $"{c.CombustibleEntrada.Value:F0}%" : "-";

                        sb.AppendLine(string.Join(";", new string[]
                        {
                            c.CodigoAlquiler,
                            c.CodigoReserva,
                            $"\"{c.Operador.Replace("\"", "\"\"")}\"",
                            $"\"{c.Cliente.Replace("\"", "\"\"")}\"",
                            c.DniCliente,
                            c.TelefonoCliente,
                            c.EmailCliente,
                            $"\"{c.Vehiculo.Replace("\"", "\"\"")}\"",
                            c.Patente,
                            $"\"{c.Categoria.Replace("\"", "\"\"")}\"",
                            c.TarifaDiariaCategoria.ToString("F2"),
                            fPactadaIni,
                            fPactadaFin,
                            fRetiroReal,
                            fDevolucionReal,
                            c.DiasPactados.ToString(),
                            c.DiasReales.ToString("F1"),
                            $"\"{c.EstadoRetorno}\"",
                            c.KilometrajeSalida.ToString(),
                            kmEntrada,
                            c.KmRecorridos.ToString(),
                            $"{c.CombustibleSalida:F0}%",
                            combEntrada,
                            $"\"{c.DanosTexto}\"",
                            c.CostoBase.ToString("F2"),
                            c.MontoMora.ToString("F2"),
                            c.TotalCobrado.ToString("F2")
                        }));
                    }

                    // UTF-8 con BOM para que Excel en Windows/Office reconozca automáticamente tildes y caracteres especiales
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
                    MessageBox.Show("Reporte detallado exportado exitosamente con 27 columnas de auditoría operativa y financiera.", "Exportación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar archivo CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}