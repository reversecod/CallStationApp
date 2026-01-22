const cronometrosAtivos = [];
let proximoNumeroChamado = 1;
let chamadoSelecionadoId = null;

// =============== CRIAÇÃO DE NOVO CHAMADO ===============
document.addEventListener("DOMContentLoaded", () => {
    const botaoNovo = document.getElementById("btnNovoChamado");
    if (botaoNovo) {
        botaoNovo.addEventListener("click", criarNovoChamado);
    }

    // Adiciona chamados existentes ao array de cronômetros
    document.querySelectorAll(".cronometro").forEach(span => {
        cronometrosAtivos.push(span);
    });

    const chamadosExistentes = document.querySelectorAll(".chamado-numero");
    if (chamadosExistentes.length > 0) {
        const maiorNumero = Math.max(
            ...Array.from(chamadosExistentes).map(el => parseInt(el.textContent.replace("Chamado ", "")) || 0)
        );
        proximoNumeroChamado = maiorNumero + 1;
    }

    // Delegação de clique nos tickets (resolve problema dos novos cards)
    const containerChamados = document.getElementById("chamados-container");
    if (containerChamados) {
        containerChamados.addEventListener("click", e => {
            const card = e.target.closest(".ticket-card");
            if (!card) return;
            const id = card.dataset.id;
            if (!id) return;
            carregarChamado(parseInt(id));
        });
    }

    // Botões de salvar / cancelar no painel de edição
    inicializarBotoesEdicao();

    atualizarCronometros();
    setInterval(atualizarCronometros, 1000);
});

async function criarNovoChamado() {
    const tokenInput = document.getElementById("requestVerificationToken");
    if (!tokenInput) {
        alert("Token de verificação não encontrado.");
        return;
    }
    const token = tokenInput.value;

    const container = document.getElementById("chamados-container");
    if (!container) return;

    // ---- Card otimista "Criando..." enquanto espera o backend ----
    const chamadoDiv = document.createElement("div");
    chamadoDiv.className = "ticket-card";

    const img = document.createElement("img");
    img.src = "/images/logoticket.png";
    img.alt = "Ticket";
    img.className = "ticket-icon";
    img.loading = "eager";

    const spanOuter = document.createElement("span");
    spanOuter.append("Criando... - ");

    const spanCronometro = document.createElement("span");
    spanCronometro.className = "cronometro";
    const agoraLocal = new Date();
    spanCronometro.dataset.criadoEm = agoraLocal.toISOString();
    spanCronometro.textContent = "00:00";

    spanOuter.appendChild(spanCronometro);
    chamadoDiv.appendChild(img);
    chamadoDiv.appendChild(spanOuter);

    const buttonDiv = container.querySelector(".d-flex.align-items-center");
    container.insertBefore(chamadoDiv, buttonDiv);

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

        // ✅ Se chegou aqui, o chamado foi criado no backend.
        // Não precisamos nem ler o JSON: apenas recarrega a página.
        location.reload();
    } catch (error) {
        // Se der erro, remove o card "Criando..."
        chamadoDiv.remove();
        const index = cronometrosAtivos.indexOf(spanCronometro);
        if (index > -1) cronometrosAtivos.splice(index, 1);

        console.error("Erro na requisição:", error);
        alert("Falha ao criar chamado: " + error.message);
    }
}

// =============== CRONÔMETROS ===============
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

