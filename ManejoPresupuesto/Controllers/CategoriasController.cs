using ManejoPresupuesto.Models;
using ManejoPresupuesto.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ManejoPresupuesto.Controllers
{
    public class CategoriasController: Controller
    {
        private readonly IservicioUsuarios servicioUsuarios;
        private readonly IRepositorioCategorias repositorioCategorias;

        public CategoriasController(IservicioUsuarios servicioUsuarios, IRepositorioCategorias repositorioCategorias)
        {
            this.servicioUsuarios = servicioUsuarios;
            this.repositorioCategorias = repositorioCategorias;
        }
        public async Task<IActionResult> Index(PaginacionViewModel paginacion)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categorias = await repositorioCategorias.Obtener(usuarioId, paginacion);
            var totalCategorias = await repositorioCategorias.Contar(usuarioId);

            var respuestaVM = new PaginacionRespuesta<Categoria>
            {
                Elementos = categorias,
                Pagina = paginacion.Pagina,
                CantidadTotalRecords = totalCategorias,
                RecordsPorPagina = paginacion.RecordsPorPagina,
                BaseUrl = Url.Action()
            };
            return View(respuestaVM);
        }
        public IActionResult Crear()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Crear(Categoria categoria)
        {
            if(!ModelState.IsValid)
            {
                return View(categoria);
            }
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            categoria.UsuarioId = usuarioId;
            await repositorioCategorias.Crear(categoria);

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Editar(int id)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categoria = await repositorioCategorias.ObtenerCategoriaId(id, usuarioId);

            if(categoria is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }            
            return View(categoria);
        }
        [HttpPost]
        public async Task<IActionResult> Editar(Categoria categoriaEditar)
        {
            if (!ModelState.IsValid)
            {
                return View(categoriaEditar);
            }

            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categoria = await repositorioCategorias.ObtenerCategoriaId(categoriaEditar.Id, usuarioId);

            if(categoria is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }
            categoriaEditar.UsuarioId = usuarioId;
            await repositorioCategorias.Actualizar(categoriaEditar);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Borrar(int id)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categoria = await repositorioCategorias.ObtenerCategoriaId(id, usuarioId);

            if (categoria is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }            
            return View(categoria);
        }

        [HttpPost]
        public async Task<IActionResult> BorrarCategoria(int id)
        {
            var usuarioId = servicioUsuarios.ObtenerUsuarioId();
            var categoria = await repositorioCategorias.ObtenerCategoriaId(id, usuarioId);

            if (categoria is null)
            {
                return RedirectToAction("NoEncontrado", "Home");
            }
            await repositorioCategorias.Borrar(categoria.Id);
            return RedirectToAction("Index");
        }
    }
}
