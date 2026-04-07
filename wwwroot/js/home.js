const cronometrosAtivos = [];
let chamadoSelecionadoId = null;

document.addEventListener("DOMContentLoaded", () => {
    const botaoNovo = document.getElementById("btnNovoChamado");
    if (botaoNovo) {
        botaoNovo.addEventListener("click", criarNovoChamado);
    }

    document.querySelectorAll(".cronometro").forEach(span => {
        cronometrosAtivos.push(span);
    });

    const containerChamados = document.getElementById("chamados-container");
    if (containerChamados) {
        containerChamados.addEventListener("click", e => {
            const card = e.target.closest(".ticket-card");
            if (!card) return;

            const id = Number(card.dataset.id);
            if (!id) return;

            carregarChamado(id);
        });
    }

    inicializarBotoesEdicao();
    atualizarCronometros();
    setInterval(atualizarCronometros, 1000);
});

async function criarNovoChamado() {
    const tokenInput = document.getElementById("requestVerificationToken");
    const grupoIdInput = document.getElementById("grupoIdAtual");
    const container = document.getElementById("chamados-container");

    if (!tokenInput) {
        alert("Token de verificação não encontrado.");
        return;
    }

    if (!grupoIdInput || !grupoIdInput.value) {
        alert("Grupo atual não encontrado.");
        return;
    }

    if (!container) {
        alert("Container de chamados não encontrado.");
        return;
    }

    const token = tokenInput.value;
    const grupoId = grupoIdInput.value;

    const chamadoDiv = document.createElement("div");
    chamadoDiv.className = "ticket-card ticket-aberto";

    const img = document.createElement("img");
    img.src = "/images/logoticket.png";
    img.alt = "Ticket";
    img.className = "ticket-icon";
    img.loading = "eager";

    const spanOuter = document.createElement("span");
    spanOuter.className = "ticket-text";
    spanOuter.append("Criando... ");

    const spanCronometro = document.createElement("span");
    spanCronometro.className = "cronometro";
    spanCronometro.dataset.criadoEm = new Date().toISOString();
    spanCronometro.textContent = "00:00";

    spanOuter.appendChild(spanCronometro);
    chamadoDiv.appendChild(img);
    chamadoDiv.appendChild(spanOuter);

    const buttonDiv = container.querySelector(".d-flex.align-items-center");
    if (buttonDiv) {
        container.insertBefore(chamadoDiv, buttonDiv);
    } else {
        container.appendChild(chamadoDiv);
    }

    cronometrosAtivos.push(spanCronometro);
    atualizarCronometros();

    try {
        const response = await fetch(`?handler=NovoChamado&grupoId=${encodeURIComponent(grupoId)}`, {
            method: "POST",
            headers: {
                "RequestVerificationToken": token
            }
        });

        const data = await response.json();

        if (!response.ok || !data.success) {
            throw new Error(data.message || `Erro HTTP: ${response.status}`);
        }

        location.reload();
    } catch (error) {
        chamadoDiv.remove();

        const index = cronometrosAtivos.indexOf(spanCronometro);
        if (index > -1) {
            cronometrosAtivos.splice(index, 1);
        }

        console.error("Erro na requisição de criação:", error);
        alert("Falha ao criar chamado: " + error.message);
    }
}

function atualizarCronometros() {
    const agora = new Date();

    cronometrosAtivos.forEach(span => {
        const criadoEmTexto = span.dataset.criadoEm;
        const criadoEm = new Date(criadoEmTexto);

        if (isNaN(criadoEm.getTime())) {
            span.textContent = "00:00";
            return;
        }

        const diffMs = agora - criadoEm;
        const totalSegundos = Math.max(0, Math.floor(diffMs / 1000));

        const dias = Math.floor(totalSegundos / 86400);
        const horas = Math.floor((totalSegundos % 86400) / 3600);
        const minutos = Math.floor((totalSegundos % 3600) / 60);
        const segundos = totalSegundos % 60;

        if (dias > 0) {
            span.textContent =
                `${String(dias).padStart(2, "0")}:` +
                `${String(horas).padStart(2, "0")}:` +
                `${String(minutos).padStart(2, "0")}:` +
                `${String(segundos).padStart(2, "0")}`;
        } else if (horas > 0) {
            span.textContent =
                `${String(horas).padStart(2, "0")}:` +
                `${String(minutos).padStart(2, "0")}:` +
                `${String(segundos).padStart(2, "0")}`;
        } else {
            span.textContent =
                `${String(minutos).padStart(2, "0")}:` +
                `${String(segundos).padStart(2, "0")}`;
        }
    });
}

