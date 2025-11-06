// wwwroot/js/permisos-index.js
(function () {
    const btnVer = document.getElementById('btnVer');
    const btnEditar = document.getElementById('btnEditar');
    const btnNuevo = document.getElementById('btnNuevo');

    const btnEstadoApply = document.getElementById('btnEstadoApply');
    const btnEstadoClear = document.getElementById('btnEstadoClear');
    const selEstado = document.getElementById('fEstado');

    let selectedRol = null;

    function toggleButtons(enable) {
        if (btnVer) { btnVer.classList.toggle('is-disabled', !enable); btnVer.setAttribute('aria-disabled', (!enable).toString()); }
        if (btnEditar) { btnEditar.classList.toggle('is-disabled', !enable); btnEditar.setAttribute('aria-disabled', (!enable).toString()); }
    }
    toggleButtons(false);

    function applySelection(row) {
        document.querySelectorAll('.ua-rowitem').forEach(r => r.classList.remove('row-selected'));
        row.classList.add('row-selected');
        document.querySelectorAll('.ua-rowchk').forEach(c => {
            if (c.closest('.ua-rowitem') !== row) c.checked = false;
        });
        selectedRol = row.getAttribute('data-rol') || null;
        toggleButtons(!!selectedRol);
    }

    document.addEventListener('click', function (e) {
        const row = e.target.closest('.ua-rowitem');
        if (!row) return;
        if (e.target.closest('a,button,select,textarea,label,input')) return;
        const chk = row.querySelector('.ua-rowchk');
        if (chk) { chk.checked = true; chk.dispatchEvent(new Event('change', { bubbles: true })); }
    });

    document.addEventListener('change', function (e) {
        const chk = e.target.closest('.ua-rowchk');
        if (!chk) return;
        const row = chk.closest('.ua-rowitem'); if (!row) return;
        if (chk.checked) applySelection(row);
        else { selectedRol = null; toggleButtons(false); row.classList.remove('row-selected'); }
    });

    if (btnVer) {
        btnVer.addEventListener('click', function (e) {
            e.preventDefault();
            if (!selectedRol) { if (window.Swal) Swal.fire("Seleccione un registro", "Elija un rol para ver.", "info"); return; }
            window.location.href = '/Permisos/EditBulk?rolId=' + encodeURIComponent(selectedRol);
        });
    }
    if (btnEditar) {
        btnEditar.addEventListener('click', function (e) {
            e.preventDefault();
            if (!selectedRol) { if (window.Swal) Swal.fire("Seleccione un registro", "Elija un rol para modificar.", "info"); return; }
            window.location.href = '/Permisos/EditBulk?rolId=' + encodeURIComponent(selectedRol);
        });
    }
    if (btnNuevo) {
        btnNuevo.addEventListener('click', function (e) {
            if (selectedRol) {
                e.preventDefault();
                window.location.href = '/Permisos/Create?rolId=' + encodeURIComponent(selectedRol);
            }
        });
    }

    // Filtro ESTADO (usa helpers de ua-common)
    if (btnEstadoApply && selEstado) {
        btnEstadoApply.addEventListener('click', function () {
            window.uaSubmitFilter({ estado: selEstado.value || null });
        });
    }
    if (btnEstadoClear) {
        btnEstadoClear.addEventListener('click', function () {
            window.uaSubmitFilter({ estado: null });
        });
    }
})();
