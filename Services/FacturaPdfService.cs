using System;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DriveNow.Models;

namespace DriveNow.Services
{
    public class FacturaPdfService : IFacturaPdfService
    {
        public string GenerarFacturaPdf(Alquiler alquiler, string rutaDestino)
        {
            string? carpeta = Path.GetDirectoryName(rutaDestino);
            if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            var cliente = alquiler.Reserva?.Cliente;
            var vehiculo = alquiler.Reserva?.Vehiculo;
            var reserva = alquiler.Reserva;
            var empleadoEntrega = alquiler.EmpleadoEntrega;
            var empleadoDevolucion = alquiler.EmpleadoDevolucion;

            // Cálculos
            int diasAlquiler = reserva != null ? Math.Max(1, (reserva.FechaHasta.Date - reserva.FechaDesde.Date).Days) : 1;
            decimal tarifaDiaria = diasAlquiler > 0 ? alquiler.CostoTotal / diasAlquiler : alquiler.CostoTotal;
            decimal totalFacturado = alquiler.CostoTotal + alquiler.MontoMora;
            decimal subtotalNeto = Math.Round(totalFacturado / 1.21m, 2);
            decimal iva21 = totalFacturado - subtotalNeto;

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Lato").FontColor(Colors.Grey.Darken3));

                    // 1. CABECERA
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Izquierda: Branding DriveNow
                            row.RelativeItem().Column(brand =>
                            {
                                brand.Item().Text("DRIVE NOW").FontSize(24).Bold().FontColor("#1E3A8A");
                                brand.Item().Text("ALQUILER Y GESTIÓN INTELIGENTE DE FLOTA").FontSize(8).Bold().FontColor("#64748B");
                                brand.Item().PaddingTop(4).Text("DriveNow Argentina S.A.  •  CUIT: 30-71894523-4").FontSize(8.5f).FontColor("#475569");
                                brand.Item().Text("Av. 3 de Abril 4500, Ctes. Capital  •  Tel: +54 (0379) 4345690").FontSize(8.5f).FontColor("#475569");
                                brand.Item().Text("contacto@drivenow.com.ar  •  www.drivenow.com.ar").FontSize(8.5f).FontColor("#475569");
                            });

                            // Derecha: Cuadro de Comprobante
                            row.ConstantItem(210).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(10).Column(rec =>
                            {
                                rec.Item().Text("LIQUIDACIÓN DE ALQUILER").FontSize(11).Bold().FontColor("#0F172A");
                                rec.Item().Text($"N°: FAC-{alquiler.IdAlquiler:D6}").FontSize(13).Bold().FontColor("#2563EB");
                                rec.Item().PaddingTop(3).Text($"Fecha Emisión: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8.5f);
                                rec.Item().Text($"Reserva Asociada: {reserva?.CodigoReserva ?? "N/A"}").FontSize(8.5f);
                                rec.Item().Text("Condición IVA: Responsable Inscripto").FontSize(8f).FontColor("#64748B");
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor("#E2E8F0");
                    });

                    // 2. CUERPO PRINCIPAL
                    page.Content().Column(col =>
                    {
                        // Sección A: Datos del Cliente y Datos del Vehículo
                        col.Item().Row(row =>
                        {
                            // Tarjeta Cliente
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").CornerRadius(4).Padding(10).Column(c =>
                            {
                                c.Item().Text("DATOS DEL TITULAR / CONDUCTOR").FontSize(9).Bold().FontColor("#1E3A8A");
                                c.Item().PaddingTop(4).Text($"Nombre: {cliente?.NombreCompleto ?? "N/D"}").Bold();
                                c.Item().Text($"DNI / Doc: {cliente?.Dni ?? "N/D"}  |  Nacionalidad: {cliente?.Nacionalidad ?? "Argentina"}");
                                c.Item().Text($"Domicilio: {cliente?.DomicilioCompleto ?? "-"}");
                                c.Item().Text($"Teléfono: {cliente?.Telefono ?? "-"}");
                                c.Item().Text($"Licencia N°: {cliente?.NroLicencia ?? "-"} (Vence: {cliente?.VencimientoLicencia:dd/MM/yyyy})");
                            });

                            row.ConstantItem(12);

                            // Tarjeta Vehículo
                            row.RelativeItem().Border(1).BorderColor("#E2E8F0").CornerRadius(4).Padding(10).Column(v =>
                            {
                                v.Item().Text("DATOS DEL VEHÍCULO Y SERVICIO").FontSize(9).Bold().FontColor("#1E3A8A");
                                v.Item().PaddingTop(4).Text($"Vehículo: {vehiculo?.MarcaModelo ?? "N/D"} ({vehiculo?.Anio})").Bold();
                                v.Item().Text($"Patente: {vehiculo?.Patente ?? "N/D"}  |  Cat: {vehiculo?.Categoria?.Nombre ?? "N/D"}");
                                v.Item().Text($"Retiro: {alquiler.FechaEntregaReal:dd/MM/yyyy HH:mm} (Km: {alquiler.KilometrajeSalida})");
                                string devTexto = alquiler.FechaEntregaDevolucion.HasValue ? alquiler.FechaEntregaDevolucion.Value.ToString("dd/MM/yyyy HH:mm") : "En curso";
                                v.Item().Text($"Retorno: {devTexto} (Km: {alquiler.KilometrajeEntrada?.ToString() ?? "-"})");
                                v.Item().Text($"Uso Total: {alquiler.KmRecorridosTexto}  |  Tanque: {alquiler.CombustibleEntrada?.ToString() ?? "100"}%");
                            });
                        });

                        col.Item().PaddingTop(14);

                        // Sección B: Tabla de Detalle y Conceptos
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#0F172A").Padding(6).Text("#").FontColor(Colors.White).Bold().FontSize(8.5f);
                                header.Cell().Background("#0F172A").Padding(6).Text("CONCEPTO / DESCRIPCIÓN").FontColor(Colors.White).Bold().FontSize(8.5f);
                                header.Cell().Background("#0F172A").Padding(6).AlignCenter().Text("CANTIDAD").FontColor(Colors.White).Bold().FontSize(8.5f);
                                header.Cell().Background("#0F172A").Padding(6).AlignRight().Text("PRECIO UNIT.").FontColor(Colors.White).Bold().FontSize(8.5f);
                                header.Cell().Background("#0F172A").Padding(6).AlignRight().Text("TOTAL").FontColor(Colors.White).Bold().FontSize(8.5f);
                            });

                            // Fila 1: Tarifa Base
                            table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Text("1").FontSize(8.5f);
                            table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Column(d =>
                            {
                                d.Item().Text($"Alquiler de Vehículo - {vehiculo?.MarcaModelo ?? "Flota"}").Bold().FontSize(8.5f);
                                d.Item().Text($"Período contratado ({reserva?.FechaDesde:dd/MM/yyyy} al {reserva?.FechaHasta:dd/MM/yyyy})").FontSize(7.5f).FontColor("#64748B");
                            });
                            table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignCenter().Text($"{diasAlquiler} día(s)").FontSize(8.5f);
                            table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"${tarifaDiaria:N2}").FontSize(8.5f);
                            table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"${alquiler.CostoTotal:N2}").Bold().FontSize(8.5f);

