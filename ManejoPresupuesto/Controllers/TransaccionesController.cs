using AutoMapper;
using ClosedXML.Excel;
using ManejoPresupuesto.Models;
using ManejoPresupuesto.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;

namespace ManejoPresupuesto.Controllers
{    
    public class TransaccionesController: Controller
    {
        private readonly IservicioUsuarios servicioUsuarios;
        private readonly IRepostiorioCuentas repositiorioCuentas;
        private readonly IRepositorioTransacciones repositorioTransacciones;
        private readonly IRepositorioCategorias repositorioCategorias;
        private readonly IMapper mapper;
        private readonly IServicioReportes servicioReportes;

        public TransaccionesController(IservicioUsuarios servicioUsuarios,
            IRepostiorioCuentas repositiorioCuentas, IRepositorioTransacciones repositorioTransacciones, 
            IRepositorioCategorias repositorioCategorias,
            IMapper mapper, IServicioReportes servicioReportes)
        {
            this.servicioUsuarios = servicioUsuarios;
            this.repositiorioCuentas = repositiorioCuentas;
            this.repositorioTransacciones = repositorioTransacciones;
            this.repositorioCategorias = repositorioCategorias;
            this.mapper = mapper;
            this.servicioReportes = servicioReportes;
        }
        [Authorize]
        public async Task<IActionResult> Index(int mes, int año)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var modelo = await servicioReportes.ObtenerReporteTransaccionesDetalladas(usuarioId, mes, año, ViewBag);
          
            return View(modelo);
        }
        public async Task<IActionResult> Semanal(int mes, int año)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            IEnumerable<ResultadoObtenerPorSemana> transaccionesPorSemana = 
                await servicioReportes.ObtenerReporteSemanal(usuarioId, mes, año, ViewBag);

            var agrupado = transaccionesPorSemana.GroupBy(x => x.Semana).Select(x => new ResultadoObtenerPorSemana(){
                Semana = x.Key,
                Ingresos = x.Where(x => x.TipoOperacionId == TipoOperacion.Ingreso)
                .Select(x => x.Monto).FirstOrDefault(),
                Gastos = x.Where(x => x.TipoOperacionId == TipoOperacion.Gasto)
                .Select(x => x.Monto).FirstOrDefault(),

            }).ToList();

            if(año == 0 || mes == 0)
            {
                var hoy = DateTime.Today;
                año = hoy.Year;
                mes = hoy.Month;
            }

            var fechaReferencia = new DateTime(año, mes, 1);
            var diasDelMes = Enumerable.Range(1, fechaReferencia.AddMonths(1).AddDays(-1).Day);

            var diasSegmentados = diasDelMes.Chunk(7).ToList();

            for( int i = 0; i < diasSegmentados.Count; i++)
            {
                var semana = i + 1;
                var fechaInicio = new DateTime(año, mes, diasSegmentados[i].First());
                var fechaFin = new DateTime(año, mes, diasSegmentados[i].Last());
                var grupoSemana = agrupado.FirstOrDefault(x => x.Semana == semana);

                if(grupoSemana is null)
                {
                    agrupado.Add(new ResultadoObtenerPorSemana()
                    {
                        Semana = semana,
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,
                    });
                }
                else
                {
                    grupoSemana.FechaInicio = fechaInicio;
                    grupoSemana.FechaFin = fechaFin;
                }
            }

            agrupado = agrupado.OrderByDescending(x => x.Semana).ToList();
            
            var modelo = new ReporteSemanalViewModel();
            modelo.TransaccionesPorSemana = agrupado;
            modelo.FechaReferencia = fechaReferencia;
            
