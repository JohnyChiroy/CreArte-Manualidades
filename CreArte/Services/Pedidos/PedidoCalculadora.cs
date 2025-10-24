using CreArte.Models;
using System;

namespace CreArte.Services.Pedidos
{
    public static class PedidoCalculadora
    {
        public static void RecalcularTotalesYAnticipo(PEDIDO pedido)
        {
            decimal total = 0m;
            foreach (var d in pedido.DETALLE_PEDIDO)
            {
                var precio = d.PRECIO_PEDIDO ?? 0m;
                d.SUBTOTAL = Math.Round(d.CANTIDAD * precio, 2); // si CANTIDAD es int en tu entidad, EF hace la conversión implícita a decimal aquí
                total += d.SUBTOTAL;
            }

            pedido.TOTAL_PEDIDO = Math.Round(total, 2);
            pedido.REQUIERE_ANTICIPO = pedido.TOTAL_PEDIDO >= 300m;
            pedido.ANTICIPO_MINIMO = pedido.REQUIERE_ANTICIPO ? Math.Round(pedido.TOTAL_PEDIDO * 0.25m, 2) : 0m;

            if (pedido.REQUIERE_ANTICIPO && string.IsNullOrWhiteSpace(pedido.ANTICIPO_ESTADO))
                pedido.ANTICIPO_ESTADO = "PENDIENTE";
        }
    }
}
