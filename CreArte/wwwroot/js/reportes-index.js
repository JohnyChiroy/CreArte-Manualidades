// =======================================
// Reportes INDEX: carga partials con fetch
// =======================================

document.addEventListener('DOMContentLoaded', () => {
    // 1) Selecciona todas las tarjetas
    const cards = document.querySelectorAll('.ua-card-reporte');
    const modalEl = document.getElementById('reporteModal');
    const modalBody = document.getElementById('reporteModalBody');
    const modalTitle = document.getElementById('reporteModalLabel');

    // 2) Instancia el modal de Bootstrap
    const bsModal = new bootstrap.Modal(modalEl);

    // 3) Para cada tarjeta, configura el click
    cards.forEach(card => {
        card.addEventListener('click', async () => {
            // Limpia contenido previo
            modalBody.innerHTML = '<div class="text-center py-5">Cargando…</div>';

            // Título según la tarjeta (lee el texto .ua-title)
            const title = card.querySelector('.ua-title')?.textContent?.trim() || 'Reporte';
            modalTitle.textContent = `Reporte de ${title}`;

            // Obtiene el endpoint del data-attribute
            const endpoint = card.getAttribute('data-endpoint');
            try {
                // Pide el Partial al servidor
                const res = await fetch(endpoint, { credentials: 'same-origin' });
                const html = await res.text();
                modalBody.innerHTML = html;

                // Importante: cuando llega el HTML del partial, ese HTML contiene
                // <script> que arma las gráficas Chart.js. Al inyectarse como texto,
                // el script no se ejecuta automáticamente. Aquí lo forzamos:
                modalBody.querySelectorAll('script').forEach(scr => {
                    const s = document.createElement('script');
                    s.text = scr.text;
                    document.body.appendChild(s);
                    // Limpieza opcional
                    setTimeout(() => s.remove(), 0);
                });

            } catch (err) {
                modalBody.innerHTML = `<div class="text-danger">Error al cargar el reporte.</div>`;
                console.error(err);
            }

            // Abre el modal
            bsModal.show();
        });
    });
});
