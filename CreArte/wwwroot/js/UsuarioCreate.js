        // Referencias a controles
        const pwd = document.getElementById('pwd');
        const pwd2 = document.getElementById('pwd2');
        const save = document.getElementById('btnSave');
        const chkLower = document.getElementById('chkLower');
        const chkUpper = document.getElementById('chkUpper');
        const chkDigit = document.getElementById('chkDigit');
        const chkLen   = document.getElementById('chkLen');
        const pwdMsg = document.getElementById('pwdMsg');
        const pwd2Msg = document.getElementById('pwd2Msg');

        // Reglas
        const rxLower = /[a-z]/;
        const rxUpper = /[A-Z]/;
        const rxDigit = /[0-9]/;

        // Evalúa reglas y habilita/deshabilita Guardar
        function validateAll(){
            const v = pwd.value ?? '';

            const okLower = rxLower.test(v);
            const okUpper = rxUpper.test(v);
            const okDigit = rxDigit.test(v);
            const okLen   = v.length >= 8 && v.length <= 15;

            toggleCheck(chkLower, okLower);
            toggleCheck(chkUpper, okUpper);
            toggleCheck(chkDigit, okDigit);
            toggleCheck(chkLen,   okLen);

            const match = v.length > 0 && v === (pwd2.value ?? '');
            pwd2Msg.textContent = match ? '' : 'Las contraseñas no coinciden.';

            const ready = okLower && okUpper && okDigit && okLen && match
                       && document.querySelector('[name="USUARIO_NOMBRE"]').value.trim().length > 0
                       && document.querySelector('[name="ROL_ID"]').value.trim().length > 0
                       && document.querySelector('[name="EMPLEADO_ID"]').value.trim().length > 0;

            save.disabled = !ready;
            save.classList.toggle('enabled', ready);
        }

        function toggleCheck(el, ok){
            if(ok){ el.classList.add('ok'); }
            else { el.classList.remove('ok'); }
        }

        // Listeners
        pwd.addEventListener('input', validateAll);
        pwd2.addEventListener('input', validateAll);
        document.querySelector('[name="USUARIO_NOMBRE"]').addEventListener('input', validateAll);
        document.querySelector('[name="ROL_ID"]').addEventListener('change', validateAll);
        document.querySelector('[name="EMPLEADO_ID"]').addEventListener('change', validateAll);

        // Init
        validateAll();