async function carregarChamado(id) {
    chamadoSelecionadoId = id;

    const grupoId = document.getElementById("grupoIdAtual")?.value;
    if (!grupoId) {
        alert("Grupo atual não encontrado.");
        return;
    }

    try {
        const response = await fetch(`?handler=CarregarChamado&id=${encodeURIComponent(id)}&grupoId=${encodeURIComponent(grupoId)}`, {
            method: "GET"
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();

        if (data.success === false) {
            alert(data.message || "Não foi possível carregar o chamado.");
            return;
        }

        preencherFormularioEdicao(data);
        aplicarPermissoesChamado(data.permissoes ?? {});
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

function setValueIfExists(id, value) {
    const el = document.getElementById(id);
    if (el) {
        el.value = value ?? "";
    }
}

function setCheckedIfExists(id, value) {
    const el = document.getElementById(id);
    if (el) {
        el.checked = !!value;
    }
}

function aplicarPermissoesChamado(permissoes) {
    configurarCampo("wrapEditTitulo", "editTitulo", !!permissoes.podeEditarTitulo);
    configurarCampo("wrapEditDescricao", "editDescricao", !!permissoes.podeEditarDescricao);
    configurarCampo("wrapEditSolucao", "editSolucao", !!permissoes.podeEditarSolucao);

    configurarCampo("wrapEditSetorId", "editSetorId", !!permissoes.podeEditarSetorId);
    configurarCampo("wrapEditOcorrenciaTipoId", "editOcorrenciaTipoId", !!permissoes.podeEditarOcorrenciaTipoId);
    configurarCampo("wrapEditOcorrenciaCategoriaId", "editOcorrenciaCategoriaId", !!permissoes.podeEditarOcorrenciaCategoriaId);
    configurarCampo("wrapEditOcorrenciaSubcategoriaId", "editOcorrenciaSubcategoriaId", !!permissoes.podeEditarOcorrenciaSubcategoriaId);

    configurarCampo("wrapEditAnexoArquivo", "editAnexoArquivo", !!permissoes.podeEditarAnexoChamado);

    configurarCampo("wrapEditPrioridade", "editPrioridade", !!permissoes.podeEditarPrioridade);
    configurarCampo("wrapEditCriticidade", "editCriticidade", !!permissoes.podeEditarCriticidade);
    configurarCampo("wrapEditUrgencia", "editUrgencia", !!permissoes.podeEditarUrgencia);
    configurarCampo("wrapEditStatus", "editStatus", !!permissoes.podeEditarStatus);

    configurarCampo("wrapEditDataFinalizacao", "editDataFinalizacao", !!permissoes.podeEditarDataFinalizacao);
    configurarCampo("wrapEditPrazoResposta", "editPrazoResposta", !!permissoes.podeEditarPrazoResposta);
    configurarCampo("wrapEditPrazoConclusao", "editPrazoConclusao", !!permissoes.podeEditarPrazoConclusao);
    configurarCampo("wrapEditPublico", "editPublico", !!permissoes.podeEditarPublico);

    const btnExcluir = document.getElementById("btnExcluirChamado");
    if (btnExcluir) {
        btnExcluir.classList.toggle("d-none", !permissoes.podeExcluir);
    }

    const btnSalvar = document.getElementById("btnSalvarEdicao");
    if (btnSalvar) {
        const podeSalvar =
            !!permissoes.podeEditarTitulo ||
            !!permissoes.podeEditarDescricao ||
            !!permissoes.podeEditarSolucao ||
            !!permissoes.podeEditarSetorId ||
            !!permissoes.podeEditarOcorrenciaTipoId ||
            !!permissoes.podeEditarOcorrenciaCategoriaId ||
            !!permissoes.podeEditarOcorrenciaSubcategoriaId ||
            !!permissoes.podeEditarAnexoChamado ||
            !!permissoes.podeEditarPrioridade ||
            !!permissoes.podeEditarCriticidade ||
            !!permissoes.podeEditarUrgencia ||
            !!permissoes.podeEditarStatus ||
            !!permissoes.podeEditarDataFinalizacao ||
            !!permissoes.podeEditarPrazoResposta ||
            !!permissoes.podeEditarPrazoConclusao ||
            !!permissoes.podeEditarPublico;

        btnSalvar.disabled = !podeSalvar;
    }
}

function configurarCampo(wrapperId, inputId, podeEditar) {
    const wrapper = document.getElementById(wrapperId);
    const input = document.getElementById(inputId);

    if (!wrapper || !input) return;

    if (podeEditar) {
        wrapper.classList.remove("d-none");
        input.disabled = false;
        input.readOnly = false;
    } else {
        wrapper.classList.add("d-none");
        input.disabled = true;
        input.readOnly = true;
    }
}

function preencherFormularioEdicao(data) {
    const msg = document.getElementById("mensagemSelecioneChamado");
    const form = document.getElementById("formEdicaoChamado");
    const label = document.getElementById("chamadoSelecionadoLabel");
    const anexoTexto = document.getElementById("anexoAtualTexto");
    const textoDataCriacao = document.getElementById("textoDataCriacao");

    if (!form) return;

    if (msg) msg.classList.add("d-none");
    form.classList.remove("d-none");

    if (label) {
        label.textContent = data.id ? `Chamado ${data.id}` : "Chamado";
        label.classList.remove("d-none");
    }

    setValueIfExists("editId", data.id);
    setValueIfExists("editTitulo", data.titulo);
    setValueIfExists("editDescricao", data.descricao);
    setValueIfExists("editSolucao", data.solucao);

    setValueIfExists("editGrupoId", data.grupoId);
    setValueIfExists("editSetorId", data.setorId);
    setValueIfExists("editOcorrenciaTipoId", data.ocorrenciaTipoId);
    setValueIfExists("editOcorrenciaCategoriaId", data.ocorrenciaCategoriaId);
    setValueIfExists("editOcorrenciaSubcategoriaId", data.ocorrenciaSubcategoriaId);

    if (anexoTexto) {
        anexoTexto.textContent = data.anexoChamado ? data.anexoChamado : "Nenhum";
    }

    if (textoDataCriacao) {
        textoDataCriacao.textContent = data.dataCriacao
            ? new Date(data.dataCriacao).toLocaleString("pt-BR")
            : "-";
    }

    setValueIfExists("editPrioridade", data.prioridade);
    setValueIfExists("editCriticidade", data.criticidade);
    setValueIfExists("editUrgencia", data.urgencia);
    setValueIfExists("editStatus", data.status ?? "Aberto");

    setValueIfExists("editDataFinalizacao", formatDateTimeLocal(data.dataFinalizacao));
    setValueIfExists("editPrazoResposta", formatDateTimeLocal(data.prazoResposta));
    setValueIfExists("editPrazoConclusao", formatDateTimeLocal(data.prazoConclusao));

    setCheckedIfExists("editPublico", data.publico);
}

function inicializarBotoesEdicao() {
    const btnSalvar = document.getElementById("btnSalvarEdicao");
    const btnCancelar = document.getElementById("btnCancelarEdicao");
    const btnExcluir = document.getElementById("btnExcluirChamado");

    if (btnSalvar) {
        btnSalvar.addEventListener("click", salvarEdicaoChamado);
    }

    if (btnCancelar) {
        btnCancelar.addEventListener("click", cancelarEdicaoChamado);
    }

    if (btnExcluir) {
        btnExcluir.addEventListener("click", excluirChamado);
    }
}

function cancelarEdicaoChamado() {
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

    try {
        const response = await fetch("?handler=ExcluirChamado", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify({ id: chamadoSelecionadoId })
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();

        if (!data.success) {
            alert(data.message || "Erro ao excluir chamado.");
            return;
        }

        const card = document.querySelector(`.ticket-card[data-id="${chamadoSelecionadoId}"]`);
        if (card) {
            card.remove();
        }

        cancelarEdicaoChamado();
        alert("Chamado excluído com sucesso.");
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
        id: toNullableInt(getValue("editId")),
        titulo: getNullableString("editTitulo"),
        descricao: getNullableString("editDescricao"),
        solucao: getNullableString("editSolucao"),
        grupoId: toNullableInt(getValue("editGrupoId")),
        setorId: toNullableInt(getValue("editSetorId")),
        ocorrenciaTipoId: toNullableInt(getValue("editOcorrenciaTipoId")),
        ocorrenciaCategoriaId: toNullableInt(getValue("editOcorrenciaCategoriaId")),
        ocorrenciaSubcategoriaId: toNullableInt(getValue("editOcorrenciaSubcategoriaId")),
        prioridade: getNullableString("editPrioridade"),
        criticidade: getNullableString("editCriticidade"),
        urgencia: getNullableString("editUrgencia"),
        status: getNullableString("editStatus"),
        dataFinalizacao: toNullableDate(getValue("editDataFinalizacao")),
        prazoResposta: toNullableDate(getValue("editPrazoResposta")),
        prazoConclusao: toNullableDate(getValue("editPrazoConclusao")),
        publico: !!document.getElementById("editPublico")?.checked
    };

    const formData = new FormData();

    for (const [key, value] of Object.entries(payload)) {
        if (value !== null && value !== undefined && value !== "") {
            formData.append(key, value);
        }
    }

    const fileInput = document.getElementById("editAnexoArquivo");
    if (fileInput && fileInput.files && fileInput.files.length > 0) {
        formData.append("AnexoArquivo", fileInput.files[0]);
    }

    try {
        const response = await fetch("?handler=SalvarChamado", {
            method: "POST",
            headers: {
                "RequestVerificationToken": token
            },
            body: formData
        });

        if (!response.ok) {
            throw new Error(`Erro HTTP: ${response.status}`);
        }

        const data = await response.json();

        if (!data.success) {
            alert(data.message || "Erro ao salvar chamado.");
            return;
        }

        alert("Chamado atualizado com sucesso.");
        window.location.reload();
    } catch (error) {
        console.error("Erro ao salvar chamado:", error);
        alert("Erro ao salvar chamado: " + error.message);
    }
}

function getValue(id) {
    return document.getElementById(id)?.value ?? "";
}

function getNullableString(id) {
    const value = getValue(id).trim();
    return value === "" ? null : value;
}

function toNullableInt(value) {
    if (value === null || value === undefined || value === "") {
        return null;
    }

    const numero = parseInt(value, 10);
    return Number.isNaN(numero) ? null : numero;
}

function toNullableDate(value) {
    if (!value) return null;
    return value;
}