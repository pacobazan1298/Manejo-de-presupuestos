using Dapper;
using ManejoPresupuesto.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;

namespace ManejoPresupuesto.Servicios
{
    public interface IRepositorioTiposCuentas
    {
        Task Actualizar(TipoCuenta tipoCuenta);
        Task Borrar(int id);
        Task Crear(TipoCuenta tipocuenta);
        Task<bool> Existe(string nombre, int usuarioId, int id = 0);
        Task<IEnumerable<TipoCuenta>> Obtener(int usuarioId);
        Task<TipoCuenta> ObtenerPorId(int id, int usuarioId);
        Task Ordenar(IEnumerable<TipoCuenta> tipoCuentasOrdenados);
        Task<bool> ValidarNombre(string nombre);
    }
    public class RepositorioTiposCuentas: IRepositorioTiposCuentas
    {
        public readonly string connectionString;
        public RepositorioTiposCuentas(IConfiguration configuration) 
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task Crear(TipoCuenta tipocuenta)
        {
            using var connection= new SqlConnection(connectionString);
            var id = await connection.QuerySingleAsync<int>
                                                    ("TiposCuentas_Insertar",
                                                    new {usuarioId = tipocuenta.UsuarioId,
                                                    nombre = tipocuenta.Nombre},
                                                    commandType: System.Data.CommandType.StoredProcedure);
            tipocuenta.Id = id;
        }

        public async Task<bool> Existe(string nombre, int usuarioId, int id = 0)
        {
            using var connection = new SqlConnection(connectionString);
            var existe = await connection.QueryFirstOrDefaultAsync<int>(@"SELECT 1 FROM TiposCuentas 
                                                                        WHERE Nombre = @Nombre AND UsuarioId = @UsuarioId AND Id <> @id",
                                                                        new { nombre, usuarioId, id });

            return existe == 1;
        }
        //CODIGO QUE HICE YO
        public async Task<bool> ValidarNombre(string nombre)
        {
            using var connection = new SqlConnection(connectionString);
            bool validacion = await connection.QuerySingleAsync<bool>("SELECT CAST(CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END AS BIT) as Resultado FROM TiposCuentas WHERE Nombre = '"+nombre+"'");
            
            return validacion;
        }   
        //----------------------------
        
        public async Task<IEnumerable<TipoCuenta>> Obtener(int usuarioId)
        {
            using var connection = new SqlConnection (connectionString);
            return await connection.QueryAsync<TipoCuenta>(@"SELECT Id,Nombre,Orden FROM TiposCuentas WHERE UsuarioId = @usuarioID ORDER BY Orden ASC",new {usuarioId});
        }

        public async Task Actualizar(TipoCuenta tipoCuenta)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.ExecuteAsync(@"UPDATE TiposCuentas SET
                                            Nombre = @Nombre
                                            WHERE Id = @Id", tipoCuenta);
        }

        public async Task<TipoCuenta> ObtenerPorId(int id, int usuarioId)
        {
            using var connection = new SqlConnection(connectionString);
            return await connection.QueryFirstOrDefaultAsync<TipoCuenta>(@"SELECT Id,Nombre,Orden FROM TiposCuentas 
                                                                   WHERE Id=@Id AND UsuarioId = @usuarioID",new {id,usuarioId});
        }

        public async Task Borrar(int id)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.ExecuteAsync("DELETE TiposCuentas WHERE Id = @Id", new {id});           
        }

        public async Task Ordenar(IEnumerable<TipoCuenta> tipoCuentasOrdenados)
        {
            var query = "UPDATE TiposCuentas SET Orden = @Orden WHERE Id = @Id;";

            using var connection = new SqlConnection(connectionString);

            await connection.ExecuteAsync(query, tipoCuentasOrdenados);
        }
    }
}
