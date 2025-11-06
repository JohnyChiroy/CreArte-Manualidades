// wwwroot/js/permisos-bulk-ui.js
window.PermisosBulk = (function () {
    function $all(sel, root) { return Array.from((root || document).querySelectorAll(sel)); }
    function $(sel, root) { return (root || document).querySelector(sel); }

    function markTouched(chk) {
        const row = chk.closest('tr');
        if (!row) return;
        const hidden = row.querySelector('input[type="hidden"][name$=".Touched"]');
        if (hidden) hidden.value = 'true';
        row.classList.add('row-touched');
    }

    function bindMasterCols(root, mode) {
        const colAllVer = $('#colAllVer', root);
        const colAllCrear = $('#colAllCrear', root);
        const colAllEditar = $('#colAllEditar', root);
        const colAllEliminar = $('#colAllEliminar', root);

        if (colAllVer) colAllVer.addEventListener('change', () => {
            $all('.chk-ver', root).forEach(ch => {
                if (mode === 'create' && ch.hasAttribute('disabled')) return;
                ch.checked = colAllVer.checked;
                markTouched(ch);
            });
        });
        if (colAllCrear) colAllCrear.addEventListener('change', () => {
            $all('.chk-crear', root).forEach(ch => {
                if (mode === 'create' && ch.hasAttribute('disabled')) return;
                ch.checked = colAllCrear.checked;
                markTouched(ch);
            });
        });
        if (colAllEditar) colAllEditar.addEventListener('change', () => {
            $all('.chk-editar', root).forEach(ch => {
                if (mode === 'create' && ch.hasAttribute('disabled')) return;
                ch.checked = colAllEditar.checked;
                markTouched(ch);
            });
        });
        if (colAllEliminar) colAllEliminar.addEventListener('change', () => {
            $all('.chk-eliminar', root).forEach(ch => {
                if (mode === 'create' && ch.hasAttribute('disabled')) return;
                ch.checked = colAllEliminar.checked;
                markTouched(ch);
            });
        });
    }

    function bindRowTouches(root) {
        $all('.chk-ver,.chk-crear,.chk-editar,.chk-eliminar', root).forEach(ch => {
            ch.addEventListener('change', () => markTouched(ch));
        });
    }

    function init(opts) {
        const mode = (opts && opts.mode) || 'create'; // 'create'|'edit'
        const root = document;
        bindMasterCols(root, mode);
        if (mode === 'edit') bindRowTouches(root);
    }

    return { init };
})();
