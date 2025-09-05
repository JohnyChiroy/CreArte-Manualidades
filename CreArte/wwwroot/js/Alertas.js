// /wwwroot/js/alerts.js
// Módulo global con helpers de SweetAlert2 reutilizables.
// Queda disponible como window.KarySwal en toda la app.
window.KarySwal = (function () {

    /**
     * Modal de éxito con una o dos acciones.
     * Por defecto: 1 botón (Aceptar → indexUrl).
     * Si necesitas 2 botones, pasa showDenyButton: true y createUrl.
     */
    function saveSuccess(opts) {
        const o = Object.assign({
            title: '¡Guardado!',
            text: 'La operación se realizó correctamente.',
            icon: 'success',
            indexUrl: '#',
            createUrl: null,          // por defecto null
            confirmText: 'Aceptar',   // por defecto 1 botón
            denyText: 'Guardar y nuevo',
            showDenyButton: false     // por defecto, NO mostrar 2do botón
        }, opts || {});

        return Swal.fire({
            icon: o.icon,
            title: o.title,
            text: o.text,
            showDenyButton: o.showDenyButton && !!o.createUrl, // sólo si lo pides y hay URL
            confirmButtonText: o.confirmText,
            denyButtonText: o.denyText,
            allowOutsideClick: false,
            allowEscapeKey: false
        }).then(r => {
            if (r.isConfirmed && o.indexUrl) window.location.href = o.indexUrl;
            else if (r.isDenied && o.createUrl) window.location.href = o.createUrl;
        });
    } // 👈 IMPORTANTE: este cierre faltaba

    /**
     * Protege un formulario de salidas accidentales si hay cambios sin guardar.
     * - formSelector: selector del <form> (por ejemplo, "#frmArea")
     * - leaveSelector: selector de elementos que "salen" (p.ej. ".js-leave")
     */
    function guardUnsaved(formSelector, leaveSelector) {
        const form = document.querySelector(formSelector);
        if (!form) return;

        let isDirty = false;
        let isSubmitting = false;

        // Marcar el form como "sucio" al cambiar algo
        form.addEventListener('input', () => { isDirty = true; }, { capture: true });
        form.addEventListener('change', () => { isDirty = true; }, { capture: true });

        // Si se envía, ya no preguntamos
        form.addEventListener('submit', () => { isSubmitting = true; });

        // Navegación del navegador (cerrar/recargar/back)
        window.addEventListener('beforeunload', (e) => {
            if (isDirty && !isSubmitting) {
                e.preventDefault();
                e.returnValue = '';
            }
        });

        // Interceptar salidas internas (links/botones)
        document.querySelectorAll(leaveSelector).forEach(el => {
            el.addEventListener('click', (ev) => {
                if (!isDirty) return; // Sin cambios, pasar directo
                ev.preventDefault();

                // Soporta <a href> y data-href en <button>
                const href = el.getAttribute('href') || el.getAttribute('data-href') || '#';

                Swal.fire({
                    icon: 'warning',
                    title: 'Cambios sin guardar',
                    text: 'Si sales ahora, los cambios no se guardarán.',
                    showCancelButton: true,
                    confirmButtonText: 'Salir sin guardar',
                    cancelButtonText: 'Seguir editando'
                }).then(r => {
                    if (r.isConfirmed && href && href !== '#') window.location.href = href;
                });
            });
        });
    }

    /**
     * Mensaje informativo simple (para "No se realizó ningún cambio", etc.)
     */
    function info(opts) {
        const o = Object.assign({
            title: 'Información',
            text: '',
            icon: 'info',
            confirmText: 'Aceptar',
            redirectUrl: null
        }, opts || {});
        return Swal.fire({
            icon: o.icon,
            title: o.title,
            text: o.text,
            confirmButtonText: o.confirmText
        }).then(r => {
            if (r.isConfirmed && o.redirectUrl) {
                window.location.href = o.redirectUrl;
            }
        });
    }

    return { saveSuccess, guardUnsaved, info };
})();
