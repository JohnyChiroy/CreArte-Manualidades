// Toggle del sidebar (colapsa/expande agregando una clase al body)
(function () {
    const btn = document.getElementById('btnToggle');
    if (!btn) return;

    // obtenemos el <i> dentro del bot�n
    const icon = btn.querySelector("i");

    btn.addEventListener('click', function () {
        // alternamos la clase del body
        document.body.classList.toggle('sidebar-collapsed');

        // si el body est� colapsado, mostramos el �cono "left"
        if (document.body.classList.contains("sidebar-collapsed")) {
            icon.classList.remove("bi-text-indent-right");
            icon.classList.add("bi-text-indent-left");
        } else {
            // si no, mostramos el �cono "right"
            icon.classList.remove("bi-text-indent-left");
            icon.classList.add("bi-text-indent-right");
        }
    });
})();
