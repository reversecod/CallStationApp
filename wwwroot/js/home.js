const cronometrosAtivos = [];

document.getElementById("btnNovoChamado").addEventListener("click", async () => {
    const token = document.getElementById("requestVerificationToken").value;
    try {
        const response = await fetch("?handler=NovoChamado", {
            method: "POST",
            headers: {
                "RequestVerificationToken": token
            }
        });
        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }
        const data = await response.json();
        if (data.success) {
            const container = document.getElementById("chamados-container");

            // Criação do novo elemento (permanece igual)
            const chamadoDiv = document.createElement("div");
            chamadoDiv.className = "ticket-card";

            const img = document.createElement("img");
            img.src = "/images/logoticket.png";
            img.alt = "Ticket";
            img.className = "ticket-icon";
            img.loading = "lazy";

            const spanOuter = document.createElement("span");
            spanOuter.textContent = `${data.status} - `;

            const spanCronometro = document.createElement("span");
            spanCronometro.className = "cronometro";
            spanCronometro.dataset.criadoEm = new Date(data.criadoEm).toISOString();
            spanCronometro.textContent = "00:00:00:00";  // Inicial ajustado para o novo formato

            spanOuter.appendChild(spanCronometro);
            chamadoDiv.appendChild(img);
            chamadoDiv.appendChild(spanOuter);

            // Encontra o div do botão (para inserir antes dele)
            const buttonDiv = container.querySelector('.d-flex.align-items-center');

            // Insere o novo chamado antes do div do botão (no final da lista)
            container.insertBefore(chamadoDiv, buttonDiv);

            // Adiciona o novo span ao array de ativos
            cronometrosAtivos.push(spanCronometro);
        } else {
            alert(data.message || "Erro ao criar chamado");
        }
    } catch (error) {
        console.error("Erro na requisição:", error);
        alert("Falha ao criar chamado: " + error.message);
    }
});

function atualizarCronometros() {
    const agora = new Date();

    cronometrosAtivos.forEach(span => {
        const criadoEm = new Date(span.dataset.criadoEm);
        if (isNaN(criadoEm.getTime())) {
            span.textContent = "00:00:00:00";
            return;
        }

        const diffMs = agora - criadoEm;
        const totalSegundos = Math.floor(diffMs / 1000);

        const dias = Math.floor(totalSegundos / 86400);
        const horas = Math.floor((totalSegundos % 86400) / 3600);
        const minutos = Math.floor((totalSegundos % 86400 % 3600) / 60);
        const segundos = totalSegundos % 60;

        span.textContent = `${String(dias).padStart(2, "0")}:${String(horas).padStart(2, "0")}:${String(minutos).padStart(2, "0")}:${String(segundos).padStart(2, "0")}`;
    });
}

// Timer global único (melhor performance)
setInterval(atualizarCronometros, 1000);

// Inicia imediatamente no load, populando o array com existentes
window.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".cronometro").forEach(span => {
        cronometrosAtivos.push(span);
    });
    atualizarCronometros();
});