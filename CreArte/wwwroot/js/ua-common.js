// wwwroot/js/ua-common.js
(function () {
    // Cerrar popovers al hacer click fuera
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.ua-popover-wrap') && !e.target.closest('.ua-popover')) {
            document.querySelectorAll('.ua-popover').forEach(p => p.classList.remove('open'));
        }
    });

    // Abrir popover
    document.querySelectorAll('.ua-filter-btn').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const id = this.getAttribute('data-pop');
            const pop = document.getElementById(id);
            if (!pop) return;
            const openNow = pop.classList.contains('open');
            document.querySelectorAll('.ua-popover').forEach(p => p.classList.remove('open'));
            if (!openNow) pop.classList.add('open');
        });
    });

    // Helpers de URL / filtros (expuestos globalmente)
    window.uaGetUrl = function () {
        return new URL(window.location.href);
    };
    window.uaSubmitFilter = function (params) {
        const url = uaGetUrl();
        const q = url.searchParams;
        q.set('page', '1');
        Object.keys(params).forEach(k => {
            const v = params[k];
            if (v === null || v === undefined || v === '') q.delete(k);
            else q.set(k, v);
        });
        window.location.href = url.pathname + '?' + q.toString();
    };
})();