            return View(modelo);
        }
        public async Task<IActionResult> Mensual(int año)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            if(año == 0)
            {
                año = DateTime.Today.Year;
            }

            var transaccionesPorMes = await repositorioTransacciones.ObtenerPorMes(usuarioId, año);

            var transaccionesAgrupadas = transaccionesPorMes.GroupBy(x => x.Mes)
                .Select(x => new ResultadoObtenerPorMes()
                {
                    Mes = x.Key,
                    Ingreso = x.Where(x => x.TipoOperacionId == TipoOperacion.Ingreso)
                    .Select(x => x.Monto).FirstOrDefault(),
                    Gasto = x.Where(x => x.TipoOperacionId == TipoOperacion.Gasto)
                    .Select(x => x.Monto).FirstOrDefault()
                }).ToList();

            for(int mes = 1; mes <= 12; mes++)
            {
                var transaccion = transaccionesAgrupadas.FirstOrDefault(x => x.Mes == mes);
                var fechaReferencia = new DateTime(año, mes, 1);
                if(transaccion is null)
                {
                    transaccionesAgrupadas.Add(new ResultadoObtenerPorMes()
                    {
                        Mes = mes,
                        FechaReferencia = fechaReferencia
                    });
                }
                else
                {
                    transaccion.FechaReferencia = fechaReferencia;
                }
            }

            transaccionesAgrupadas = transaccionesAgrupadas.OrderByDescending(x => x.Mes).ToList();

            var modelo = new ReporteMensualViewModel();
            modelo.Año = año;
            modelo.TransaccionesPorMes = transaccionesAgrupadas;

            return View(modelo);
        }
        public IActionResult ExcelReporte()
        {
            return View();
        }
        public async Task<FileResult> ExportarExcelPorMes(int mes, int año)
        {
            var fechaInicio = new DateTime(año, mes, 1);
            var fechaFin = fechaInicio.AddMonths(1).AddDays(-1);
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var transacciones = await repositorioTransacciones.ObtenrPorUsuarioId(new ParametroObtenerTransaccionesPorUsuario
            {
                UsuarioId = usuarioId,
                FechaFin = fechaFin,
                FechaInicio = fechaInicio
            });

            var nombreArchivo = $"Manejo Presupuesto - {fechaInicio.ToString("MMM yyyy")}.xlsx";

            return GenerarExcel(nombreArchivo, transacciones);
        }

        public async Task<FileResult> ExportarExcelPorAño(int año)
        {
            var fechaInicio = new DateTime(año, 1, 1);
            var fechaFin = fechaInicio.AddYears(1).AddDays(-1);

            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var transacciones = await repositorioTransacciones.ObtenrPorUsuarioId(new ParametroObtenerTransaccionesPorUsuario
            {
                UsuarioId = usuarioId,
                FechaFin = fechaFin,
                FechaInicio = fechaInicio
            });

            var nombreArchivo = $"Manejo Presupuesto - {fechaInicio.ToString("yyyy")}.xlsx";

            return GenerarExcel(nombreArchivo, transacciones);
        }
        public async Task<FileResult> ExportarExcelTodo()
        {
            var fechaInicio = DateTime.Today.AddYears(-100);
            var fechaFin = DateTime.Today.AddYears(1000);

            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var transacciones = await repositorioTransacciones.ObtenrPorUsuarioId(new ParametroObtenerTransaccionesPorUsuario
            {
                UsuarioId = usuarioId,
                FechaFin = fechaFin,
                FechaInicio = fechaInicio
            });

            var nombreArchivo = $"Manejo Presupuesto - {DateTime.Today.ToString("dd-MM-yyyy")}.xlsx";

            return GenerarExcel(nombreArchivo, transacciones);
        }
            private FileResult GenerarExcel(string nombreArchivo, IEnumerable<Transaccion> transacciones)
        {
            DataTable dataTable = new DataTable("Transacciones");
            dataTable.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("Fecha"),
                new DataColumn("Cuenta"),
                new DataColumn("Categoria"),
                new DataColumn("Nota"),
                new DataColumn("Monto"),
                new DataColumn("Ingreso/Gasto")     
            });

            foreach(var transaccion in transacciones)
            {
                dataTable.Rows.Add(transaccion.FechaTransaccion,
                    transaccion.Cuenta,
                    transaccion.Categoria,
                    transaccion.Nota,
                    transaccion.Monto,
                    transaccion.TipoOperacionId);
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.Worksheets.Add(dataTable);

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    return File(stream.ToArray(), 
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                        nombreArchivo);
                }
            }
        }
        public IActionResult Calendario()
        {

            return View();
        }

        public async Task<JsonResult> ObtenerTransaccionesCalendario(DateTime start, DateTime end)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var transacciones = await repositorioTransacciones.ObtenrPorUsuarioId(new ParametroObtenerTransaccionesPorUsuario
            {
                UsuarioId = usuarioId,
                FechaFin = end,
                FechaInicio = start
            });

            var eventosCalendario = transacciones.Select(transacciones => new EventoCalendario()
            {
                Title = transacciones.Monto.ToString("N"),
                Start = transacciones.FechaTransaccion.ToString("yyyy-MM-dd"),
                End = transacciones.FechaTransaccion.ToString("yyyy-MM-dd"),
                Color = (transacciones.TipoOperacionId == TipoOperacion.Gasto ? "Red" : null)
            });

            return Json(eventosCalendario);
        }

        public async Task<JsonResult> ObtenerTransaccionesPorFecha(DateTime fecha)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            var transacciones = await repositorioTransacciones.ObtenrPorUsuarioId(new ParametroObtenerTransaccionesPorUsuario
            {
                UsuarioId = usuarioId,
                FechaFin = fecha,
                FechaInicio = fecha
            });

            return Json(transacciones);
        }

        public async Task<IActionResult> Crear()
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var modelo = new TransaccionCreacionViewModel();
            modelo.Cuentas = await ObtenerCuentas(usuarioId);
            modelo.Categorias = await ObtenerCategorias(usuarioId,modelo.TipoOperacionId);
            return View(modelo);
        }

        [HttpPost]
        public async Task<IActionResult> Crear(TransaccionCreacionViewModel modelo)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();

            if (!ModelState.IsValid)
            {                
                modelo.Cuentas = await ObtenerCuentas(usuarioId);
                modelo.Categorias = await ObtenerCategorias(usuarioId, modelo.TipoOperacionId);
                return View(modelo);
            }
            var cuenta = await repositiorioCuentas.ObtenerPorId(modelo.CuentaId, usuarioId);

            if(cuenta is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }
            var categoria = await repositorioCategorias.ObtenerCategoriaId(modelo.CategoriaId, usuarioId);

            if (categoria is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }
            modelo.UsuarioId = usuarioId;

            if(modelo.TipoOperacionId == TipoOperacion.Gasto)
            {
                modelo.Monto *= -1;
            }
            await repositorioTransacciones.Crear(modelo);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Editar(int id,string UrlRetorno = null)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var transaccion = await repositorioTransacciones.ObtenerPorId(id,usuarioId);
            if(transaccion is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }

            var modelo = mapper.Map<TransaccionActualizacionViewModel>(transaccion);

            modelo.MontoAnterior = modelo.Monto;

            if (modelo.TipoOperacionId == TipoOperacion.Gasto)
            {
                modelo.MontoAnterior = modelo.Monto * -1;
            }

            modelo.CuentaAnteriorId = transaccion.CuentaId;
            modelo.Categorias = await ObtenerCategorias(usuarioId, transaccion.TipoOperacionId);
            modelo.Cuentas = await ObtenerCuentas(usuarioId);
            modelo.UrlRetorno = UrlRetorno;

            return View(modelo);
        }
        [HttpPost]
        public async Task<IActionResult> Editar(TransaccionActualizacionViewModel modelo)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                modelo.Categorias = await ObtenerCategorias(usuarioId, modelo.TipoOperacionId);
                modelo.Cuentas = await ObtenerCuentas(usuarioId);
                return View(modelo);
            }
            var cuenta = await repositiorioCuentas.ObtenerPorId(modelo.CuentaId, usuarioId);
            if (cuenta == null)
            {
                return RedirectToAction("NoEncontrado", "Home");

            }
            var categoria = await repositorioCategorias.ObtenerCategoriaId(modelo.CategoriaId, usuarioId);
            if (categoria == null)
            {
                return RedirectToAction("NoEncontrado", "Home");

            }
            var transaccion = await repositorioTransacciones.ObtenerPorId(modelo.Id, usuarioId);
            if (transaccion is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }

            var transaccionModelo = mapper.Map<Transaccion>(modelo);
            if (modelo.TipoOperacionId == TipoOperacion.Gasto)
            {
                transaccion.Monto *= -1;
            }

            await repositorioTransacciones.Actualizar(
                transaccionModelo, modelo.MontoAnterior, modelo.CuentaAnteriorId);

            if (string.IsNullOrEmpty(modelo.UrlRetorno))
            {
                return RedirectToAction("Index");
            }
            else
            {
                return LocalRedirect(modelo.UrlRetorno);
            }            
        }

        [HttpPost]
        public async Task<IActionResult> Borrar(int id, string urlRetorno = null)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var transaccion = await repositorioTransacciones.ObtenerPorId(id, usuarioId);
            if (transaccion is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }

            await repositorioTransacciones.Borrar(id);

            if (string.IsNullOrEmpty(urlRetorno))
            {
                return RedirectToAction("Index");
            }
            else
            {
                return LocalRedirect(urlRetorno);
            }         
        }
        private async Task<IEnumerable<SelectListItem>> ObtenerCuentas(int usuarioId)
        {
            var cuentas = await repositiorioCuentas.Buscar(usuarioId);
            return cuentas.Select(x => new SelectListItem(x.Nombre, x.Id.ToString()));
        }
        private async Task<IEnumerable<SelectListItem>> ObtenerCategorias(int usuarioId, 
            TipoOperacion tipoOperacion)
        {
            var categirias = await repositorioCategorias.Obtener(usuarioId,tipoOperacion);

            var resultado =  categirias.Select(x => new SelectListItem(x.Nombre, x.Id.ToString())).ToList();

            var opcionPorDefecto = new SelectListItem("-- Seleccione una categoria --", "0", true);

            resultado.Insert(0,opcionPorDefecto);

            return resultado;
        }

        [HttpPost]
        public async Task<IActionResult> ObtenerCategorias([FromBody] TipoOperacion tipoOperacion)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categorias = await ObtenerCategorias(usuarioId, tipoOperacion);

            return Ok(categorias);
            
        } 
    }
}