                            // Fila 2: Mora (si existió)
                            if (alquiler.MontoMora > 0)
                            {
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Text("2").FontSize(8.5f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Column(d =>
                                {
                                    d.Item().Text("Recargo por retraso en devolución fuera de término").Bold().FontColor("#B91C1C").FontSize(8.5f);
                                    d.Item().Text("Penalización contractual por horas/días excedentes").FontSize(7.5f).FontColor("#64748B");
                                });
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignCenter().Text("1 servicio").FontSize(8.5f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"${alquiler.MontoMora:N2}").FontSize(8.5f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"${alquiler.MontoMora:N2}").Bold().FontColor("#B91C1C").FontSize(8.5f);
                            }

                            // Fila 3: Daños reportados (si existieron)
                            if (alquiler.Danos != null && alquiler.Danos.Count > 0)
                            {
                                int numItem = alquiler.MontoMora > 0 ? 3 : 2;
                                var dano = alquiler.Danos.First();
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Text(numItem.ToString()).FontSize(8.5f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).Column(d =>
                                {
                                    d.Item().Text($"Inspección de Devolución - Daño asentado: {dano.Zona}").Bold().FontColor("#C2410C").FontSize(8.5f);
                                    d.Item().Text(dano.Descripcion).FontSize(7.5f).FontColor("#64748B");
                                });
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignCenter().Text("1 acta").FontSize(8.5f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text("Según Franquicia").FontSize(8f);
                                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text("Liquidado").FontSize(8f).FontColor("#C2410C");
                            }
                        });

                        col.Item().PaddingTop(12);

                        // Sección C: Resumen de Totales
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().PaddingRight(20).Column(obs =>
                            {
                                obs.Item().Text("OPERADORES INTERVINIENTES").FontSize(8.5f).Bold().FontColor("#1E3A8A");
                                obs.Item().Text($"Entrega de llaves: {empleadoEntrega?.NombreCompleto ?? "Oficina Central"}").FontSize(8f);
                                obs.Item().Text($"Recepción y control: {empleadoDevolucion?.NombreCompleto ?? "Oficina Central"}").FontSize(8f);
                                obs.Item().PaddingTop(6).Text("Forma de Pago: Tarjeta / Depósito Bancario  •  Comprobante válido como constancia de servicio").FontSize(7.5f).FontColor("#64748B");
                            });

                            row.ConstantItem(210).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(8).Column(tot =>
                            {
                                tot.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Subtotal Neto (sin IVA):").FontSize(8.5f);
                                    r.ConstantItem(75).AlignRight().Text($"${subtotalNeto:N2}").FontSize(8.5f);
                                });
                                tot.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("IVA (21.0%):").FontSize(8.5f);
                                    r.ConstantItem(75).AlignRight().Text($"${iva21:N2}").FontSize(8.5f);
                                });
                                tot.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#CBD5E1");
                                tot.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL FINAL:").FontSize(11).Bold().FontColor("#0F172A");
                                    r.ConstantItem(90).AlignRight().Text($"${totalFacturado:N2}").FontSize(12).Bold().FontColor("#16A34A");
                                });
                            });
                        });
                    });

                    // 3. PIE DE PÁGINA Y FIRMAS
                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor("#E2E8F0");

                        // Firmas
                        col.Item().PaddingTop(15).Row(row =>
                        {
                            row.RelativeItem().Column(f1 =>
                            {
                                f1.Item().LineHorizontal(1).LineColor("#94A3B8");
                                f1.Item().AlignCenter().Text("Firma y Sello del Operador (DriveNow)").FontSize(8).FontColor("#64748B");
                            });

                            row.ConstantItem(50);

                            row.RelativeItem().Column(f2 =>
                            {
                                f2.Item().LineHorizontal(1).LineColor("#94A3B8");
                                f2.Item().AlignCenter().Text("Firma y Aclaración del Conductor / Titular").FontSize(8).FontColor("#64748B");
                            });
                        });

                        col.Item().PaddingTop(12).AlignCenter().Text("DriveNow Argentina  •  Gracias por confiar en nuestros servicios de alquiler").FontSize(7.5f).FontColor("#94A3B8");
                    });
                });
            });

            documento.GeneratePdf(rutaDestino);
            return rutaDestino;
        }
    }
}
