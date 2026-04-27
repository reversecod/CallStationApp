const cronometrosAtivos = [];
let chamadoSelecionadoId = null;
let modalSelecionarListaTarefa = null;
let salvandoEdicaoChamado = false;
const camposDataHoraChamado = [
    { id: "editDataFinalizacao", nome: "Finalizacao" },
    { id: "editPrazoResposta", nome: "Prazo resposta" },
    { id: "editPrazoConclusao", nome: "Prazo conclusao" }
];

function mostrarToast(mensagem, tipo = "danger") {
    let container = document.getElementById("toastContainer");
    if (!container) {
        container = document.createElement("div");
        container.id = "toastContainer";
        container.className = "toast-container position-fixed top-0 end-0 p-3";
        container.style.zIndex = "2000";
        document.body.appendChild(container);
    }

    const toast = document.createElement("div");
    toast.className = `toast align-items-center text-bg-${tipo} border-0`;
    toast.role = "alert";
    toast.ariaLive = "assertive";
    toast.ariaAtomic = "true";
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${escapeHtml(String(mensagem || "Ocorreu um erro."))}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Fechar"></button>
        </div>
    `;

    container.appendChild(toast);
    const instance = bootstrap.Toast.getOrCreateInstance(toast, { delay: 4500 });
    toast.addEventListener("hidden.bs.toast", () => toast.remove());
    instance.show();
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

document.addEventListener("DOMContentLoaded", () => {
    mostrarToastPendente();

    const modalListaElement = document.getElementById("modalSelecionarListaTarefa");
    if (modalListaElement && window.bootstrap) {
        modalSelecionarListaTarefa = new bootstrap.Modal(modalListaElement);
    }

    document.getElementById("btnConfirmarListaTarefa")?.addEventListener("click", async () => {
        const chamadoId = Number(document.getElementById("chamadoPendenteTarefaId")?.value);
        const colunaId = Number(document.getElementById("selectListaTarefaDestino")?.value);

        if (!chamadoId || !colunaId) {
            mostrarToast("Selecione uma lista para criar a tarefa.");
            return;
        }

        modalSelecionarListaTarefa?.hide();
        await criarTarefaDeChamado(chamadoId, document.getElementById("navTarefasDrop")?.dataset.tasksUrl, colunaId);
    });

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

        containerChamados.addEventListener("dragstart", e => {
            const card = e.target.closest(".ticket-card");
            if (!card) return;

            e.dataTransfer.effectAllowed = "copy";
            e.dataTransfer.setData("text/plain", card.dataset.id);
            card.classList.add("dragging");
        });

        containerChamados.addEventListener("dragend", e => {
            const card = e.target.closest(".ticket-card");
            if (card) card.classList.remove("dragging");
        });
    }

    inicializarDropTarefas();
    inicializarBotoesEdicao();
    inicializarCamposDataHoraChamado();
    inicializarLimpezaSelecaoChamado();
    atualizarCronometros();
    setInterval(atualizarCronometros, 1000);
});

function mostrarToastPendente() {
    const mensagem = sessionStorage.getItem("toastChamadoMensagem");
    const tipo = sessionStorage.getItem("toastChamadoTipo") || "success";

    if (!mensagem) {
        return;
    }

    sessionStorage.removeItem("toastChamadoMensagem");
    sessionStorage.removeItem("toastChamadoTipo");
    mostrarToast(mensagem, tipo);
}

function inicializarDropTarefas() {
    const navTarefas = document.getElementById("navTarefasDrop");
    if (!navTarefas) return;

    navTarefas.addEventListener("dragover", e => {
        e.preventDefault();
        navTarefas.classList.add("active");
    });

    navTarefas.addEventListener("dragleave", () => {
        navTarefas.classList.remove("active");
    });

    navTarefas.addEventListener("drop", async e => {
        e.preventDefault();
        navTarefas.classList.remove("active");

        const chamadoId = Number(e.dataTransfer.getData("text/plain"));
        if (!chamadoId) return;

        await prepararCriacaoTarefaDeChamado(chamadoId, navTarefas.dataset.tasksUrl);
    });
}

async function prepararCriacaoTarefaDeChamado(chamadoId, redirectUrl) {
    const grupoId = Number(document.getElementById("grupoIdAtual")?.value);
    if (!grupoId) {
        mostrarToast("Grupo atual nao encontrado.");
        return;
    }

    try {
        const response = await fetch(`?handler=ListasTarefas&grupoId=${encodeURIComponent(grupoId)}`);
        const data = await response.json();

        if (!response.ok || !data.success) {
            throw new Error(data.message || `Erro HTTP: ${response.status}`);
        }

        const listas = data.listas || [];
        if (listas.length === 0) {
            mostrarToast("Crie uma lista em Tarefas antes de arrastar chamados.");
            if (redirectUrl) {
                window.location.href = redirectUrl;
            }
            return;
        }

        if (listas.length === 1) {
            await criarTarefaDeChamado(chamadoId, redirectUrl, listas[0].id);
            return;
        }

        abrirModalSelecionarListaTarefa(chamadoId, listas);
    } catch (error) {
        console.error("Erro ao carregar listas de tarefas:", error);
        mostrarToast("Não foi possível carregar as listas: " + error.message);
    }
}

function abrirModalSelecionarListaTarefa(chamadoId, listas) {
    const chamadoInput = document.getElementById("chamadoPendenteTarefaId");
    const select = document.getElementById("selectListaTarefaDestino");

    if (!chamadoInput || !select || !modalSelecionarListaTarefa) {
        mostrarToast("Não foi possível abrir a seleção de lista.");
        return;
    }

    chamadoInput.value = chamadoId;
    select.innerHTML = "";

    listas.forEach(lista => {
        const option = document.createElement("option");
        option.value = lista.id;
        option.textContent = lista.nome;
        select.appendChild(option);
    });

    modalSelecionarListaTarefa.show();
}

async function criarTarefaDeChamado(chamadoId, redirectUrl, colunaId) {
    const tokenInput = document.getElementById("requestVerificationToken");
    const grupoIdInput = document.getElementById("grupoIdAtual");

    if (!tokenInput || !grupoIdInput) {
        mostrarToast("Dados da sessao nao encontrados.");
        return;
    }

    try {
        const response = await fetch("?handler=CriarTarefaDeChamado", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": tokenInput.value
            },
            body: JSON.stringify({
                grupoId: Number(grupoIdInput.value),
                chamadoId,
                colunaId
            })
        });

        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.message || `Erro HTTP: ${response.status}`);
        }

        window.location.href = data.redirectUrl || redirectUrl || "/Menu/Tasks";
    } catch (error) {
        console.error("Erro ao criar tarefa a partir do chamado:", error);
        mostrarToast("Não foi possível criar a tarefa: " + error.message);
    }
}

async function criarNovoChamado() {
    const tokenInput = document.getElementById("requestVerificationToken");
    const grupoIdInput = document.getElementById("grupoIdAtual");
    const container = document.getElementById("chamados-container");

    if (!tokenInput) {
        mostrarToast("Token de verificação não encontrado.");
        return;
    }

    if (!grupoIdInput || !grupoIdInput.value) {
        mostrarToast("Grupo atual não encontrado.");
        return;
    }

    if (!container) {
        mostrarToast("Container de chamados não encontrado.");
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
        mostrarToast("Falha ao criar chamado: " + error.message);
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
        mostrarToast("Grupo atual não encontrado.");
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
            mostrarToast(data.message || "Não foi possível carregar o chamado.");
            return;
        }

        await preencherFormularioEdicao(data);
        aplicarPermissoesChamado(data.permissoes ?? {});
    } catch (error) {
        console.error("Erro ao carregar chamado:", error);
        mostrarToast("Erro ao carregar chamado: " + error.message);
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

function formatDateTimeDisplay(value) {
    const local = formatDateTimeLocal(value);
    if (!local) return "";

    const [data, hora] = local.split("T");
    const [ano, mes, dia] = data.split("-");
    return `${dia}/${mes}/${ano} ${hora}`;
}

function setValueIfExists(id, value) {
    const el = document.getElementById(id);
    if (el) {
        el.value = value ?? "";
    }
}

function setDateValueIfExists(id, value) {
    const el = document.getElementById(id);
    if (!el) return;

    const dataHoraOriginal = formatDateTimeDisplay(value);
    el.dataset.originalDateTime = dataHoraOriginal;
    el.value = dataHoraOriginal;
    el.classList.remove("is-invalid");
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

function normalizarTextoParaClasse(valor) {
    return String(valor ?? "")
        .toLowerCase()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/\s+/g, "");
}

function formatarStatusChamado(valor) {
    switch (valor) {
        case "EmAndamento":
            return "Em andamento";
        case "Concluido":
            return "Concluido";
        case "Reaberto":
            return "Reaberto";
        default:
            return valor || "-";
    }
}

function atualizarResumoChamado(data) {
    const metaInfo = document.getElementById("chamadoMetaInfo");
    const criadoPor = document.getElementById("chamadoCriadoPor");
    const criadoPorWrap = document.getElementById("chamadoCriadoPorWrap");
    const statusInfo = document.getElementById("chamadoStatusInfo");
    const statusWrap = document.getElementById("chamadoStatusWrap");
    const label = document.getElementById("chamadoSelecionadoLabel");

    if (label) {
        label.textContent = data.id ? `Chamado ${data.id}` : "Chamado";
        label.className = "badge badge-chamado-selecionado";
        label.classList.remove("d-none");
    }

    if (statusInfo && statusWrap) {
        const statusClasse = `badge-status-${normalizarTextoParaClasse(data.status || "aberto")}`;
        statusInfo.textContent = formatarStatusChamado(data.status);
        statusInfo.className = `meta-badge ${statusClasse}`;
        statusWrap.classList.remove("d-none");
    }

    if (metaInfo && criadoPor && criadoPorWrap) {
        metaInfo.classList.remove("d-none");

        if (data.criadorNomeUsuario && data.criadorPermissao) {
            const permissaoClasse = `badge-permissao-${normalizarTextoParaClasse(data.criadorPermissao)}`;
            criadoPor.textContent = data.criadorNomeUsuario;
            criadoPor.className = `meta-badge ${permissaoClasse}`;
            criadoPorWrap.classList.remove("d-none");
        } else {
            criadoPor.textContent = "-";
            criadoPor.className = "meta-badge badge-permissao-nenhuma";
            criadoPorWrap.classList.add("d-none");
        }
    }
}

async function preencherFormularioEdicao(data) {
    const msg = document.getElementById("mensagemSelecioneChamado");
    const form = document.getElementById("formEdicaoChamado");
    const label = document.getElementById("chamadoSelecionadoLabel");
    const anexoTexto = document.getElementById("anexoAtualTexto");
    const textoDataCriacao = document.getElementById("textoDataCriacao");

    if (!form) return;

    if (msg) msg.classList.add("d-none");
    form.classList.remove("d-none");

    atualizarResumoChamado(data);

    setValueIfExists("editId", data.id);
    setValueIfExists("editTitulo", data.titulo);
    setValueIfExists("editDescricao", data.descricao);
    setValueIfExists("editSolucao", data.solucao);

    setValueIfExists("editGrupoId", data.grupoId);
    setValueIfExists("editSetorId", data.setorId);
    setValueIfExists("editOcorrenciaTipoId", data.ocorrenciaTipoId);
    await carregarCategorias(data.ocorrenciaTipoId, data.ocorrenciaCategoriaId);
    await carregarSubcategorias(data.ocorrenciaCategoriaId, data.ocorrenciaSubcategoriaId);

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

    setDateValueIfExists("editDataFinalizacao", data.dataFinalizacao);
    setDateValueIfExists("editPrazoResposta", data.prazoResposta);
    setDateValueIfExists("editPrazoConclusao", data.prazoConclusao);

    setCheckedIfExists("editPublico", data.publico);
}

function inicializarBotoesEdicao() {
    const btnSalvar = document.getElementById("btnSalvarEdicao");
    const btnCancelar = document.getElementById("btnCancelarEdicao");
    const btnExcluir = document.getElementById("btnExcluirChamado");
    const form = document.getElementById("formEdicaoChamado");

    if (btnSalvar) {
        btnSalvar.addEventListener("click", salvarEdicaoChamado);
    }

    if (form) {
        form.addEventListener("submit", event => {
            event.preventDefault();
            salvarEdicaoChamado();
        });

        form.addEventListener("keydown", event => {
            if (event.key !== "Enter" || event.shiftKey || event.isComposing) {
                return;
            }

            const target = event.target;
            if (target?.matches?.("button, input[type='file']")) {
                return;
            }

            event.preventDefault();
            salvarEdicaoChamado();
        });
    }

    if (btnCancelar) {
        btnCancelar.addEventListener("click", cancelarEdicaoChamado);
    }

    if (btnExcluir) {
        btnExcluir.addEventListener("click", excluirChamado);
    }

    const selectTipo = document.getElementById("editOcorrenciaTipoId");
    const selectCategoria = document.getElementById("editOcorrenciaCategoriaId");

    if (selectTipo) {
        selectTipo.addEventListener("change", async function () {
            await carregarCategorias(this.value, null);
            await carregarSubcategorias(null, null);
        });
    }

    if (selectCategoria) {
        selectCategoria.addEventListener("change", async function () {
            await carregarSubcategorias(this.value, null);
        });
    }
}

function inicializarCamposDataHoraChamado() {
    camposDataHoraChamado.forEach(campo => {
        const input = document.getElementById(campo.id);
        if (!input) return;

        input.addEventListener("input", () => {
            input.value = aplicarMascaraDataHora(input.value);
            input.classList.remove("is-invalid");
        });

        input.addEventListener("blur", () => {
            input.value = aplicarMascaraDataHora(input.value, true);
            if (!input.value) {
                input.classList.remove("is-invalid");
                return;
            }

            const normalizado = normalizarDataHoraChamado(input.value);
            input.classList.toggle("is-invalid", !normalizado.valido);
            if (normalizado.valido) {
                input.value = normalizado.valor;
            }
        });
    });
}

function inicializarLimpezaSelecaoChamado() {
    document.querySelector(".sidebar")?.addEventListener("click", () => {
        if (!chamadoSelecionadoId) {
            return;
        }

        cancelarEdicaoChamado();
    });
}

function cancelarEdicaoChamado() {
    const form = document.getElementById("formEdicaoChamado");
    const msg = document.getElementById("mensagemSelecioneChamado");
    const label = document.getElementById("chamadoSelecionadoLabel");
    const metaInfo = document.getElementById("chamadoMetaInfo");
    const criadoPorWrap = document.getElementById("chamadoCriadoPorWrap");
    const statusInfo = document.getElementById("chamadoStatusInfo");
    const statusWrap = document.getElementById("chamadoStatusWrap");

    if (form) form.classList.add("d-none");
    if (msg) msg.classList.remove("d-none");
    if (metaInfo) metaInfo.classList.add("d-none");
    if (criadoPorWrap) criadoPorWrap.classList.add("d-none");
    if (statusInfo) statusInfo.className = "meta-badge badge-status-aberto";
    if (statusWrap) statusWrap.classList.add("d-none");

    if (label) {
        label.classList.add("d-none");
        label.textContent = "";
        label.className = "badge badge-chamado-selecionado d-none";
    }

    chamadoSelecionadoId = null;
}

async function excluirChamado() {
    if (!chamadoSelecionadoId) {
        mostrarToast("Nenhum chamado selecionado.");
        return;
    }

    if (!confirm("Tem certeza que deseja cancelar este chamado? Ele será removido da lista.")) {
        return;
    }

    const tokenInput = document.getElementById("requestVerificationToken");
    if (!tokenInput) {
        mostrarToast("Token de verificação não encontrado.");
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
            mostrarToast(data.message || "Erro ao cancelar chamado.");
            return;
        }

        const card = document.querySelector(`.ticket-card[data-id="${chamadoSelecionadoId}"]`);
        if (card) {
            card.remove();
        }

        cancelarEdicaoChamado();
        mostrarToast(data.message || "Chamado cancelado com sucesso.", "success");
    } catch (error) {
        console.error("Erro ao cancelar chamado:", error);
        mostrarToast("Erro ao cancelar chamado: " + error.message);
    }
}

async function salvarEdicaoChamado() {
    if (salvandoEdicaoChamado) {
        return;
    }

    if (!chamadoSelecionadoId) {
        mostrarToast("Nenhum chamado selecionado.");
        return;
    }

    const tokenInput = document.getElementById("requestVerificationToken");
    if (!tokenInput) {
        mostrarToast("Token de verificação não encontrado.");
        return;
    }

    const token = tokenInput.value;
    const datasNormalizadas = normalizarDatasChamadoParaSubmit();
    if (!datasNormalizadas.valido) {
        mostrarToast(datasNormalizadas.mensagem);
        return;
    }

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
        dataFinalizacao: datasNormalizadas.valores.editDataFinalizacao,
        prazoResposta: datasNormalizadas.valores.editPrazoResposta,
        prazoConclusao: datasNormalizadas.valores.editPrazoConclusao,
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

    salvandoEdicaoChamado = true;
    const btnSalvar = document.getElementById("btnSalvarEdicao");
    if (btnSalvar) {
        btnSalvar.disabled = true;
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
            mostrarToast(data.message || "Erro ao salvar chamado.");
            return;
        }

        sessionStorage.setItem("toastChamadoMensagem", "Chamado atualizado com sucesso.");
        sessionStorage.setItem("toastChamadoTipo", "success");
        cancelarEdicaoChamado();
        window.location.reload();
    } catch (error) {
        console.error("Erro ao salvar chamado:", error);
        mostrarToast("Erro ao salvar chamado: " + error.message);
        salvandoEdicaoChamado = false;
        if (btnSalvar) {
            btnSalvar.disabled = false;
        }
    }
}

function atualizarCardChamadoAposSalvar(payload) {
    const card = document.querySelector(`.ticket-card[data-id="${chamadoSelecionadoId}"]`);
    if (!card) return;

    if (payload.status === "Cancelado" || payload.status === "Excluido") {
        card.remove();
        cancelarEdicaoChamado();
        return;
    }

    card.classList.remove("ticket-aberto", "ticket-pendente", "ticket-em-andamento", "ticket-reaberto");

    const classeStatus = payload.status === "Pendente"
        ? "ticket-pendente"
        : payload.status === "EmAndamento"
            ? "ticket-em-andamento"
            : payload.status === "Reaberto"
                ? "ticket-reaberto"
            : "ticket-aberto";

    card.classList.add(classeStatus);

    const texto = card.querySelector(".ticket-text");
    const cronometro = card.querySelector(".cronometro");
    const titulo = payload.titulo || `Chamado ${chamadoSelecionadoId}`;

    if (texto && cronometro) {
        texto.textContent = `${titulo} - `;
        texto.appendChild(cronometro);
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

function aplicarMascaraDataHora(valor, completarHora = false) {
    const texto = String(valor ?? "").trim();
    const contemHoraAberta = /--:--$/.test(texto);
    const digitos = texto.replace(/\D/g, "").slice(0, 12);

    if (!digitos) return "";

    let resultado = digitos.slice(0, 2);
    if (digitos.length > 2) resultado += `/${digitos.slice(2, 4)}`;
    if (digitos.length > 4) resultado += `/${digitos.slice(4, 8)}`;

    if (digitos.length > 8) {
        resultado += ` ${digitos.slice(8, 10)}`;
        if (digitos.length > 10) resultado += `:${digitos.slice(10, 12)}`;
    } else if ((completarHora || contemHoraAberta) && digitos.length === 8) {
        resultado += " --:--";
    }

    return resultado;
}

function normalizarDataHoraChamado(valor) {
    const texto = String(valor ?? "").trim();
    if (!texto) {
        return { valido: true, valor: null };
    }

    const match = texto.match(/^(\d{2})\/(\d{2})\/(\d{4})\s+(--:--|(\d{2}):(\d{2}))$/);
    if (!match) {
        return { valido: false, valor: null };
    }

    const dia = Number(match[1]);
    const mes = Number(match[2]);
    const ano = Number(match[3]);
    const horaTexto = match[4] === "--:--" ? "00:00" : match[4];
    const [hora, minuto] = horaTexto.split(":").map(Number);

    if (hora < 0 || hora > 23 || minuto < 0 || minuto > 59) {
        return { valido: false, valor: null };
    }

    const data = new Date(ano, mes - 1, dia, hora, minuto, 0, 0);
    const dataValida =
        data.getFullYear() === ano &&
        data.getMonth() === mes - 1 &&
        data.getDate() === dia &&
        data.getHours() === hora &&
        data.getMinutes() === minuto;

    if (!dataValida) {
        return { valido: false, valor: null };
    }

    return {
        valido: true,
        valor: `${match[1]}/${match[2]}/${match[3]} ${horaTexto}`
    };
}

function normalizarDatasChamadoParaSubmit() {
    const valores = {};

    for (const campo of camposDataHoraChamado) {
        const input = document.getElementById(campo.id);
        if (!input || input.disabled) {
            valores[campo.id] = null;
            continue;
        }

        input.value = aplicarMascaraDataHora(input.value, true);
        const normalizado = normalizarDataHoraChamado(input.value);
        input.classList.toggle("is-invalid", !normalizado.valido);

        if (!normalizado.valido) {
            return {
                valido: false,
                mensagem: `${campo.nome}: informe uma data valida no formato dd/mm/aaaa --:-- ou dd/mm/aaaa HH:mm.`
            };
        }

        input.value = normalizado.valor ?? "";
        valores[campo.id] = normalizado.valor;
    }

    return { valido: true, valores };
}

async function carregarCategorias(tipoId, categoriaSelecionada) {
    const selectCategoria = document.getElementById("editOcorrenciaCategoriaId");
    if (!selectCategoria) return;

    selectCategoria.innerHTML = '<option value="">Selecione</option>';

    if (!tipoId) {
        return;
    }

    const grupoId = document.getElementById("grupoIdAtual")?.value;
    if (!grupoId) return;

    try {
        const response = await fetch(`?handler=CategoriasPorTipo&grupoId=${encodeURIComponent(grupoId)}&tipoId=${encodeURIComponent(tipoId)}`);
        const data = await response.json();

        if (!response.ok || !data.success) {
            throw new Error(data.message || "Erro ao carregar categorias.");
        }

        data.categorias.forEach(categoria => {
            const option = document.createElement("option");
            option.value = categoria.id;
            option.textContent = categoria.nome;
            if (String(categoria.id) === String(categoriaSelecionada)) {
                option.selected = true;
            }

            selectCategoria.appendChild(option);
        });
    } catch (error) {
        console.error("Erro ao carregar categorias:", error);
        mostrarToast(error.message || "Erro ao carregar categorias.");
    }
}

async function carregarSubcategorias(categoriaId, subcategoriaSelecionada) {
    const selectSubcategoria = document.getElementById("editOcorrenciaSubcategoriaId");
    if (!selectSubcategoria) return;

    selectSubcategoria.innerHTML = '<option value="">Selecione</option>';

    if (!categoriaId) {
        return;
    }

    const grupoId = document.getElementById("grupoIdAtual")?.value;
    if (!grupoId) return;

    try {
        const response = await fetch(`?handler=SubcategoriasPorCategoria&grupoId=${encodeURIComponent(grupoId)}&categoriaId=${encodeURIComponent(categoriaId)}`);
        const data = await response.json();

        if (!response.ok || !data.success) {
            throw new Error(data.message || "Erro ao carregar subcategorias.");
        }

        data.subcategorias.forEach(subcategoria => {
            const option = document.createElement("option");
            option.value = subcategoria.id;
            option.textContent = subcategoria.nome;
            if (String(subcategoria.id) === String(subcategoriaSelecionada)) {
                option.selected = true;
            }

            selectSubcategoria.appendChild(option);
        });
    } catch (error) {
        console.error("Erro ao carregar subcategorias:", error);
        mostrarToast(error.message || "Erro ao carregar subcategorias.");
    }
}

