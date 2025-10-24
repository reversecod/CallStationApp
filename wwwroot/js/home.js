const cronometrosAtivos = [];
let proximoNumeroChamado = 1; // Contador para novos chamados

document.getElementById("btnNovoChamado").addEventListener("click", async () => {
    const token = document.getElementById("requestVerificationToken").value;

    // Criação otimista: adiciona o elemento imediatamente com dados temporários
    const container = document.getElementById("chamados-container");
    const chamadoDiv = document.createElement("div");
    chamadoDiv.className = "ticket-card";

    const img = document.createElement("img");
    img.src = "/images/logoticket.png";
    img.alt = "Ticket";
    img.className = "ticket-icon";
    img.loading = "eager";

    const spanOuter = document.createElement("span");
    spanOuter.textContent = `Criando... - `; // Placeholder temporário

    const spanCronometro = document.createElement("span");
    spanCronometro.className = "cronometro";
    const agoraLocal = new Date();
    spanCronometro.dataset.criadoEm = agoraLocal.toISOString();
    spanCronometro.textContent = "00:00";

    spanOuter.appendChild(spanCronometro);
    chamadoDiv.appendChild(img);
    chamadoDiv.appendChild(spanOuter);

    // Encontra o div do botão e insere antes dele
    const buttonDiv = container.querySelector('.d-flex.align-items-center');
    container.insertBefore(chamadoDiv, buttonDiv);

    // Adiciona ao array e atualiza imediatamente
    cronometrosAtivos.push(spanCronometro);
    atualizarCronometros();

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
            // Atualiza com dados reais do servidor
            spanOuter.firstChild.textContent = `${data.status} - `; // Atualiza o texto antes do spanCronometro

            // Adiciona o número do chamado após a criação
            const spanNumero = document.createElement("span");
            spanNumero.className = "chamado-numero";
            spanNumero.textContent = `Chamado ${data.id || proximoNumeroChamado}`; // Usa o ID do servidor
            spanOuter.insertBefore(spanNumero, spanCronometro); // Insere antes do cronômetro
            spanOuter.insertBefore(document.createTextNode(" - "), spanCronometro); // Adiciona separador

            spanCronometro.dataset.criadoEm = new Date(data.criadoEm).toISOString();
            atualizarCronometros();
            proximoNumeroChamado = data.id ? data.id + 1 : proximoNumeroChamado + 1; // Atualiza o contador
        } else {
            chamadoDiv.remove();
            const index = cronometrosAtivos.indexOf(spanCronometro);
            if (index > -1) cronometrosAtivos.splice(index, 1);
            alert(data.message || "Erro ao criar chamado");
        }
    } catch (error) {
        chamadoDiv.remove();
        const index = cronometrosAtivos.indexOf(spanCronometro);
        if (index > -1) cronometrosAtivos.splice(index, 1);
        console.error("Erro na requisição:", error);
        alert("Falha ao criar chamado: " + error.message);
    }
});

function atualizarCronometros() {
    const agora = new Date();

    cronometrosAtivos.forEach(span => {
        const criadoEm = new Date(span.dataset.criadoEm);
        if (isNaN(criadoEm.getTime())) {
            span.textContent = "00:00";
            return;
        }

        const diffMs = agora - criadoEm;
        const totalSegundos = Math.floor(diffMs / 1000);

        const dias = Math.floor(totalSegundos / 86400);
        const horas = Math.floor((totalSegundos % 86400) / 3600);
        const minutos = Math.floor((totalSegundos % 3600) / 60);
        const segundos = totalSegundos % 60;

        if (dias > 0) {
            span.textContent = `${String(dias).padStart(2, "0")}:${String(horas).padStart(2, "0")}:${String(minutos).padStart(2, "0")}:${String(segundos).padStart(2, "0")}`;
        } else if (horas > 0) {
            span.textContent = `${String(horas).padStart(2, "0")}:${String(minutos).padStart(2, "0")}:${String(segundos).padStart(2, "0")}`;
        } else {
            span.textContent = `${String(minutos).padStart(2, "0")}:${String(segundos).padStart(2, "0")}`;
        }
    });
}

// Timer global único
setInterval(atualizarCronometros, 1000);

// Inicia no load
window.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".cronometro").forEach(span => {
        cronometrosAtivos.push(span);
    });
    // Inicializa o proximoNumeroChamado com base nos chamados existentes
    const chamadosExistentes = document.querySelectorAll(".chamado-numero");
    if (chamadosExistentes.length > 0) {
        const maiorNumero = Math.max(...Array.from(chamadosExistentes).map(el => parseInt(el.textContent.replace("Chamado ", "")) || 0));
        proximoNumeroChamado = maiorNumero + 1;
    }
    atualizarCronometros();
});