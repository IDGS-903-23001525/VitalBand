using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml.Linq;
using VitalBand.Models;
using VitalBand.Services;
using static System.Net.Mime.MediaTypeNames;
using iTextSharp.text;
using iTextSharp.text.pdf;
using DocPDF = iTextSharp.text.Document;

namespace VitalBand.Controllers
{
    [Authorize]
    public class HistorialController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public HistorialController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
            _env = env;
        }

        [Authorize(Roles = "Medico,medico")]
        public async Task<IActionResult> Index(int año = 0, int mes = 0, int usuarioId = 1)
        {
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;
            var medicoIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(medicoIdStr)) return Challenge();
            int idMedicoLogueado = int.Parse(medicoIdStr);

            var client = _clientFactory.CreateClient();

            string urlPacientes = _apiUrlProvider.GetApiUrl("/api/ConfiguracionApi/pacientes");
            var responsePacientes = await client.GetAsync(urlPacientes);
            if (!responsePacientes.IsSuccessStatusCode) return NotFound("Error en el servicio de pacientes.");
            var todosLosPacientes = await responsePacientes.Content.ReadFromJsonAsync<List<Paciente>>() ?? new List<Paciente>();

            var susPacientes = todosLosPacientes
                .Where(p => p.medico_asignado_id == idMedicoLogueado)
                .ToList();

            var pacienteSeleccionado = susPacientes.FirstOrDefault(p => p.id == usuarioId);
            if (pacienteSeleccionado == null && susPacientes.Any())
                pacienteSeleccionado = susPacientes.First();

            if (pacienteSeleccionado == null) return NotFound("No tiene pacientes asignados bajo su perfil.");

            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            var vm = await GenerarHistorialModelAsync(año, mes, pacienteSeleccionado, todasLasAlertas);

            ViewBag.Usuarios = susPacientes.Select(p => new UsuarioResumen { Id = p.id, Nombre = p.nombre }).ToList();

            return View("HistorialMensual", vm);
        }

        [Authorize(Roles = "Paciente,paciente")]
        public async Task<IActionResult> MiHistorial(int año = 0, int mes = 0, int usuarioId = 1)
        {
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;

            var perfilIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(perfilIdStr)) return RedirectToAction("Login", "Account");

            int idPaciente = int.Parse(perfilIdStr);
            var usuarioIdStr = User.FindFirst("UsuarioBaseId")?.Value;
            int idUsuarioReal = 0;
            if (!string.IsNullOrEmpty(usuarioIdStr))
            {
                idUsuarioReal = int.Parse(usuarioIdStr);
            }

            var client = _clientFactory.CreateClient();

            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{idPaciente}");
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode) return NotFound("No se encontró el perfil del paciente.");

            var opcionesJson = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var jsonCompleto = await responsePaciente.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(opcionesJson);

            if (!jsonCompleto.TryGetProperty("paciente", out var pacientePropiedad)) return NotFound("Estructura de datos inválida.");
            var paciente = System.Text.Json.JsonSerializer.Deserialize<Paciente>(pacientePropiedad.GetRawText(), opcionesJson);

            if (paciente == null) return NotFound();

            if (idUsuarioReal > 0)
            {
                paciente.usuario_id = idUsuarioReal;
            }

            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            var vm = await GenerarHistorialModelAsync(año, mes, paciente, todasLasAlertas);
            return View("HistorialMensual", vm);
        }

        private async Task<HistorialMensual> GenerarHistorialModelAsync(int año, int mes, Paciente paciente, List<Alerta> alertasGlobales)
        {
            var primerDia = new DateTime(año, mes, 1);
            int diasVacios = (int)primerDia.DayOfWeek == 0 ? 6 : (int)primerDia.DayOfWeek - 1;
            int totalDias = DateTime.DaysInMonth(año, mes);

            var alertasDelMes = alertasGlobales
                .Where(a => a.paciente_id == paciente.id &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .ToList();

            var dias = new List<DiaHistorial>();
            int pulsoBaseEstable = 72;

            var client = _clientFactory.CreateClient();
            var datosTelemetriaMensual = new List<Dictionary<string, object>>();

            if (paciente.usuario_id > 0)
            {
                string urlTelemetria = _apiUrlProvider.GetApiUrl($"/api/VitalSign/mensual/{paciente.usuario_id}/{año}/{mes}");
                var responseTelemetria = await client.GetAsync(urlTelemetria);
                if (responseTelemetria.IsSuccessStatusCode)
                {
                    datosTelemetriaMensual = await responseTelemetria.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? new List<Dictionary<string, object>>();
                }
            }

            for (int diaCorriente = 1; diaCorriente <= totalDias; diaCorriente++)
            {
                var alertasDeEsteDia = alertasDelMes
                    .Where(a => a.fecha_hora.Value.Day == diaCorriente)
                    .ToList();

                bool tieneAlertaEseDia = alertasDeEsteDia.Any();

                int promedioPulso = pulsoBaseEstable;

                var lecturaDeEsteDia = datosTelemetriaMensual.FirstOrDefault(lectura =>
                    lectura.TryGetValue("dia", out var d) && d != null && Convert.ToInt32(d.ToString(), CultureInfo.InvariantCulture) == diaCorriente);

                if (lecturaDeEsteDia != null && lecturaDeEsteDia.TryGetValue("bpmPromedio", out var bpmVal) && bpmVal != null)
                {
                    double bpmParseado = Convert.ToDouble(bpmVal.ToString(), CultureInfo.InvariantCulture);
                    if (bpmParseado > 0)
                    {
                        promedioPulso = (int)Math.Round(bpmParseado);
                    }
                }
                else if (tieneAlertaEseDia)
                {
                    promedioPulso = (int)alertasDeEsteDia.Average(a => a.fc_media.GetValueOrDefault());
                }

                string mensaje = tieneAlertaEseDia
                    ? $"⚠️ {alertasDeEsteDia.Count} Alerta(s)"
                    : string.Empty;

                dias.Add(new DiaHistorial
                {
                    Numero = diaCorriente,
                    BpmPromedio = promedioPulso,
                    TieneAlerta = tieneAlertaEseDia,
                    MensajeAlerta = mensaje
                });
            }

            ViewBag.FechaRegistro = paciente.Usuario?.fecha_registro ?? DateTime.Today.AddDays(-7);

            return new HistorialMensual
            {
                PeriodoNombre = primerDia.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Mes = mes,
                Año = año,
                DiasVaciosInicio = diasVacios,
                PromedioMensual = datosTelemetriaMensual.Any(t => t.TryGetValue("bpmPromedio", out var b) && b != null && Convert.ToDouble(b.ToString(), CultureInfo.InvariantCulture) > 0)
                    ? (int)Math.Round(datosTelemetriaMensual
                        .Where(t => t.TryGetValue("bpmPromedio", out var b) && b != null && Convert.ToDouble(b.ToString(), CultureInfo.InvariantCulture) > 0)
                        .Average(t => Convert.ToDouble(t["bpmPromedio"].ToString(), CultureInfo.InvariantCulture)))
                    : pulsoBaseEstable,
                TotalIncidentes = alertasDelMes.Count,
                EstadoSalud = alertasDelMes.Count > 5 ? "Riesgo Moderado" : "Estable",
                DiasDelMes = dias,
                UsuarioId = paciente.id,
                UsuarioNombre = paciente.nombre
            };
        }

        [HttpGet]
        public async Task<IActionResult> ExportarPdf(int año = 0, int mes = 0, int usuarioId = 0)
        {
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;

            int idPacienteSQL = usuarioId;
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    idPacienteSQL = pacienteId;
                }
                else
                {
                    return Forbid();
                }
            }
            else
            {
                if (idPacienteSQL == 0) idPacienteSQL = 1;
            }

            var client = _clientFactory.CreateClient();

            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{idPacienteSQL}");
            var responsePaciente = await client.GetAsync(urlPaciente);
            if (!responsePaciente.IsSuccessStatusCode) return NotFound("No se encontró el paciente.");

            var opcionesJson = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var jsonCompleto = await responsePaciente.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(opcionesJson);
            jsonCompleto.TryGetProperty("paciente", out var pacientePropiedad);
            var pacienteBD = System.Text.Json.JsonSerializer.Deserialize<Paciente>(pacientePropiedad.GetRawText(), opcionesJson);

            int edadCalculada = DateTime.Today.Year - pacienteBD.fecha_nacimiento.Year;
            if (DateTime.Today.Date < pacienteBD.fecha_nacimiento.AddYears(edadCalculada)) edadCalculada--;

            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

            var alertasMesBD = todasLasAlertas
                .Where(a => a.paciente_id == idPacienteSQL && a.fecha_hora.HasValue && a.fecha_hora.Value.Year == año && a.fecha_hora.Value.Month == mes)
                .OrderBy(a => a.fecha_hora).ToList();

            var incidentesReporte = alertasMesBD.Select(a => new IncidenteCritico
            {
                FechaHora = a.fecha_hora ?? DateTime.Now,
                Descripcion = $"Frecuencia cardíaca anómala: {a.fc_media.GetValueOrDefault()} BPM. SpO2: {a.spo2_estabilidad.GetValueOrDefault()}% y HRV: {a.hrv_rmssd.GetValueOrDefault()}.",
                Tipo = a.fc_media.GetValueOrDefault() >= 100 ? "high" : "low"
            }).ToList();

            var datosTelemetriaMensual = new List<Dictionary<string, object>>();
            int idParaBuscar = (usuarioId == 0) ? idPacienteSQL : usuarioId;
            string urlTelemetria = _apiUrlProvider.GetApiUrl($"/api/VitalSign/mensual/{idParaBuscar}/{año}/{mes}");
            var responseTelemetria = await client.GetAsync(urlTelemetria);
            if (responseTelemetria.IsSuccessStatusCode)
            {
                datosTelemetriaMensual = await responseTelemetria.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? new List<Dictionary<string, object>>();
            }

            var datosGrafica = new List<double>();
            int diasEnMes = DateTime.DaysInMonth(año, mes);
            for (int dia = 1; dia <= diasEnMes; dia++)
            {
                var lecturaDeEsteDia = datosTelemetriaMensual.FirstOrDefault(lectura =>
                    lectura.TryGetValue("dia", out var d) && d != null && Convert.ToInt32(d.ToString()) == dia);

                if (lecturaDeEsteDia != null && lecturaDeEsteDia.TryGetValue("bpmPromedio", out var bpmVal) && bpmVal != null)
                {
                    double bpmParseado = Convert.ToDouble(bpmVal.ToString());
                    if (bpmParseado > 0) { datosGrafica.Add(Math.Round(bpmParseado, 1)); continue; }
                }
                datosGrafica.Add(72.0);
            }

            string periodoTexto = new DateTime(año, mes, 1).ToString("MMMM yyyy", new CultureInfo("es-MX"));
            string identificadorFoliado = $"VB-{año}-{mes:00}-{idPacienteSQL}";

            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                DocPDF documento = new DocPDF(iTextSharp.text.PageSize.A4, 36f, 36f, 36f, 36f);
                iTextSharp.text.pdf.PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(documento, ms);
                documento.Open();

                iTextSharp.text.BaseColor colorTealHeader = new iTextSharp.text.BaseColor(9, 51, 63);   // #09333f
                iTextSharp.text.BaseColor colorTealMedio = new iTextSharp.text.BaseColor(13, 148, 136);  // #0d9488
                iTextSharp.text.BaseColor colorTealClaro = new iTextSharp.text.BaseColor(240, 253, 250); // #f0fdfa
                iTextSharp.text.BaseColor colorGrisLabel = new iTextSharp.text.BaseColor(100, 116, 139); // #64748b
                iTextSharp.text.BaseColor colorGrisTexto = new iTextSharp.text.BaseColor(51, 65, 85);    // #334155
                iTextSharp.text.BaseColor colorGrisBorde = new iTextSharp.text.BaseColor(226, 232, 240); // #e2e8f0

                iTextSharp.text.Font fuenteHeaderBlanca = iTextSharp.text.FontFactory.GetFont("Arial", 16, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                iTextSharp.text.Font fuenteHeaderMeta = iTextSharp.text.FontFactory.GetFont("Arial", 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.WHITE);
                iTextSharp.text.Font fuenteLabelGris = iTextSharp.text.FontFactory.GetFont("Arial", 8, iTextSharp.text.Font.BOLD, colorGrisLabel);
                iTextSharp.text.Font fuenteValorNegrita = iTextSharp.text.FontFactory.GetFont("Arial", 11, iTextSharp.text.Font.BOLD, colorTealHeader);
                iTextSharp.text.Font fuenteSeccion = iTextSharp.text.FontFactory.GetFont("Arial", 11, iTextSharp.text.Font.BOLD, colorTealHeader);
                iTextSharp.text.Font fuenteTexto = iTextSharp.text.FontFactory.GetFont("Arial", 9.5f, iTextSharp.text.Font.NORMAL, colorGrisTexto);
                iTextSharp.text.Font fuenteAlertaTitulo = iTextSharp.text.FontFactory.GetFont("Arial", 9.5f, iTextSharp.text.Font.BOLD, colorTealHeader);
                iTextSharp.text.Font fuenteEjesGrafica = iTextSharp.text.FontFactory.GetFont("Arial", 7.5f, iTextSharp.text.Font.NORMAL, colorGrisLabel);


                iTextSharp.text.pdf.PdfPTable tablaEncabezadoColor = new iTextSharp.text.pdf.PdfPTable(2);
                tablaEncabezadoColor.WidthPercentage = 100;
                tablaEncabezadoColor.SetWidths(new float[] { 65f, 35f });

                iTextSharp.text.pdf.PdfPTable subTablaBrand = new iTextSharp.text.pdf.PdfPTable(2);
                subTablaBrand.SetWidths(new float[] { 15f, 85f });
                subTablaBrand.DefaultCell.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;

                string rutaLogo = System.IO.Path.Combine(_env.WebRootPath, "img", "logoVitalBand.png");
                if (System.IO.File.Exists(rutaLogo))
                {
                    iTextSharp.text.Image imgLogo = iTextSharp.text.Image.GetInstance(rutaLogo);
                    imgLogo.ScaleToFit(28f, 28f);
                    iTextSharp.text.pdf.PdfPCell celdaLogo = new iTextSharp.text.pdf.PdfPCell(imgLogo);
                    celdaLogo.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                    celdaLogo.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                    subTablaBrand.AddCell(celdaLogo);
                }
                else
                {
                    subTablaBrand.AddCell(new iTextSharp.text.pdf.PdfPCell() { Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER });
                }

                iTextSharp.text.pdf.PdfPCell celdaTextoBrand = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph("VitalBand", fuenteHeaderBlanca));
                celdaTextoBrand.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                celdaTextoBrand.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                celdaTextoBrand.PaddingLeft = 4f;
                subTablaBrand.AddCell(celdaTextoBrand);

                iTextSharp.text.pdf.PdfPCell celdaIzquierdaMaestra = new iTextSharp.text.pdf.PdfPCell(subTablaBrand);
                celdaIzquierdaMaestra.BackgroundColor = colorTealHeader;
                celdaIzquierdaMaestra.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                celdaIzquierdaMaestra.PaddingTop = 14f;
                celdaIzquierdaMaestra.PaddingBottom = 14f;
                celdaIzquierdaMaestra.PaddingLeft = 20f;
                celdaIzquierdaMaestra.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                tablaEncabezadoColor.AddCell(celdaIzquierdaMaestra);

                iTextSharp.text.pdf.PdfPCell celdaMetaHeader = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph($"Emisión: {DateTime.Today.ToString("dd/MM/yyyy")}", fuenteHeaderMeta));
                celdaMetaHeader.BackgroundColor = colorTealHeader;
                celdaMetaHeader.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                celdaMetaHeader.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                celdaMetaHeader.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                celdaMetaHeader.PaddingRight = 20f;
                tablaEncabezadoColor.AddCell(celdaMetaHeader);

                tablaEncabezadoColor.SpacingAfter = 25f;
                documento.Add(tablaEncabezadoColor);

                iTextSharp.text.pdf.PdfPTable tablaFichaPaciente = new iTextSharp.text.pdf.PdfPTable(2);
                tablaFichaPaciente.WidthPercentage = 100;
                tablaFichaPaciente.SetWidths(new float[] { 60f, 40f });

                iTextSharp.text.Paragraph pPaciente = new iTextSharp.text.Paragraph();
                pPaciente.Add(new iTextSharp.text.Chunk("PACIENTE ASIGNADO\n", fuenteLabelGris));
                pPaciente.Add(new iTextSharp.text.Chunk($"{pacienteBD?.nombre}, {edadCalculada} años", fuenteValorNegrita));
                iTextSharp.text.pdf.PdfPCell cFichaIzq = new iTextSharp.text.pdf.PdfPCell(pPaciente);
                cFichaIzq.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                cFichaIzq.PaddingLeft = 10f;
                tablaFichaPaciente.AddCell(cFichaIzq);

                iTextSharp.text.Paragraph pCiclo = new iTextSharp.text.Paragraph();
                pCiclo.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                pCiclo.Add(new iTextSharp.text.Chunk("CICLO DE TELEMETRÍA\n", fuenteLabelGris));
                pCiclo.Add(new iTextSharp.text.Chunk($"{periodoTexto.ToLower()}", fuenteValorNegrita));
                iTextSharp.text.pdf.PdfPCell cFichaDer = new iTextSharp.text.pdf.PdfPCell(pCiclo);
                cFichaDer.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                cFichaDer.PaddingRight = 10f;
                tablaFichaPaciente.AddCell(cFichaDer);

                tablaFichaPaciente.SpacingAfter = 4f;
                documento.Add(tablaFichaPaciente);

                iTextSharp.text.Paragraph lineaSeparadoraFicha = new iTextSharp.text.Paragraph(new iTextSharp.text.Chunk(new iTextSharp.text.pdf.draw.LineSeparator(1.2f, 98f, colorTealMedio, iTextSharp.text.Element.ALIGN_CENTER, 0f)));
                lineaSeparadoraFicha.SpacingAfter = 25f;
                documento.Add(lineaSeparadoraFicha);

                iTextSharp.text.Paragraph tSeccionInterp = new iTextSharp.text.Paragraph("INTERPRETACIÓN MÉDICA AUTOMATIZADA", fuenteSeccion);
                tSeccionInterp.SpacingAfter = 12f;
                tSeccionInterp.IndentationLeft = 10f;
                documento.Add(tSeccionInterp);

                iTextSharp.text.pdf.PdfPTable containerCajaInterp = new iTextSharp.text.pdf.PdfPTable(1);
                containerCajaInterp.WidthPercentage = 98;

                iTextSharp.text.Paragraph contenidoCajaAnalisis = new iTextSharp.text.Paragraph();
                if (incidentesReporte.Any())
                {
                    contenidoCajaAnalisis.Add(new iTextSharp.text.Chunk("Anomalías detectadas durante el período evaluado\n", fuenteAlertaTitulo));
                    contenidoCajaAnalisis.Add(new iTextSharp.text.Chunk($"El sistema de monitorización ha registrado un total de {incidentesReporte.Count} incidente(s) crítico(s) en este ciclo mensual. Se sugiere al especialista médico realizar un cruce de datos con la bitácora general para evaluar posibles causas externas o variaciones hemodinámicas fuera de los rangos basales.", fuenteTexto));
                }
                else
                {
                    contenidoCajaAnalisis.Add(new iTextSharp.text.Chunk("Comportamiento cardíaco dentro de parámetros óptimos\n", fuenteAlertaTitulo));
                    contenidoCajaAnalisis.Add(new iTextSharp.text.Chunk("La telemetría de red consolidada muestra una regularidad constante en las frecuencias transmitidas. El paciente no presentó picos atípicos ni activaciones de alertas de urgencia IoT durante el mes.", fuenteTexto));
                }

                iTextSharp.text.pdf.PdfPCell celdaInternaBox = new iTextSharp.text.pdf.PdfPCell(contenidoCajaAnalisis);
                celdaInternaBox.Padding = 14f;
                celdaInternaBox.BackgroundColor = incidentesReporte.Any() ? new iTextSharp.text.BaseColor(254, 242, 242) : colorTealClaro;
                celdaInternaBox.BorderColor = incidentesReporte.Any() ? new iTextSharp.text.BaseColor(254, 202, 202) : colorTealMedio;
                celdaInternaBox.Border = iTextSharp.text.pdf.PdfPCell.BOX;
                containerCajaInterp.AddCell(celdaInternaBox);
                containerCajaInterp.SpacingAfter = 25f;
                documento.Add(containerCajaInterp);

                iTextSharp.text.Paragraph tSeccionGrafica = new iTextSharp.text.Paragraph("ANÁLISIS DINÁMICO DE FRECUENCIA CARDÍACA", fuenteSeccion);
                tSeccionGrafica.SpacingAfter = 12f;
                tSeccionGrafica.IndentationLeft = 10f;
                documento.Add(tSeccionGrafica);

                float canvasWidth = 525f;
                float canvasHeight = 170f;
                float paddingLeft = 30f;
                float paddingBottom = 20f;
                float graphWidth = canvasWidth - paddingLeft - 10f;
                float graphHeight = canvasHeight - paddingBottom - 10f;

                iTextSharp.text.pdf.PdfTemplate plantillaGrafica = writer.DirectContent.CreateTemplate(canvasWidth, canvasHeight);

                plantillaGrafica.SetLineWidth(1.0f);
                plantillaGrafica.SetColorStroke(colorGrisBorde);
                plantillaGrafica.Rectangle(paddingLeft, paddingBottom, graphWidth, graphHeight);
                plantillaGrafica.Stroke();

                int minBpm = 50;
                int maxBpm = 110;
                int stepBpm = 10;

                for (int bpm = minBpm; bpm <= maxBpm; bpm += stepBpm)
                {
                    float ratio = (float)(bpm - minBpm) / (maxBpm - minBpm);
                    float yPos = paddingBottom + (ratio * graphHeight);

                    if (bpm > minBpm && bpm < maxBpm)
                    {
                        plantillaGrafica.SetLineWidth(0.6f);
                        plantillaGrafica.SetColorStroke(new iTextSharp.text.BaseColor(241, 245, 249)); // #f1f5f9
                        plantillaGrafica.MoveTo(paddingLeft, yPos);
                        plantillaGrafica.LineTo(canvasWidth - 10f, yPos);
                        plantillaGrafica.Stroke();
                    }

                    iTextSharp.text.pdf.ColumnText.ShowTextAligned(
                        plantillaGrafica,
                        iTextSharp.text.Element.ALIGN_RIGHT,
                        new iTextSharp.text.Phrase(bpm.ToString(), fuenteEjesGrafica),
                        paddingLeft - 6f,
                        yPos - 3f,
                        0
                    );
                }

                int[] diasEtiquetas = new int[] { 1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31 };
                float deltaX = graphWidth / (diasEnMes - 1);

                foreach (int d in diasEtiquetas)
                {
                    if (d <= diasEnMes)
                    {
                        float xPos = paddingLeft + ((d - 1) * deltaX);
                        iTextSharp.text.pdf.ColumnText.ShowTextAligned(
                            plantillaGrafica,
                            iTextSharp.text.Element.ALIGN_CENTER,
                            new iTextSharp.text.Phrase($"Día {d}", fuenteEjesGrafica),
                            xPos,
                            paddingBottom - 14f,
                            0
                        );
                    }
                }

                if (datosGrafica.Any())
                {
                    plantillaGrafica.SetLineWidth(2.2f);
                    plantillaGrafica.SetColorStroke(colorTealMedio);

                    for (int i = 0; i < datosGrafica.Count; i++)
                    {
                        double bpmActual = datosGrafica[i] == 0 ? 72.0 : datosGrafica[i];
                        float ratioY = (float)(bpmActual - minBpm) / (maxBpm - minBpm);
                        float yPos = paddingBottom + (ratioY * graphHeight);
                        float xPos = paddingLeft + (i * deltaX);

                        if (i == 0) plantillaGrafica.MoveTo(xPos, yPos);
                        else plantillaGrafica.LineTo(xPos, yPos);
                    }
                    plantillaGrafica.Stroke();

                    plantillaGrafica.SetColorFill(new iTextSharp.text.BaseColor(20, 184, 166)); // #14b8a6
                    float radiusNodo = datosGrafica.Count > 15 ? 1.2f : 2.2f;
                    for (int i = 0; i < datosGrafica.Count; i += 2)
                    {
                        double bpmActual = datosGrafica[i] == 0 ? 72.0 : datosGrafica[i];
                        float ratioY = (float)(bpmActual - minBpm) / (maxBpm - minBpm);
                        float yPos = paddingBottom + (ratioY * graphHeight);
                        float xPos = paddingLeft + (i * deltaX);

                        plantillaGrafica.Circle(xPos, yPos, radiusNodo);
                        plantillaGrafica.Fill();
                    }
                }

                iTextSharp.text.Image componenteGraficaImg = iTextSharp.text.Image.GetInstance(plantillaGrafica);
                componenteGraficaImg.SpacingAfter = 10f;
                componenteGraficaImg.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                documento.Add(componenteGraficaImg);

                documento.NewPage();

                iTextSharp.text.Paragraph tTablaSecundario = new iTextSharp.text.Paragraph("REGISTRO DE INCIDENTES CRÍTICOS DETALLADOS", fuenteSeccion);
                tTablaSecundario.SpacingAfter = 12f;
                documento.Add(tTablaSecundario);

                iTextSharp.text.pdf.PdfPTable tablaIncidentes = new iTextSharp.text.pdf.PdfPTable(2);
                tablaIncidentes.WidthPercentage = 100;
                tablaIncidentes.SetWidths(new float[] { 30f, 70f });

                iTextSharp.text.pdf.PdfPCell th1 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph("FECHA Y HORA", iTextSharp.text.FontFactory.GetFont("Arial", 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE)));
                th1.BackgroundColor = colorTealMedio;
                th1.Padding = 8f;
                th1.BorderColor = colorGrisBorde;
                tablaIncidentes.AddCell(th1);

                iTextSharp.text.pdf.PdfPCell th2 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph("DESCRIPCIÓN DEL EVENTO", iTextSharp.text.FontFactory.GetFont("Arial", 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE)));
                th2.BackgroundColor = colorTealMedio;
                th2.Padding = 8f;
                th2.BorderColor = colorGrisBorde;
                tablaIncidentes.AddCell(th2);

                if (incidentesReporte.Any())
                {
                    foreach (var inc in incidentesReporte)
                    {
                        iTextSharp.text.pdf.PdfPCell tdFecha = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(inc.FechaHora.ToString("dd/MM/yyyy HH:mm"), fuenteTexto));
                        tdFecha.Padding = 8f;
                        tdFecha.BorderColor = colorGrisBorde;
                        tablaIncidentes.AddCell(tdFecha);

                        iTextSharp.text.pdf.PdfPCell tdDesc = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(inc.Descripcion, fuenteTexto));
                        tdDesc.Padding = 8f;
                        tdDesc.BorderColor = colorGrisBorde;
                        tablaIncidentes.AddCell(tdDesc);
                    }
                }
                else
                {
                    iTextSharp.text.pdf.PdfPCell celdaVacia = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph("No se registraron incidentes críticos ni anomalías cardíacas en este período.", fuenteTexto));
                    celdaVacia.Colspan = 2;
                    celdaVacia.Padding = 20f;
                    celdaVacia.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                    celdaVacia.BorderColor = colorGrisBorde;
                    tablaIncidentes.AddCell(celdaVacia);
                }
                tablaIncidentes.SpacingAfter = 50f;
                documento.Add(tablaIncidentes);

                iTextSharp.text.pdf.PdfPTable tablaFirma = new iTextSharp.text.pdf.PdfPTable(2);
                tablaFirma.WidthPercentage = 100;
                tablaFirma.SetWidths(new float[] { 50f, 50f });

                iTextSharp.text.Paragraph pFirmaLine = new iTextSharp.text.Paragraph("_______________________\nFirma del Profesional", fuenteTexto);
                iTextSharp.text.pdf.PdfPCell cFirma = new iTextSharp.text.pdf.PdfPCell(pFirmaLine);
                cFirma.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                tablaFirma.AddCell(cFirma);

                iTextSharp.text.Paragraph pInfoPie = new iTextSharp.text.Paragraph($"Vital Health Systems\nEcosistema IoT Preventivo\nID: {identificadorFoliado}", iTextSharp.text.FontFactory.GetFont("Arial", 8, iTextSharp.text.Font.NORMAL, colorGrisTexto));
                iTextSharp.text.pdf.PdfPCell cInfo = new iTextSharp.text.pdf.PdfPCell(pInfoPie);
                cInfo.Border = iTextSharp.text.pdf.PdfPCell.NO_BORDER;
                cInfo.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                tablaFirma.AddCell(cInfo);

                documento.Add(tablaFirma);
                documento.Close();

                byte[] bytesPdf = ms.ToArray();
                string nombreArchivoPdf = $"Reporte_Salud_{idPacienteSQL}_{mes}_{año}.pdf";
                return File(bytesPdf, "application/pdf", nombreArchivoPdf);
            }
        }
    }
}