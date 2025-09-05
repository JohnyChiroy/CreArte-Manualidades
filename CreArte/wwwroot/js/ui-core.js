// ======================================================
// POPovers AREAS — patrón estable (mousedown + flag)
// - Abre en mousedown (más rápido, evita blur/cierre inmediato)
// - "Click fuera" en document con protección justOpened
// - Fuerza display:block al abrir por si el CSS externo lo oculta
// - Cierra sólo si el click es realmente fuera de wrap/popover/botón
// ======================================================
(function () {
    const OPEN_CLASS = 'open';
    let activePopover = null;       // referencia al <div.ua-popover> abierto
    let justOpened = false;         // flag para ignorar el primer click global
    let outsideHandlerBound = false;

    // Cierra el popover actual
    function closePopover() {
        if (!activePopover) return;
        activePopover.classList.remove(OPEN_CLASS);
        activePopover.style.display = 'none'; // aseguramos ocultarlo
        activePopover = null;
    }

    // Abre un popover específico
    function openPopover(btn, pop) {
        // cierra otro abierto, si hay
        closePopover();

        // asegura stacking correcto
        const wrap = btn.closest('.ua-popover-wrap');
        if (wrap && getComputedStyle(wrap).position === 'static') {
            wrap.style.position = 'relative';
        }

        // abre y fuerza display:block (por si otra hoja de estilos lo pisa)
        pop.classList.add(OPEN_CLASS);
        pop.style.display = 'block';
        activePopover = pop;

        // clicks dentro del popover NO deben cerrar
        pop.addEventListener('mousedown', (ev) => ev.stopPropagation());
        pop.addEventListener('click', (ev) => ev.stopPropagation());

        // foco amable al primer input
        const first = pop.querySelector('input, select, button, textarea');
        if (first) setTimeout(() => first.focus(), 0);

        // marca que "acabamos de abrir" para ignorar el siguiente mousedown global
        justOpened = true;

        // asegura que tenemos el outside handler (una sola vez)
        if (!outsideHandlerBound) {
            document.addEventListener('mousedown', onGlobalMouseDown, true);
            outsideHandlerBound = true;
        }
    }

    // Cierre por click fuera
    function onGlobalMouseDown(e) {
        // si acabamos de abrir, ignoramos este primer mousedown global
        if (justOpened) {
            justOpened = false;
            return;
        }
        // si clic dentro de popover o dentro del wrap o en el mismo botón => no cerrar
        if (e.target.closest('.ua-popover')) return;
        if (e.target.closest('.ua-popover-wrap')) return;
        if (e.target.closest('.ua-filter-btn')) return;

        // fuera => cerrar
        closePopover();
    }

    // Botones de filtro: abrimos en MOUSEDOWN (no en click)
    document.querySelectorAll('.ua-filter-btn').forEach(btn => {
        btn.setAttribute('type', 'button'); // seguridad: evita submit si está en un <form>
        btn.addEventListener('mousedown', function (e) {
            e.preventDefault();
            e.stopPropagation();

            const id = this.getAttribute('data-pop');
            const pop = document.getElementById(id);
            if (!pop) return;

            // toggle
            const isOpen = pop.classList.contains(OPEN_CLASS) && pop.style.display !== 'none';
            if (isOpen) {
                closePopover();
            } else {
                openPopover(this, pop);
            }
        });
    });

    // Cerrar con ESC
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closePopover();
    });

    // Marcar botones como "active" si hay filtros aplicados (igual que antes)
    function setActive(sel, val) { const b = document.querySelector(sel); if (b) { b.classList.toggle('active', !!val); } }
    setActive('[data-pop="popArea"]', document.getElementById('ua_val_area')?.value);
    setActive('[data-pop="popFecha"]', document.getElementById('ua_val_fi')?.value || document.getElementById('ua_val_ff')?.value);
    setActive('[data-pop="popEstado"]', document.getElementById('ua_val_estado')?.value);
})();

/* ============================================
   Envío de filtros (preserva Sort/Dir) – AREAS
   (igual que teníamos, sin tocar)
   ============================================ */
function submitFilters(extra) {
    const f = document.getElementById('uaFilterForm');

    // 1) Limpia pares previos
    ['Area', 'FechaInicio', 'FechaFin', 'Estado', 'Sort', 'Dir'].forEach(n => {
        f.querySelectorAll(`[name="${n}"]`).forEach(nod => nod.remove());
    });

    // 2) Mantén orden actual (inyectado por Razor)
    const currentSort = '@(Model.Sort ?? "fecha")';
    const currentDir = '@(Model.Dir  ?? "desc")';
    let s = document.createElement('input'); s.type = 'hidden'; s.name = 'Sort'; s.value = currentSort; f.appendChild(s);
    let d = document.createElement('input'); d.type = 'hidden'; d.name = 'Dir'; d.value = currentDir; f.appendChild(d);

    // 3) Agrega filtros recibidos
    for (const k in extra) {
        const input = document.createElement('input');
        input.type = 'hidden'; input.name = k; input.value = extra[k] ?? '';
        f.appendChild(input);
    }

    // 4) Reinicia página y envía
    f.querySelector('input[name="Page"]').value = 1;
    f.submit();
}

/* --- ÁREA --- */
function uaApplyArea() {
    const txt = (document.getElementById('fArea')?.value || '').trim();
    const opt = (document.querySelector('input[name="optA"]:checked') || {}).value || 'all';
    let val = txt;
    if (opt === 'blanks') val = '__BLANKS__';
    if (opt === 'nonblanks') val = '__NONBLANKS__';
    submitFilters({
        Area: val,
        FechaInicio: document.getElementById('fIni')?.value,
        FechaFin: document.getElementById('fFin')?.value,
        Estado: document.getElementById('fEstado')?.value
    });
}
function uaClearArea() {
    const i = document.getElementById('fArea'); if (i) i.value = '';
    submitFilters({
        Area: '',
        FechaInicio: document.getElementById('fIni')?.value,
        FechaFin: document.getElementById('fFin')?.value,
        Estado: document.getElementById('fEstado')?.value
    });
}

/* --- FECHA --- */
function uaApplyFecha() {
    submitFilters({
        Area: document.getElementById('fArea')?.value,
        FechaInicio: document.getElementById('fIni')?.value,
        FechaFin: document.getElementById('fFin')?.value,
        Estado: document.getElementById('fEstado')?.value
    });
}
function uaClearFecha() {
    const i = document.getElementById('fIni'), f = document.getElementById('fFin');
    if (i) i.value = ''; if (f) f.value = '';
    uaApplyFecha();
}

/* --- ESTADO --- */
function uaApplyEstado() {
    submitFilters({
        Area: document.getElementById('fArea')?.value,
        FechaInicio: document.getElementById('fIni')?.value,
        FechaFin: document.getElementById('fFin')?.value,
        Estado: document.getElementById('fEstado')?.value
    });
}
function uaClearEstado() { const e = document.getElementById('fEstado'); if (e) e.value = ''; uaApplyEstado(); }

/* Enter en el input de área = aplicar */
document.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && e.target && e.target.id === 'fArea') { e.preventDefault(); uaApplyArea(); }
});