// =============== CARREGAR CHAMADO P/ EDIÇÃO ===============
async function carregarChamado(id) {
    chamadoSelecionadoId = id;
    console.log("Carregando chamado", id);

    try {
        const response = await fetch(`?handler=CarregarChamado&id=${encodeURIComponent(id)}`, {
            method: "GET"
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();
        console.log("Resposta CarregarChamado:", data);

        if (data.success === false) {
            alert(data.message || "Não foi possível carregar o chamado.");
            return;
        }

        preencherFormularioEdicao(data);
    } catch (error) {
        console.error("Erro ao carregar chamado:", error);
        alert("Erro ao carregar chamado: " + error.message);
    }
}

function formatDateTimeLocal(value) {
    if (!value) return "";
    const dt = new Date(value);
    if (isNaN(dt.getTime())) return "";
    const tzoffset = dt.getTimezoneOffset() * 60000;
    const localISO = new Date(dt.getTime() - tzoffset).toISOString();
    return localISO.slice(0, 16);
}

function preencherFormularioEdicao(data) {
    console.log("preencherFormularioEdicao", data);

    const msg = document.getElementById("mensagemSelecioneChamado");
    const form = document.getElementById("formEdicaoChamado");
    if (!form) return;

    // Mostra o formulário e esconde a mensagem inicial
    if (msg) msg.classList.add("d-none");
    form.classList.remove("d-none");

    // === LABEL COM O NÚMERO DO CHAMADO ===
    const label = document.getElementById("chamadoSelecionadoLabel");
    if (label) {
        if (data.id) {
            label.textContent = `Chamado ${data.id}`;
        } else {
            label.textContent = "Chamado";
        }
        label.classList.remove("d-none");
    }

    // === CAMPOS DE TEXTO / IDS / ENUMS / DATAS ===
    document.getElementById("editId").value = data.id ?? "";
    document.getElementById("editTitulo").value = data.titulo ?? "";
    document.getElementById("editDescricao").value = data.descricao ?? "";
    document.getElementById("editSolucao").value = data.solucao ?? "";

    document.getElementById("editGrupoId").value = data.grupoId ?? "";
    document.getElementById("editSetorId").value = data.setorId ?? "";
    document.getElementById("editOcorrenciaTipoId").value = data.ocorrenciaTipoId ?? "";
    document.getElementById("editOcorrenciaCategoriaId").value = data.ocorrenciaCategoriaId ?? "";
    document.getElementById("editOcorrenciaSubcategoriaId").value = data.ocorrenciaSubcategoriaId ?? "";

    const anexoTexto = document.getElementById("anexoAtualTexto");
    if (anexoTexto) {
        anexoTexto.textContent = data.anexoChamado ? data.anexoChamado : "Nenhum";
    }
    document.getElementById("editCriadorSolicitacao").value = data.criadorSolicitacao ?? "";
    document.getElementById("editResponsavelSolucao").value = data.responsavelSolucao ?? "";

    document.getElementById("editPrioridade").value = data.prioridade ?? "";
    document.getElementById("editCriticidade").value = data.criticidade ?? "";
    document.getElementById("editUrgencia").value = data.urgencia ?? "";
    document.getElementById("editStatus").value = data.status ?? "Aberto";

    document.getElementById("editDataInicioAtendimento").value = formatDateTimeLocal(data.dataInicioAtendimento);
    document.getElementById("editDataCriacao").value = formatDateTimeLocal(data.dataCriacao);
    document.getElementById("editDataFinalizacao").value = formatDateTimeLocal(data.dataFinalizacao);
    document.getElementById("editPrazoResposta").value = formatDateTimeLocal(data.prazoResposta);
    document.getElementById("editPrazoConclusao").value = formatDateTimeLocal(data.prazoConclusao);

    document.getElementById("editPublico").checked = !!data.publico;
}
// =============== SALVAR / CANCELAR EDIÇÃO ===============
function inicializarBotoesEdicao() {
    const btnSalvar = document.getElementById("btnSalvarEdicao");
    const btnCancelar = document.getElementById("btnCancelarEdicao");
    const btnExcluir = document.getElementById("btnExcluirChamado");

    if (btnSalvar) {
        btnSalvar.addEventListener("click", salvarEdicaoChamado);
    }

    if (btnCancelar) {
        btnCancelar.addEventListener("click", () => {
            const form = document.getElementById("formEdicaoChamado");
            const msg = document.getElementById("mensagemSelecioneChamado");
            const label = document.getElementById("chamadoSelecionadoLabel");
            if (form) form.classList.add("d-none");
            if (msg) msg.classList.remove("d-none");
            if (label) {
                label.classList.add("d-none");
                label.textContent = "";
            }
            chamadoSelecionadoId = null;
        });
    }

    if (btnExcluir) {
        btnExcluir.addEventListener("click", excluirChamado);
    }
}

async function excluirChamado() {
    if (!chamadoSelecionadoId) {
        alert("Nenhum chamado selecionado.");
        return;
    }

    if (!confirm("Tem certeza que deseja excluir este chamado? Ele será marcado como 'Cancelado' e removido da lista.")) {
        return;
    }

    const tokenInput = document.getElementById("requestVerificationToken");
    if (!tokenInput) {
        alert("Token de verificação não encontrado.");
        return;
    }
    const token = tokenInput.value;

    const payload = { id: chamadoSelecionadoId };

    try {
        const response = await fetch("?handler=ExcluirChamado", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();
        console.log("Resposta ExcluirChamado:", data);

        if (!data.success) {
            alert(data.message || "Erro ao excluir chamado.");
            return;
        }

        // Remove o card da esquerda
        const card = document.querySelector(`.ticket-card[data-id="${chamadoSelecionadoId}"]`);
        if (card) card.remove();

        // Reseta painel
        const form = document.getElementById("formEdicaoChamado");
        const msg = document.getElementById("mensagemSelecioneChamado");
        const label = document.getElementById("chamadoSelecionadoLabel");
        if (form) form.classList.add("d-none");
        if (msg) msg.classList.remove("d-none");
        if (label) {
            label.classList.add("d-none");
            label.textContent = "";
        }
        chamadoSelecionadoId = null;

        alert("Chamado excluído (status 'Cancelado').");
    } catch (error) {
        console.error("Erro ao excluir chamado:", error);
        alert("Erro ao excluir chamado: " + error.message);
    }
}

async function salvarEdicaoChamado() {
    if (!chamadoSelecionadoId) {
        alert("Nenhum chamado selecionado.");
        return;
    }

    const tokenInput = document.getElementById("requestVerificationToken");
    if (!tokenInput) {
        alert("Token de verificação não encontrado.");
        return;
    }
    const token = tokenInput.value;

    const payload = {
        id: parseInt(document.getElementById("editId").value),
        titulo: document.getElementById("editTitulo").value || null,
        descricao: document.getElementById("editDescricao").value || null,
        solucao: document.getElementById("editSolucao").value || null,
        grupoId: toNullableInt(document.getElementById("editGrupoId").value),
        setorId: toNullableInt(document.getElementById("editSetorId").value),
        ocorrenciaTipoId: toNullableInt(document.getElementById("editOcorrenciaTipoId").value),
        ocorrenciaCategoriaId: toNullableInt(document.getElementById("editOcorrenciaCategoriaId").value),
        ocorrenciaSubcategoriaId: toNullableInt(document.getElementById("editOcorrenciaSubcategoriaId").value),
        // anexoChamado removido daqui (arquivo vai separado)
        criadorSolicitacao: document.getElementById("editCriadorSolicitacao").value || null,
        responsavelSolucao: document.getElementById("editResponsavelSolucao").value || null,
        prioridade: document.getElementById("editPrioridade").value || null,
        criticidade: document.getElementById("editCriticidade").value || null,
        urgencia: document.getElementById("editUrgencia").value || null,
        status: document.getElementById("editStatus").value || null,
        dataInicioAtendimento: toNullableDate(document.getElementById("editDataInicioAtendimento").value),
        dataCriacao: toNullableDate(document.getElementById("editDataCriacao").value),
        dataFinalizacao: toNullableDate(document.getElementById("editDataFinalizacao").value),
        prazoResposta: toNullableDate(document.getElementById("editPrazoResposta").value),
        prazoConclusao: toNullableDate(document.getElementById("editPrazoConclusao").value),
        publico: document.getElementById("editPublico").checked
    };

    const formData = new FormData();
    for (const [key, value] of Object.entries(payload)) {
        if (value !== null && value !== undefined) {
            formData.append(key, value);
        }
    }

    const fileInput = document.getElementById("editAnexoArquivo");
    if (fileInput && fileInput.files.length > 0) {
        formData.append("AnexoArquivo", fileInput.files[0]); // nome bate com o DTO
    }

    try {
        const response = await fetch("?handler=SalvarChamado", {
            method: "POST",
            headers: {
                "RequestVerificationToken": token
                // NÃO definir Content-Type aqui, o browser monta o multipart/form-data
            },
            body: formData
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();
        console.log("Resposta SalvarChamado:", data);

        if (!data.success) {
            alert(data.message || "Erro ao salvar chamado.");
            return;
        }

        // sucesso -> recarrega a página
        alert("Chamado atualizado com sucesso.");
        window.location.reload(); // ou location.reload();

        alert("Chamado atualizado com sucesso.");
    } catch (error) {
        console.error("Erro ao salvar chamado:", error);
        alert("Erro ao salvar chamado: " + error.message);
    }
}

function toNullableInt(v) {
    if (!v) return null;
    const n = parseInt(v);
    return isNaN(n) ? null : n;
}

function toNullableDate(v) {
    if (!v) return null;
    // O model binder entende "yyyy-MM-ddTHH:mm" como hora local
    return v;
}