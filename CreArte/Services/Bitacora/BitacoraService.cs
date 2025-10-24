using CreArte.Data;
using CreArte.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CreArte.Services.Bitacora
{
    public interface IBitacoraService
    {
        Task LogAsync(string tabla, string operacion, string usuario, string detalle, CancellationToken ct = default);
    }

    public class BitacoraService : IBitacoraService
    {
        private readonly CreArteDbContext _db;
        public BitacoraService(CreArteDbContext db) => _db = db;

        // Genera un ID único sin depender de MAX (evita errores)
        private static string NewBitacoraId()
            => $"BIT{DateTime.UtcNow.Ticks}";

        public async Task LogAsync(string tabla, string operacion, string usuario, string detalle, CancellationToken ct = default)
        {
            _db.BITACORA.Add(new BITACORA
            {
                BITACORA_ID = NewBitacoraId(),
                ACCION = operacion,                        // INSERT / UPDATE / DELETE
                DESCRIPCION = $"{tabla} | {detalle}",           // guardamos tabla + detalle
                FECHA_ACCION = DateTime.Now,
                USUARIO_CREACION = usuario,
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            });

            await _db.SaveChangesAsync(ct);
        }
    }
}
