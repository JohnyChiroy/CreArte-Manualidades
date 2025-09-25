// Toggle del sidebar (colapsa/expande agregando una clase al body)
(function () {
    const btn = document.getElementById('btnToggle');
    if (!btn) return;

    btn.addEventListener('click', function () {
        document.body.classList.toggle('sidebar-collapsed');
    });
})();

//subopciones de sidebar(abre / cierra submen�s)

// === Sidebar: recordar estado de "Configuraci�n" (con Bootstrap Collapse) ===
// Guarda/lee 'sidebar.config.open' en localStorage -> "1" (abierto) o "0" (cerrado)
//document.addEventListener('DOMContentLoaded', function () {
//    // Referencias a elementos del DOM
//    const toggle = document.getElementById('cfg-toggle'); // bot�n padre "Configuraci�n"
//    const sub = document.getElementById('cfg-sub');    // contenedor del submen�

//    // Si no existen (por ejemplo en alguna vista sin sidebar), terminamos
//    if (!toggle || !sub) return;

//    // Detectamos si estamos en uno de los controladores hijos para forzar apertura
//    const isInChild = ['areas', 'niveles', 'roles', 'categorias', 'empresa']
//        .includes((document.body.getAttribute('data-current-controller') || '').toLowerCase());

//    // Leemos preferencia guardada del usuario
//    const saved = localStorage.getItem('sidebar.config.open');

//    if (!isInChild && saved !== null) {
//        // Forzamos estado seg�n preferencia previa (s�lo si no estamos en un hijo)
//        const shouldOpen = saved === '1';
//        const c = bootstrap.Collapse.getOrCreateInstance(sub, { toggle: false }); // no toggle autom�tico
//        if (shouldOpen) {
//            c.show();
//            toggle.classList.add('is-open');
//            toggle.setAttribute('aria-expanded', 'true');
//        } else {
//            c.hide();
//            toggle.classList.remove('is-open');
//            toggle.setAttribute('aria-expanded', 'false');
//        }
//    }

//    // Cuando se muestra el submen�, guardamos "1"
//    sub.addEventListener('shown.bs.collapse', function () {
//        toggle.classList.add('is-open');
//        toggle.setAttribute('aria-expanded', 'true');
//        localStorage.setItem('sidebar.config.open', '1');
//    });

//    // Cuando se oculta el submen�, guardamos "0"
//    sub.addEventListener('hidden.bs.collapse', function () {
//        toggle.classList.remove('is-open');
//        toggle.setAttribute('aria-expanded', 'false');
//        localStorage.setItem('sidebar.config.open', '0');
//    });
//});
