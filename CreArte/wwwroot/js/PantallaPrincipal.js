// Toggle del sidebar (colapsa/expande agregando una clase al body)
(function () {
    const btn = document.getElementById('btnToggle');
    if (!btn) return;

    btn.addEventListener('click', function () {
        document.body.classList.toggle('sidebar-collapsed');
    });
})();
