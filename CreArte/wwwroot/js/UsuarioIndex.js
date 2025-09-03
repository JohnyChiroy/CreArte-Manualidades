/* =========================
    POPovers: abrir/cerrar + activar iconos
    ========================= */
    (function() {
        document.querySelectorAll('.ua-filter-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                const id = this.getAttribute('data-pop');
                const pop = document.getElementById(id);
                const openNow = pop.classList.contains('open');
                document.querySelectorAll('.ua-popover').forEach(p => p.classList.remove('open'));
                if (!openNow) { pop.classList.add('open'); }
            });
        });
    document.addEventListener('click', function(e){
    if(!e.target.closest('.ua-popover-wrap') && !e.target.closest('.ua-popover')){
        document.querySelectorAll('.ua-popover').forEach(p => p.classList.remove('open'));
    }
  });
    function setActive(sel, val){ const b=document.querySelector(sel); if(b){b.classList.toggle('active', !!val); } }
    setActive('[data-pop="popUsuario"]', document.getElementById('ua_val_usuario')?.value);
    setActive('[data-pop="popFecha"]',   document.getElementById('ua_val_fi')?.value || document.getElementById('ua_val_ff')?.value);
    setActive('[data-pop="popRol"]',     document.getElementById('ua_val_rol')?.value);
    setActive('[data-pop="popEstado"]',  document.getElementById('ua_val_estado')?.value);
})();

    /* =========================
       Aplicar / Limpiar filtros (preserva Sort/Dir)
       ========================= */
    function submitFilters(extra){
  const f = document.getElementById('uaFilterForm');

  // 1) Elimina pares previos para evitar duplicados (incluye Sort/Dir)
  ['Usuario','FechaInicio','FechaFin','Rol','Estado','Sort','Dir'].forEach(n=>{
        f.querySelectorAll(`[name="${n}"]`).forEach(nod => nod.remove());
  });

    // 2) Mantiene orden actual leyendo los hidden del form
    const currentSort = '@(Model.Sort ?? "fecha")';
    const currentDir  = '@(Model.Dir  ?? "desc")';
    let s = document.createElement('input'); s.type='hidden'; s.name='Sort'; s.value=currentSort; f.appendChild(s);
    let d = document.createElement('input'); d.type='hidden'; d.name='Dir';  d.value=currentDir;  f.appendChild(d);

    // 3) Agrega filtros enviados por la acción del popover
    for(const k in extra){
    const input = document.createElement('input');
    input.type='hidden'; input.name=k; input.value=extra[k] ?? '';
    f.appendChild(input);
  }

    // 4) Reinicia página y envía
    f.querySelector('input[name="Page"]').value = 1;
    f.submit();
}

    /* --- USUARIO --- */
    function uaApplyUsuario(){
  const txt = document.getElementById('fUsuario').value;
    const opt = (document.querySelector('input[name="optU"]:checked')||{ }).value || 'all';
    let val = txt;
    if(opt==='blanks')     val = '__BLANKS__';
    if(opt==='nonblanks')  val = '__NONBLANKS__';
    submitFilters({
        Usuario: val,
    FechaInicio: document.getElementById('fIni')?.value,
    FechaFin:    document.getElementById('fFin')?.value,
    Rol:         document.getElementById('fRol')?.value,
    Estado:      document.getElementById('fEstado')?.value
  });
}
    function uaClearUsuario(){
        document.getElementById('fUsuario').value = '';
    submitFilters({
        Usuario: '',
    FechaInicio: document.getElementById('fIni')?.value,
    FechaFin:    document.getElementById('fFin')?.value,
    Rol:         document.getElementById('fRol')?.value,
    Estado:      document.getElementById('fEstado')?.value
  });
}

    /* --- FECHA --- */
    function uaApplyFecha(){
        submitFilters({
            Usuario: document.getElementById('fUsuario')?.value,
            FechaInicio: document.getElementById('fIni')?.value,
            FechaFin: document.getElementById('fFin')?.value,
            Rol: document.getElementById('fRol')?.value,
            Estado: document.getElementById('fEstado')?.value
        });
}
    function uaClearFecha(){
        document.getElementById('fIni').value = ''; document.getElementById('fFin').value='';
    uaApplyFecha();
}

    /* --- ROL --- */
    function uaApplyRol(){
        submitFilters({
            Usuario: document.getElementById('fUsuario')?.value,
            FechaInicio: document.getElementById('fIni')?.value,
            FechaFin: document.getElementById('fFin')?.value,
            Rol: document.getElementById('fRol')?.value,
            Estado: document.getElementById('fEstado')?.value
        });
}
    function uaClearRol(){document.getElementById('fRol').value = ''; uaApplyRol(); }

    /* --- ESTADO --- */
    function uaApplyEstado(){
        submitFilters({
            Usuario: document.getElementById('fUsuario')?.value,
            FechaInicio: document.getElementById('fIni')?.value,
            FechaFin: document.getElementById('fFin')?.value,
            Rol: document.getElementById('fRol')?.value,
            Estado: document.getElementById('fEstado')?.value
        });
}
    function uaClearEstado(){document.getElementById('fEstado').value = ''; uaApplyEstado(); }

    /* Enter en el input de usuario = aplicar */
    document.addEventListener('keydown', function(e){
  if(e.key==='Enter' && e.target && e.target.id==='fUsuario'){e.preventDefault(); uaApplyUsuario(); }
});
