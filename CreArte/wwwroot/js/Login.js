function updateDateTime() {
    const now = new Date();
    const options = { year: 'numeric', month: 'short', day: 'numeric' };
    document.getElementById("currentDate").innerText = now.toLocaleDateString('es-ES', options);
    document.getElementById("currentTime").innerText = now.toLocaleTimeString('es-ES', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
}

function togglePassword() {
    const passInput = document.getElementById("clave");
    passInput.type = passInput.type === "password" ? "text" : "password";
}

updateDateTime();
setInterval(updateDateTime, 1000);
