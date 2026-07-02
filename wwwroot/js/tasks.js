let draggedCard = null;
let draggedList = null;
let modalLista = null;
let modalCartao = null;
let modalMembrosCartao = null;
let modalChamadosCartao = null;
let modalVisualizarAnexoTarefa = null;
let modalTemplateCartao = null;
let modalSelecionarTemplate = null;
let modalAcoesLista = null;
let painelTemplates = null;
let colunaTemplatesAtual = null;
let listaAcoesAtualId = null;
let uploadAnexoTarefaController = null;
let previewAnexoTarefaController = null;
let buscaChamadosCartaoTimer = null;
let buscaChamadosCartaoController = null;
let templatesAtuais = [];
let snapshotBoardAntesDrag = null;
let snapshotListasAntesDrag = null;
let chamadosSelecionadosModal = new Set();
let chamadosRemovidosModal = new Set();
let cartaoAtualEstado = {
    membros: [],
    membrosVisiveis: [],
    chamados: [],
    chamadosVinculados: [],
    compartilharGrupo: false,
    arquivado: false,
    podeEditar: false,
    podeSairVinculo: false,
    podeGerenciarEtiquetas: true,
    etiquetasDisponiveis: [],
    etiquetasAplicadas: [],
    checklists: [],
    anexos: [],
    titulo: "",
    colunaId: null
};
let cartaoSnapshotSalvar = null;

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

document.addEventListener("DOMContentLoaded", () => {
    const modalCartaoElement = document.getElementById("modalCartao");
    const modalVisualizarAnexoElement = document.getElementById("modalVisualizarAnexoTarefa");
    const modalChamadosCartaoElement = document.getElementById("modalChamadosCartao");

    modalLista = new bootstrap.Modal(document.getElementById("modalLista"));
    modalCartao = new bootstrap.Modal(modalCartaoElement);
    modalMembrosCartao = new bootstrap.Modal(document.getElementById("modalMembrosCartao"));
    modalChamadosCartao = new bootstrap.Modal(modalChamadosCartaoElement);
    modalVisualizarAnexoTarefa = new bootstrap.Modal(modalVisualizarAnexoElement);
    modalTemplateCartao = new bootstrap.Modal(document.getElementById("modalTemplateCartao"));
    modalSelecionarTemplate = new bootstrap.Modal(document.getElementById("modalSelecionarTemplate"));
    modalAcoesLista = new bootstrap.Modal(document.getElementById("modalAcoesLista"));

    document.getElementById("btnAbrirArquivados")?.addEventListener("click", abrirPainelArquivados);
    document.getElementById("btnFecharArquivados")?.addEventListener("click", fecharPainelArquivados);
    document.getElementById("btnAdicionarListaInline")?.addEventListener("click", abrirModalLista);
    document.getElementById("formLista")?.addEventListener("submit", criarLista);
    document.getElementById("formCartao")?.addEventListener("submit", salvarCartao);
    document.getElementById("formTemplateCartao")?.addEventListener("submit", salvarTemplate);
    document.getElementById("btnAdicionarComentario")?.addEventListener("click", adicionarComentario);
    document.getElementById("btnArquivarCartao")?.addEventListener("click", alternarArquivamentoCartao);
    document.getElementById("btnSairCartao")?.addEventListener("click", sairCartao);
    document.getElementById("btnMostrarSalvarTemplate")?.addEventListener("click", mostrarSalvarComoTemplate);
    document.getElementById("btnConfirmarSalvarTemplate")?.addEventListener("click", salvarComoTemplate);
    document.getElementById("btnNovoTemplateModal")?.addEventListener("click", abrirModalTemplateNovo);
    document.getElementById("btnRenomearListaModal")?.addEventListener("click", () => renomearLista(listaAcoesAtualId));
    document.getElementById("btnArquivarCartoesLista")?.addEventListener("click", arquivarCartoesListaAtual);
    document.getElementById("btnExcluirLista")?.addEventListener("click", excluirListaAtual);
    document.getElementById("cartaoCorCapa")?.addEventListener("input", atualizarCorCapaModal);
    document.getElementById("cartaoCorCapa")?.addEventListener("change", atualizarCorCapaModal);

    document.getElementById("btnFocoDatas")?.addEventListener("click", () => focarSecao("secaoDatas"));
    document.getElementById("btnFocoMembros")?.addEventListener("click", abrirModalMembrosCartao);
    document.getElementById("btnFocoChamados")?.addEventListener("click", abrirModalChamadosCartao);
    document.getElementById("btnSalvarMembrosCartao")?.addEventListener("click", salvarMembrosCartao);
    document.getElementById("btnSalvarChamadosCartao")?.addEventListener("click", salvarChamadosCartao);
    document.getElementById("cartaoChamadosBusca")?.addEventListener("input", event => {
        clearTimeout(buscaChamadosCartaoTimer);
        buscaChamadosCartaoTimer = setTimeout(() => carregarChamadosModal(String(event.target.value || "")), 250);
    });
    document.getElementById("btnAlternarEtiquetasTarefa")?.addEventListener("click", alternarPainelEtiquetasTarefa);
    document.getElementById("btnCriarEtiquetaTarefa")?.addEventListener("click", criarEtiquetaTarefa);
    document.getElementById("btnCriarChecklistTarefa")?.addEventListener("click", criarChecklistTarefa);
    document.getElementById("btnEnviarAnexoTarefa")?.addEventListener("click", enviarAnexoTarefa);
    modalCartaoElement?.addEventListener("hide.bs.modal", cancelarUploadAnexoTarefa);
    modalVisualizarAnexoElement?.addEventListener("hide.bs.modal", limparPreviewAnexoTarefa);
    modalChamadosCartaoElement?.addEventListener("hide.bs.modal", cancelarBuscaChamadosModal);
    document.getElementById("cartaoGrupoTodoModal")?.addEventListener("change", atualizarMembrosModalGrupoTodo);
    document.getElementById("cartaoMembros")?.addEventListener("change", () => {
        atualizarResumoMembros();
        carregarChamadosPermitidos();
    });
    document.getElementById("cartaoChamados")?.addEventListener("change", atualizarResumoChamados);
    document.getElementById("cartaoCompartilharGrupo")?.addEventListener("change", () => {
        const compartilharGrupo = !!document.getElementById("cartaoCompartilharGrupo")?.checked;
        document.getElementById("cartaoPrivacidadeBadge").textContent = compartilharGrupo ? "Grupo" : "Privado";
        cartaoAtualEstado.compartilharGrupo = compartilharGrupo;
        atualizarEstadoModalCartao();
        atualizarResumoMembros();
        carregarChamadosPermitidos();
    });

    document.querySelectorAll("[data-add-card-column]").forEach(btn => {
        btn.addEventListener("click", () => abrirModalCartaoNovo(Number(btn.dataset.addCardColumn)));
    });
    document.querySelectorAll("[data-template-column]").forEach(inicializarBotaoTemplate);

    document.addEventListener("click", event => {
        if (!painelTemplates || painelTemplates.classList.contains("d-none")) return;
        if (painelTemplates.contains(event.target) ||
            event.target.closest("[data-template-column]") ||
            event.target.closest(".modal")) return;

        fecharPainelTemplates();
    });

    document.addEventListener("click", event => {
        const painel = document.getElementById("painelArquivados");
        if (!painel || painel.classList.contains("d-none")) return;
        if (painel.contains(event.target) || event.target.closest("#btnAbrirArquivados") || event.target.closest(".modal")) return;

        fecharPainelArquivados();
    });

    inicializarBoard();
});

function inicializarBoard() {
    document.querySelectorAll(".task-list[data-column-id]").forEach(inicializarLista);
    document.querySelectorAll(".task-card").forEach(inicializarCard);
    document.querySelectorAll(".task-cards").forEach(inicializarDropZone);
    inicializarOrdenacaoListas();
    inicializarPopoversMembros(document);
}

function inicializarLista(lista) {
    const colunaId = Number(lista.dataset.columnId);
    const handle = lista.querySelector(".task-list-drag-handle");

    handle?.addEventListener("dragstart", event => {
        snapshotListasAntesDrag = capturarSnapshotListas();
        draggedList = lista;
        lista.classList.add("dragging-list");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", String(colunaId));
    });

    handle?.addEventListener("dragend", async () => {
        lista.classList.remove("dragging-list");
        draggedList = null;

        const snapshotListasDepoisDrag = capturarSnapshotListas();
        if (!snapshotsListasIguais(snapshotListasAntesDrag, snapshotListasDepoisDrag)) {
            await salvarOrdemListas(snapshotListasDepoisDrag);
            sincronizarSelectColunasComBoard();
        }

        snapshotListasAntesDrag = null;
    });

    lista.querySelector("[data-rename-list]")?.addEventListener("click", () => renomearLista(colunaId));
    lista.querySelector("[data-list-actions]")?.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        abrirAcoesLista(colunaId);
    });
}

function inicializarOrdenacaoListas() {
    const board = getBoard();
    if (!board) return;

    board.addEventListener("dragover", event => {
        if (!draggedList) return;

        event.preventDefault();
        const addListPanel = board.querySelector(".add-list-panel");
        const afterElement = obterListaAposCursor(board, event.clientX);

        if (afterElement == null) {
            board.insertBefore(draggedList, addListPanel);
        } else {
            board.insertBefore(draggedList, afterElement);
        }
    });

    board.addEventListener("drop", event => {
        if (!draggedList) return;
        event.preventDefault();
    });
}

function inicializarCard(card) {
    card.addEventListener("click", () => abrirModalCartaoExistente(Number(card.dataset.cardId)));
    inicializarInteracoesPopoverMembros(card);
    card.querySelector("[data-toggle-done]")?.addEventListener("click", async event => {
        event.preventDefault();
        event.stopPropagation();
        await alternarConcluido(Number(card.dataset.cardId), card);
    });

    card.addEventListener("dragstart", event => {
        snapshotBoardAntesDrag = capturarSnapshotBoard();
        draggedCard = card;
        card.classList.add("dragging");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", card.dataset.cardId);
    });

    card.addEventListener("dragend", async () => {
        card.classList.remove("dragging");
        draggedCard = null;
        document.querySelectorAll(".task-cards").forEach(zone => zone.classList.remove("drag-over"));

        const snapshotBoardDepoisDrag = capturarSnapshotBoard();
        if (!snapshotsBoardIguais(snapshotBoardAntesDrag, snapshotBoardDepoisDrag)) {
            await salvarOrdemBoard();
        }

        snapshotBoardAntesDrag = null;
    });
}

function inicializarPopoversMembros(root) {
    inicializarInteracoesPopoverMembros(root);
    if (!window.bootstrap?.Popover) return;

    root.querySelectorAll("[data-task-members-popover], [data-task-calls-popover]").forEach(elemento => {
        bootstrap.Popover.getOrCreateInstance(elemento, {
            container: "body",
            customClass: "task-member-popover",
            html: true,
            placement: "top",
            trigger: "hover focus"
        });
    });
}

function inicializarInteracoesPopoverMembros(root) {
    root.querySelectorAll("[data-task-members-popover], [data-task-calls-popover]").forEach(botao => {
        if (botao.dataset.taskMembersEventsInitialized === "true") return;

        botao.dataset.taskMembersEventsInitialized = "true";
        botao.addEventListener("click", event => event.stopPropagation());
        botao.addEventListener("mousedown", event => event.stopPropagation());
    });
}

function inicializarDropZone(zone) {
    zone.addEventListener("dragover", event => {
        event.preventDefault();
        if (!draggedCard) return;

        zone.classList.add("drag-over");
        const afterElement = obterElementoAposCursor(zone, event.clientY);
        if (afterElement == null) {
            zone.appendChild(draggedCard);
        } else {
            zone.insertBefore(draggedCard, afterElement);
        }
    });

    zone.addEventListener("dragleave", () => {
        zone.classList.remove("drag-over");
    });

    zone.addEventListener("drop", event => {
        event.preventDefault();
        zone.classList.remove("drag-over");
    });
}

function obterElementoAposCursor(container, y) {
    const cards = [...container.querySelectorAll(".task-card:not(.dragging)")];
    return cards.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        if (offset < 0 && offset > closest.offset) {
            return { offset, element: child };
        }

        return closest;
    }, { offset: Number.NEGATIVE_INFINITY, element: null }).element;
}

function obterListaAposCursor(container, x) {
    const listas = [...container.querySelectorAll(".task-list[data-column-id]:not(.dragging-list)")];
    return listas.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = x - box.left - box.width / 2;
        if (offset < 0 && offset > closest.offset) {
            return { offset, element: child };
        }

        return closest;
    }, { offset: Number.NEGATIVE_INFINITY, element: null }).element;
}

function capturarSnapshotListas() {
    return [...document.querySelectorAll(".task-list[data-column-id]")]
        .map(coluna => Number(coluna.dataset.columnId))
        .filter(Number.isFinite);
}

function snapshotsListasIguais(snapshotA, snapshotB) {
    if (!snapshotA || !snapshotB || snapshotA.length !== snapshotB.length) {
        return false;
    }

    return snapshotA.every((colunaId, index) => colunaId === snapshotB[index]);
}

function capturarSnapshotBoard() {
    return [...document.querySelectorAll(".task-list[data-column-id]")].map(coluna => ({
        colunaId: Number(coluna.dataset.columnId),
        cartoesIds: [...coluna.querySelectorAll(".task-card")].map(card => Number(card.dataset.cardId))
    }));
}

function snapshotsBoardIguais(snapshotA, snapshotB) {
    if (!snapshotA || !snapshotB || snapshotA.length !== snapshotB.length) {
        return false;
    }

    return snapshotA.every((colunaA, index) => {
        const colunaB = snapshotB[index];
        if (!colunaB || colunaA.colunaId !== colunaB.colunaId) {
            return false;
        }

        if (colunaA.cartoesIds.length !== colunaB.cartoesIds.length) {
            return false;
        }

        return colunaA.cartoesIds.every((cartaoId, cartaoIndex) =>
            cartaoId === colunaB.cartoesIds[cartaoIndex]);
    });
}

function abrirModalLista() {
    document.getElementById("formLista").reset();
    modalLista.show();
    setTimeout(() => document.getElementById("listaNome")?.focus(), 150);
}

async function criarLista(event) {
    event.preventDefault();
    const nome = document.getElementById("listaNome")?.value.trim();
    if (!nome) return;

    let data;
    try {
        data = await fetchJson("?handler=CriarLista", {
            grupoId: getGrupoId(),
            nome
        });
    } catch (error) {
        mostrarToast(error.message || "Não foi possível criar a lista.");
        return;
    }

    if (!data.success) {
        mostrarToast(data.message || "Não foi possível criar a lista.");
        return;
    }

    inserirListaNoBoard(data.id, data.nome);
    modalLista.hide();
    document.getElementById("formLista").reset();
}

function abrirAcoesLista(colunaId) {
    listaAcoesAtualId = colunaId;
    modalAcoesLista?.show();
}

async function renomearLista(colunaId) {
    if (!colunaId) return;

    const titulo = document.querySelector(`.task-list[data-column-id="${colunaId}"] [data-rename-list]`);
    const nomeAtual = titulo?.textContent?.trim() || "";
    const nome = prompt("Nome da lista", nomeAtual)?.trim();
    if (!nome || nome === nomeAtual) return;

    try {
        const data = await fetchJson("?handler=RenomearLista", {
            grupoId: getGrupoId(),
            colunaId,
            nome
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível renomear a lista.");
            return;
        }

        if (titulo) {
            titulo.textContent = data.nome || nome;
        }

        const option = document.querySelector(`#cartaoColunaSelect option[value="${colunaId}"]`);
        if (option) {
            option.textContent = data.nome || nome;
        }

        modalAcoesLista?.hide();
        mostrarToast("Lista renomeada com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível renomear a lista.");
    }
}

async function arquivarCartoesListaAtual() {
    const colunaId = listaAcoesAtualId;
    if (!colunaId) return;
    if (!confirm("Arquivar todos os cartoes desta lista?")) return;

    try {
        const data = await fetchJson("?handler=ArquivarCartoesLista", {
            grupoId: getGrupoId(),
            colunaId
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível arquivar os cartoes da lista.");
            return;
        }

        const lista = document.querySelector(`.task-list[data-column-id="${colunaId}"]`);
        lista?.querySelectorAll(".task-card").forEach(card => card.remove());
        atualizarContadorLista(lista);
        modalAcoesLista?.hide();
        mostrarToast("Cartoes arquivados com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível arquivar os cartoes da lista.");
    }
}

async function excluirListaAtual() {
    const colunaId = listaAcoesAtualId;
    if (!colunaId) return;
    if (!confirm("Excluir esta lista? Todos os cartoes dentro dela serão arquivados.")) return;

    try {
        const data = await fetchJson("?handler=ExcluirLista", {
            grupoId: getGrupoId(),
            colunaId
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível excluir a lista.");
            return;
        }

        document.querySelector(`.task-list[data-column-id="${colunaId}"]`)?.remove();
        document.querySelector(`#cartaoColunaSelect option[value="${colunaId}"]`)?.remove();
        modalAcoesLista?.hide();
        mostrarToast("Lista excluida com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível excluir a lista.");
    }
}

async function abrirModalCartaoNovo(colunaId) {
    document.getElementById("formCartao").reset();
    setValue("cartaoId", "");
    setValue("cartaoColunaId", colunaId);
    setValue("cartaoColunaSelect", colunaId);
    setValue("cartaoCorCapa", "#0d6efd");
    const statusNovo = document.getElementById("cartaoStatus");
    if (statusNovo) {
        statusNovo.value = "Ativa";
        statusNovo.dataset.originalStatus = "Ativa";
    }
    atualizarCorCapaModal();
    document.getElementById("cartaoCompartilharGrupo").checked = false;
    document.getElementById("cartaoPrivacidadeBadge").textContent = "Privado";
    document.getElementById("cartaoAtividade").innerHTML = '<div class="text-muted small">Sem atividade carregada.</div>';
    selecionarMultiplos("cartaoMembros", []);
    substituirOpcoesChamados([]);
    cartaoAtualEstado = {
        membros: [],
        membrosVisiveis: [],
        chamados: [],
        chamadosVinculados: [],
        compartilharGrupo: false,
        arquivado: false,
        podeEditar: true,
        podeSairVinculo: false,
        podeGerenciarEtiquetas: true,
        etiquetasDisponiveis: [],
        etiquetasAplicadas: [],
        checklists: [],
        anexos: [],
        titulo: "",
        colunaId
    };
    cartaoSnapshotSalvar = null;
    atualizarEstadoModalCartao();
    atualizarResumoMembros();
    atualizarResumoChamados();
    await carregarEtiquetasTarefaUsuario();
    renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, []);
    renderizarChecklistsTarefa([]);
    renderizarAnexosTarefa([]);
    ocultarSalvarComoTemplate();
    document.getElementById("btnArquivarCartao")?.classList.add("d-none");
    document.getElementById("btnSairCartao")?.classList.add("d-none");
    modalCartao.show();
    setTimeout(() => document.getElementById("cartaoTitulo")?.focus(), 150);
}

async function abrirModalCartaoExistente(id) {
    fecharPainelArquivados();

    let data;
    try {
        data = await fetch(`?handler=Cartao&id=${encodeURIComponent(id)}&grupoId=${encodeURIComponent(getGrupoId())}`)
            .then(r => {
                if (!r.ok) throw new Error(`Erro HTTP: ${r.status}`);
                return r.json();
            });
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar o cartão.");
        return;
    }

    if (!data.success) {
        mostrarToast(data.message || "Não foi possível carregar o cartão.");
        return;
    }

    document.getElementById("formCartao").reset();
    setValue("cartaoId", data.id);
    setValue("cartaoColunaId", data.colunaId);
    setValue("cartaoColunaSelect", data.colunaId);
    setValue("cartaoTitulo", data.titulo);
    setValueComMencoes("cartaoDescricao", data.descricao);
    setValue("cartaoPrioridade", data.prioridade);
    setValue("cartaoCriticidade", data.criticidade);
    setValue("cartaoUrgencia", data.urgencia);
    setValue("cartaoStatus", data.status || "Ativa");
    const statusSelect = document.getElementById("cartaoStatus");
    if (statusSelect) statusSelect.dataset.originalStatus = data.status || "Ativa";
    setValue("cartaoDataInicio", formatDateTimeLocal(data.dataInicio));
    setValue("cartaoDataVencimento", formatDateTimeLocal(data.dataVencimento));
    setValue("cartaoCorCapa", data.corCapa || "#0d6efd");
    atualizarCorCapaModal();

    document.getElementById("cartaoCompartilharGrupo").checked = !!data.compartilharGrupo;
    document.getElementById("cartaoPrivacidadeBadge").textContent = data.compartilharGrupo ? "Grupo" : "Privado";
    selecionarMultiplos("cartaoMembros", data.membros || []);
    substituirOpcoesChamados(data.chamadosOpcoes || []);
    selecionarMultiplos("cartaoChamados", data.chamados || []);
    cartaoAtualEstado = {
        membros: data.membros || [],
        membrosVisiveis: data.membrosVisiveis || [],
        chamados: data.chamados || [],
        chamadosVinculados: data.chamadosVinculados || [],
        compartilharGrupo: !!data.compartilharGrupo,
        arquivado: !!data.arquivado,
        podeEditar: !!data.podeEditar,
        podeSairVinculo: !!data.podeSairVinculo,
        podeGerenciarEtiquetas: true,
        etiquetasDisponiveis: data.etiquetasDisponiveis || [],
        etiquetasAplicadas: data.etiquetasAplicadas || [],
        checklists: data.checklists || [],
        anexos: data.anexos || [],
        titulo: data.titulo || "",
        colunaId: data.colunaId
    };
    cartaoSnapshotSalvar = normalizarPayloadCartao(montarPayloadSalvarCartao(data.id));
    atualizarEstadoModalCartao();
    atualizarResumoMembros();
    atualizarResumoChamados(cartaoAtualEstado.chamadosVinculados);
    renderizarEtiquetasTarefa(data.etiquetasDisponiveis || [], data.etiquetasAplicadas || []);
    renderizarChecklistsTarefa(data.checklists || []);
    renderizarAnexosTarefa(data.anexos || []);
    renderizarAtividade(data.atividade || []);
    ocultarSalvarComoTemplate();

    configurarEdicao(!!data.podeEditar);
    modalCartao.show();
}

async function sairCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId || !cartaoAtualEstado.podeSairVinculo) {
        mostrarToast("Não é possível sair desta tarefa.");
        return;
    }

    if (!confirm("Deseja sair desta tarefa? Você deixará de ter acesso direto a ela.")) {
        return;
    }

    try {
        const data = await fetchJson("?handler=SairCartao", {
            cartaoId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível sair da tarefa.");
            return;
        }

        mostrarToast(data.message || "Você saiu da tarefa.", "success");
        modalCartao?.hide();
        window.location.reload();
    } catch (error) {
        mostrarToast(error.message || "Não foi possível sair da tarefa.");
    }
}

async function salvarCartao(event) {
    event.preventDefault();

    const id = toNullableInt(getValue("cartaoId"));
    const payload = montarPayloadSalvarCartao(id);
    if (!preencherObservacaoPendenteCartao(payload)) {
        return;
    }

    if (id && cartaoSnapshotSalvar && !cartaoPossuiAlteracoes(payload)) {
        mostrarToast("Não há alterações feitas.", "warning");
        return;
    }

    let data;
    try {
        data = await fetchJson("?handler=SalvarCartao", payload);
    } catch (error) {
        mostrarToast(error.message || "Não foi possível salvar o cartão.");
        return;
    }

    if (!data.success) {
        mostrarToast(data.message || "Não foi possível salvar o cartão.");
        return;
    }

    modalCartao.hide();
    document.getElementById("formCartao").reset();
    window.location.reload();
}

function montarPayloadSalvarCartao(id) {
    return {
        id,
        grupoId: getGrupoId(),
        colunaId: Number(getValue("cartaoColunaSelect") || getValue("cartaoColunaId")),
        titulo: getValue("cartaoTitulo"),
        descricao: getNullableStringSerializado("cartaoDescricao"),
        prioridade: getNullableString("cartaoPrioridade"),
        criticidade: getNullableString("cartaoCriticidade"),
        urgencia: getNullableString("cartaoUrgencia"),
        status: getNullableString("cartaoStatus"),
        dataInicio: getNullableString("cartaoDataInicio"),
        dataVencimento: getNullableString("cartaoDataVencimento"),
        corCapa: getNullableString("cartaoCorCapa"),
        compartilharGrupo: !!document.getElementById("cartaoCompartilharGrupo")?.checked,
        membrosIds: getSelectedNumbers("cartaoMembros"),
        chamadosIds: getSelectedNumbers("cartaoChamados"),
        etiquetasIds: [...document.querySelectorAll("[data-etiqueta-tarefa]:checked")].map(input => Number(input.dataset.etiquetaTarefa))
    };
}

function preencherObservacaoPendenteCartao(payload) {
    const statusSelect = document.getElementById("cartaoStatus");
    const statusAnterior = statusSelect?.dataset.originalStatus || "Ativa";
    const statusNovo = payload.status || "Ativa";

    if (statusAnterior !== "Pendente" && statusNovo === "Pendente") {
        const valor = prompt("Observacao opcional ao colocar a tarefa em Pendente");
        if (valor === null) return false;
        const observacao = valor.trim();
        if (observacao) payload.observacaoPendenteEntrada = observacao;
    } else if (statusAnterior === "Pendente" && statusNovo !== "Pendente") {
        const valor = prompt("Observacao opcional ao retirar a tarefa de Pendente");
        if (valor === null) return false;
        const observacao = valor.trim();
        if (observacao) payload.observacaoPendenteSaida = observacao;
    }

    return true;
}

function cartaoPossuiAlteracoes(payload) {
    return JSON.stringify(normalizarPayloadCartao(payload)) !== JSON.stringify(cartaoSnapshotSalvar);
}

function normalizarPayloadCartao(payload) {
    return {
        id: toNullableInt(payload.id),
        grupoId: Number(payload.grupoId) || 0,
        colunaId: Number(payload.colunaId) || 0,
        titulo: normalizarTextoComparacao(payload.titulo),
        descricao: normalizarTextoComparacao(payload.descricao),
        prioridade: normalizarTextoComparacao(payload.prioridade),
        criticidade: normalizarTextoComparacao(payload.criticidade),
        urgencia: normalizarTextoComparacao(payload.urgencia),
        status: normalizarTextoComparacao(payload.status || "Ativa"),
        dataInicio: normalizarTextoComparacao(payload.dataInicio),
        dataVencimento: normalizarTextoComparacao(payload.dataVencimento),
        corCapa: normalizarTextoComparacao(payload.corCapa || "#0d6efd").toLowerCase(),
        compartilharGrupo: !!payload.compartilharGrupo,
        membrosIds: normalizarListaNumeros(payload.membrosIds),
        chamadosIds: normalizarListaNumeros(payload.chamadosIds)
    };
}

function normalizarTextoComparacao(value) {
    const texto = String(value ?? "").trim();
    return texto === "" ? null : texto;
}

function normalizarListaNumeros(values) {
    return [...new Set((values || []).map(Number).filter(Number.isFinite))]
        .sort((a, b) => a - b);
}

async function salvarOrdemBoard() {
    const colunas = capturarSnapshotBoard();

    try {
        await fetchJson("?handler=ReordenarCartoes", {
            grupoId: getGrupoId(),
            colunas
        });
    } catch (error) {
        console.error(error);
        mostrarToast("Não foi possível salvar a nova ordem dos cartoes.");
        window.location.reload();
    }
}

async function salvarOrdemListas(colunasIds = capturarSnapshotListas()) {
    try {
        await fetchJson("?handler=ReordenarListas", {
            grupoId: getGrupoId(),
            colunasIds
        });
    } catch (error) {
        console.error(error);
        mostrarToast("Não foi possível salvar a nova ordem das listas.");
        window.location.reload();
    }
}

async function alternarConcluido(cartaoId, card) {
    if (!cartaoId || !card) return;

    try {
        const data = await fetchJson("?handler=AlternarConcluido", {
            grupoId: getGrupoId(),
            cartaoId
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível atualizar a tarefa.");
            return;
        }

        aplicarEstadoConcluido(card, !!data.concluido);
    } catch (error) {
        mostrarToast(error.message || "Não foi possível atualizar a tarefa.");
    }
}

async function alternarArquivamentoCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return;

    if (cartaoAtualEstado.arquivado) {
        await restaurarCartao(cartaoId);
        return;
    }

    if (!confirm("Arquivar este cartão? Ele sairá do board mas poderá ser restaurado.")) {
        return;
    }

    try {
        const data = await fetchJson("?handler=ArquivarCartao", {
            cartaoId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível arquivar o cartão.");
            return;
        }

        const card = document.querySelector(`.task-card[data-card-id="${cartaoId}"]`);
        const lista = card?.closest(".task-list");
        card?.remove();
        atualizarContadorLista(lista);
        modalCartao?.hide();
        mostrarToast("Cartao arquivado com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível arquivar o cartão.");
    }
}

async function restaurarCartao(cartaoId) {
    try {
        const data = await fetchJson("?handler=RestaurarCartao", {
            cartaoId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível restaurar o cartão.");
            return;
        }

        modalCartao?.hide();
        removerItemArquivado(cartaoId);

        const colunaCards = document.querySelector(`.task-cards[data-column-cards="${data.colunaId}"]`);
        if (colunaCards) {
            inserirCardNoBoard(cartaoId, {
                colunaId: data.colunaId,
                titulo: cartaoAtualEstado.titulo || getValue("cartaoTitulo") || "Cartão",
                compartilharGrupo: cartaoAtualEstado.compartilharGrupo,
                membrosVisiveis: data.membrosVisiveis || cartaoAtualEstado.membrosVisiveis || [],
                chamadosVinculados: data.chamadosVinculados || cartaoAtualEstado.chamadosVinculados || []
            });
            mostrarToast("Cartao restaurado com sucesso.", "success");
        } else {
            mostrarToast("Cartao restaurado. Atualize a página para ver na lista correta.", "success");
        }
    } catch (error) {
        mostrarToast(error.message || "Não foi possível restaurar o cartão.");
    }
}

function aplicarEstadoConcluido(card, concluido) {
    card.dataset.concluido = String(concluido);
    card.classList.toggle("task-card-done", concluido);

    const botao = card.querySelector("[data-toggle-done]");
    const icone = botao?.querySelector("i");
    if (botao) {
        botao.classList.toggle("is-done", concluido);
    }
    if (icone) {
        icone.className = concluido ? "bi bi-check-lg" : "bi";
    }
}

async function abrirPainelArquivados() {
    const painel = document.getElementById("painelArquivados");
    const lista = document.getElementById("listaCartoesArquivados");
    if (!painel || !lista) return;

    painel.classList.remove("d-none");
    lista.innerHTML = '<div class="text-muted small">Carregando...</div>';

    try {
        const response = await fetch(`?handler=CartoesArquivados&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar os cartoes arquivados.");
            return;
        }

        renderizarCartoesArquivados(data.cartoes || []);
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar os cartoes arquivados.");
        lista.innerHTML = '<div class="text-muted small">Nenhum cartão arquivado.</div>';
    }
}

function fecharPainelArquivados() {
    document.getElementById("painelArquivados")?.classList.add("d-none");
}

function renderizarCartoesArquivados(cartoes) {
    const lista = document.getElementById("listaCartoesArquivados");
    if (!lista) return;

    lista.innerHTML = cartoes.length
        ? cartoes.map(cartao => {
            const data = cartao.dataArquivamento ? new Date(cartao.dataArquivamento).toLocaleString("pt-BR") : "";
            const cor = cartao.corCapa ? `<div class="arquivado-cover" style="background:${normalizarCorTemplate(cartao.corCapa)}"></div>` : "";
            return `
                <button type="button" class="list-group-item list-group-item-action mb-2 border rounded-1 overflow-hidden p-0" data-arquivado-id="${cartao.id}">
                    ${cor}
                    <div class="p-2">
                        <div class="fw-semibold">${escapeHtml(cartao.titulo || "Cartão")}</div>
                        <div class="small text-muted">${escapeHtml(cartao.nomeColuna || "Lista")}</div>
                        <div class="small text-muted">${escapeHtml(data)}</div>
                    </div>
                </button>
            `;
        }).join("")
        : '<div class="text-muted small">Nenhum cartão arquivado.</div>';

    lista.querySelectorAll("[data-arquivado-id]").forEach(item => {
        item.addEventListener("click", () => abrirModalCartaoExistente(Number(item.dataset.arquivadoId)));
    });
}

function removerItemArquivado(cartaoId) {
    document.querySelector(`[data-arquivado-id="${cartaoId}"]`)?.remove();
    const lista = document.getElementById("listaCartoesArquivados");
    if (lista && !lista.querySelector("[data-arquivado-id]")) {
        lista.innerHTML = '<div class="text-muted small">Nenhum cartão arquivado.</div>';
    }
}

function inserirListaNoBoard(id, nome) {
    const board = getBoard();
    const addListPanel = board?.querySelector(".add-list-panel");
    if (!board || !addListPanel) return;

    const lista = document.createElement("section");
    lista.className = "task-list";
    lista.dataset.columnId = id;
    lista.innerHTML = `
        <div class="task-list-header">
            <span class="task-list-drag-handle" title="Mover lista" aria-label="Mover lista" draggable="true">
                <i class="bi bi-grip-vertical"></i>
            </span>
            <button type="button" class="task-list-title" data-rename-list="${id}">${escapeHtml(nome)}</button>
            <div class="d-flex align-items-center gap-2">
                <span class="task-list-count">0</span>
                <button type="button" class="btn btn-sm btn-link task-list-menu" data-list-actions="${id}" aria-label="Acoes da lista">
                    <i class="bi bi-three-dots"></i>
                </button>
            </div>
        </div>

        <div class="task-cards" data-column-cards="${id}"></div>

        <div class="d-flex align-items-center gap-1 px-2 pb-2">
            <button type="button" class="btn-add-card flex-grow-1 m-0" style="width:auto;" data-add-card-column="${id}">
                <i class="bi bi-plus-lg"></i>
                Adicionar cartão
            </button>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-template-column="${id}" title="Templates">
                <i class="bi bi-layout-text-sidebar"></i>
            </button>
        </div>
    `;

    board.insertBefore(lista, addListPanel);
    inicializarLista(lista);

    const cardsContainer = lista.querySelector(".task-cards");
    if (cardsContainer) {
        inicializarDropZone(cardsContainer);
    }

    lista.querySelector("[data-add-card-column]")?.addEventListener("click", () =>
        abrirModalCartaoNovo(Number(id)));
    lista.querySelector("[data-template-column]")?.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        abrirModalSelecionarTemplate(Number(id));
    });

    const selectColunas = document.getElementById("cartaoColunaSelect");
    if (selectColunas) {
        const option = document.createElement("option");
        option.value = id;
        option.textContent = nome;
        selectColunas.appendChild(option);
    }

    sincronizarSelectColunasComBoard();
}

function inserirCardNoBoard(id, payload) {
    const colunaCards = document.querySelector(`.task-cards[data-column-cards="${payload.colunaId}"]`);
    if (!colunaCards) return;

    const card = document.createElement("article");
    card.className = "task-card";
    card.draggable = true;
    card.dataset.cardId = id;
    card.dataset.concluido = "false";
    card.dataset.compartilharGrupo = String(!!payload.compartilharGrupo);
    const capa = payload.corCapa ? `<div class="task-cover" style="background:${normalizarCorTemplate(payload.corCapa)}"></div>` : "";
    card.innerHTML = `
        ${capa}
        <div class="task-card-main">
            <button type="button" class="task-done-toggle" data-toggle-done="${id}" aria-label="Alternar conclusão">
                <i class="bi"></i>
            </button>
            <div class="task-title">${escapeHtml(payload.titulo)}</div>
        </div>
        <div class="task-meta">
            ${renderizarChamadosCard(payload.chamadosVinculados || payload.chamados || [])}
            ${renderizarIndicadorChecklistMeta(payload.checklistsConcluidos || 0, payload.checklistsTotal || 0)}
            ${renderizarAcessoCard(payload.membrosVisiveis || [], !!payload.compartilharGrupo)}
        </div>
    `;

    colunaCards.appendChild(card);
    inicializarCard(card);
    inicializarPopoversMembros(card);
    atualizarContadorLista(colunaCards.closest(".task-list"));
}

function atualizarCardNoBoard(id, payload) {
    const card = document.querySelector(`.task-card[data-card-id="${id}"]`);
    if (!card) return;

    const colunaCardsAtual = card.closest(".task-cards");
    const colunaCardsDestino = document.querySelector(`.task-cards[data-column-cards="${payload.colunaId}"]`);

    if (colunaCardsDestino && colunaCardsAtual !== colunaCardsDestino) {
        colunaCardsDestino.appendChild(card);
        atualizarContadorLista(colunaCardsAtual?.closest(".task-list"));
        atualizarContadorLista(colunaCardsDestino.closest(".task-list"));
    }

    const titulo = card.querySelector(".task-title");
    if (titulo) {
        titulo.textContent = payload.titulo;
    }

    if (payload.checklistsTotal !== undefined || payload.checklistsConcluidos !== undefined) {
        atualizarIndicadorChecklistCard(
            id,
            Number(payload.checklistsConcluidos || 0),
            Number(payload.checklistsTotal || 0));
    }

    if (Array.isArray(payload.membrosVisiveis)) {
        const compartilharGrupo = typeof payload.compartilharGrupo === "boolean"
            ? payload.compartilharGrupo
            : card.dataset.compartilharGrupo === "true";
        atualizarIndicadorAcessoCard(id, payload.membrosVisiveis, compartilharGrupo);
    }

    if (Array.isArray(payload.chamadosVinculados)) {
        atualizarIndicadorChamadosCard(id, payload.chamadosVinculados);
    }
}

function renderizarChamadosCard(chamadosVinculados) {
    const chamados = Array.isArray(chamadosVinculados) ? chamadosVinculados : [];
    if (!chamados.length) return "";

    const preview = chamados.slice(0, 3)
        .map(chamado => Number(chamado.numeroChamadoGrupo))
        .filter(Number.isFinite)
        .map(numero => `#${numero}`)
        .join(", ");
    if (!preview) return "";

    const restantes = chamados.slice(3);
    const extras = restantes.length
        ? `<button type="button"
                   class="task-calls-more"
                   data-task-calls-popover
                   data-bs-content="${escapeHtml(renderizarPopoverChamados(restantes))}"
                   aria-label="Ver outros chamados vinculados">...</button>`
        : "";

    return `<span class="task-calls-summary" data-card-calls-meta><i class="bi bi-ticket-detailed"></i><span>${escapeHtml(preview)}</span>${extras}</span>`;
}

function renderizarPopoverChamados(chamados) {
    return chamados.map(chamado => {
        const numero = Number(chamado.numeroChamadoGrupo);
        const prefixo = Number.isFinite(numero) ? `#${numero}` : "Chamado";
        return `
            <div class="task-call-popover-row">
                <i class="bi bi-ticket-detailed"></i>
                <span>${escapeHtml(`${prefixo} - ${chamado.titulo || "Chamado"}`)}</span>
            </div>
        `;
    }).join("");
}

function atualizarIndicadorChamadosCard(cartaoId, chamadosVinculados) {
    const card = document.querySelector(`.task-card[data-card-id="${cartaoId}"]`);
    const meta = card?.querySelector(".task-meta");
    if (!card || !meta) return;

    const existente = meta.querySelector("[data-card-calls-meta]");
    const popoverExistente = existente?.querySelector("[data-task-calls-popover]");
    if (popoverExistente && window.bootstrap?.Popover) {
        bootstrap.Popover.getInstance(popoverExistente)?.dispose();
    }
    existente?.remove();
    meta.insertAdjacentHTML("afterbegin", renderizarChamadosCard(chamadosVinculados));
    inicializarPopoversMembros(meta);
}

function renderizarAcessoCard(membrosVisiveis, compartilharGrupo = false) {
    if (compartilharGrupo) {
        return '<span data-card-access-meta><i class="bi bi-people"></i> Grupo</span>';
    }

    const membros = Array.isArray(membrosVisiveis) ? membrosVisiveis : [];
    if (membros.length <= 1) {
        return '<span data-card-access-meta><i class="bi bi-lock"></i> Privado</span>';
    }

    const preview = membros.slice(0, 3)
        .map(membro => `<img src="${escapeHtml(obterFotoMembro(membro))}" alt="${escapeHtml(membro.nomeExibicao || "Membro")}" class="task-avatar-mini" loading="lazy">`)
        .join("");
    const excedente = membros.length > 3
        ? '<span class="task-avatar-more" aria-hidden="true">...</span>'
        : "";
    const popover = membros.map(membro => `
        <div class="task-member-popover-row">
            <img src="${escapeHtml(obterFotoMembro(membro))}" alt="">
            <span>${escapeHtml(membro.nomeExibicao || "Membro")}</span>
        </div>
    `).join("");

    return `
        <span class="task-access-meta task-shared-summary" data-card-access-meta>
            <button type="button"
                    class="task-avatar-stack"
                    data-task-members-popover
                    data-bs-content="${escapeHtml(popover)}"
                    aria-label="Ver membros compartilhados">
                ${preview}${excedente}
            </button>
            <span>Compartilhado</span>
        </span>
    `;
}

function obterFotoMembro(membro) {
    return membro?.fotoUsuario || "/images/default-user.png";
}

function atualizarIndicadorAcessoCard(cartaoId, membrosVisiveis, compartilharGrupo = false) {
    const card = document.querySelector(`.task-card[data-card-id="${cartaoId}"]`);
    const meta = card?.querySelector(".task-meta");
    if (!card || !meta) return;

    card.dataset.compartilharGrupo = String(!!compartilharGrupo);
    const existente = meta.querySelector("[data-card-access-meta]");
    const popoverExistente = existente?.querySelector("[data-task-members-popover]");
    if (popoverExistente && window.bootstrap?.Popover) {
        bootstrap.Popover.getInstance(popoverExistente)?.dispose();
    }
    existente?.remove();
    meta.insertAdjacentHTML("beforeend", renderizarAcessoCard(membrosVisiveis, compartilharGrupo));
    inicializarPopoversMembros(meta);
}

function renderizarIndicadorChecklistMeta(concluidos, total) {
    if (!total) return "";

    return `<span class="task-checklist-progress" data-card-checklist-progress><i class="bi bi-check2-square"></i> ${Number(concluidos || 0)}/${Number(total || 0)}</span>`;
}

function calcularProgressoChecklists(checklists) {
    const itens = (checklists || []).flatMap(checklist => checklist.itens || []);
    return {
        total: itens.length,
        concluidos: itens.filter(item => item.concluido).length
    };
}

function atualizarIndicadorChecklistCard(cartaoId, concluidos, total) {
    const card = document.querySelector(`.task-card[data-card-id="${cartaoId}"]`);
    const meta = card?.querySelector(".task-meta");
    if (!card || !meta) return;

    const existente = meta.querySelector("[data-card-checklist-progress]");
    if (!total) {
        existente?.remove();
        return;
    }

    if (existente) {
        existente.innerHTML = `<i class="bi bi-check2-square"></i> ${Number(concluidos || 0)}/${Number(total || 0)}`;
        return;
    }

    meta.insertAdjacentHTML("afterbegin", renderizarIndicadorChecklistMeta(concluidos, total));
}

function atualizarCorCapaModal() {
    const capa = document.querySelector("#modalCartao .task-modal-cover");
    if (!capa) return;

    const cor = getNullableString("cartaoCorCapa") || "#0d6efd";
    capa.style.background = normalizarCorTemplate(cor);
}

function inicializarBotaoTemplate(botao) {
    botao.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        abrirModalSelecionarTemplate(Number(botao.dataset.templateColumn));
    });
}

async function abrirModalSelecionarTemplate(colunaId) {
    fecharPainelTemplates();
    colunaTemplatesAtual = colunaId;
    modalSelecionarTemplate?.show();
    await carregarTemplatesPainel();
    renderizarTemplatesModal();
}

async function abrirPainelTemplates(colunaId, botao) {
    colunaTemplatesAtual = colunaId;

    if (!painelTemplates) {
        painelTemplates = document.createElement("div");
        painelTemplates.id = "painelTemplates";
        painelTemplates.className = "dropdown-menu show shadow p-2";
        painelTemplates.style.position = "absolute";
        painelTemplates.style.zIndex = "2000";
        painelTemplates.style.minWidth = "20rem";
        painelTemplates.style.maxWidth = "24rem";
        document.body.appendChild(painelTemplates);
    }

    const rect = botao.getBoundingClientRect();
    painelTemplates.style.left = `${rect.left + window.scrollX}px`;
    painelTemplates.style.top = `${rect.bottom + window.scrollY + 4}px`;
    painelTemplates.classList.remove("d-none");
    painelTemplates.innerHTML = '<div class="text-muted small px-2 py-1">Carregando templates...</div>';

    await carregarTemplatesPainel();
}

async function carregarTemplatesPainel() {
    try {
        const response = await fetch(`?handler=Templates&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar os templates.");
            return;
        }

        templatesAtuais = data.templates || [];
        renderizarPainelTemplates();
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar os templates.");
        fecharPainelTemplates();
    }
}

function renderizarPainelTemplates() {
    if (!painelTemplates) return;

    const itens = templatesAtuais.length
        ? templatesAtuais.map(template => `
            <div class="dropdown-item-text border rounded-1 p-0 mb-2">
                <div style="height:4px;background:${normalizarCorTemplate(template.corCapa)};"></div>
                <div class="p-2">
                    <div class="fw-semibold mb-2">${escapeHtml(template.nome)}</div>
                    <div class="d-flex gap-1">
                        <button type="button" class="btn btn-sm btn-primary" data-usar-template="${template.id}">
                            <i class="bi bi-layout-text-sidebar me-1"></i>Template
                        </button>
                        <button type="button" class="btn btn-sm btn-outline-secondary" data-editar-template="${template.id}" aria-label="Editar template">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button type="button" class="btn btn-sm btn-outline-danger" data-excluir-template="${template.id}" aria-label="Excluir template">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
            </div>
        `).join("")
        : '<div class="text-muted small px-2 py-2">Nenhum template cadastrado.</div>';

    painelTemplates.innerHTML = `
        <div class="fw-semibold px-2 py-1">Templates</div>
        ${itens}
        <button type="button" class="dropdown-item text-primary" id="btnNovoTemplatePainel">
            <i class="bi bi-plus-lg me-1"></i>Criar um novo template
        </button>
    `;

    painelTemplates.querySelectorAll("[data-usar-template]").forEach(btn => {
        btn.addEventListener("click", () => usarTemplate(Number(btn.dataset.usarTemplate)));
    });
    painelTemplates.querySelectorAll("[data-editar-template]").forEach(btn => {
        btn.addEventListener("click", () => abrirModalTemplateEdicao(Number(btn.dataset.editarTemplate)));
    });
    painelTemplates.querySelectorAll("[data-excluir-template]").forEach(btn => {
        btn.addEventListener("click", () => excluirTemplate(Number(btn.dataset.excluirTemplate)));
    });
    painelTemplates.querySelector("#btnNovoTemplatePainel")?.addEventListener("click", abrirModalTemplateNovo);
}

function renderizarTemplatesModal() {
    const lista = document.getElementById("listaTemplatesModal");
    if (!lista) return;

    lista.innerHTML = templatesAtuais.length
        ? templatesAtuais.map(template => `
            <div class="col-12 col-md-6">
                <div class="border rounded-1 h-100 overflow-hidden">
                    <div style="height:8px;background:${normalizarCorTemplate(template.corCapa)};"></div>
                    <div class="p-3">
                        <div class="fw-semibold mb-3">${escapeHtml(template.nome)}</div>
                        <div class="d-flex gap-2">
                            <button type="button" class="btn btn-sm btn-primary" data-usar-template="${template.id}">
                                Usar
                            </button>
                            <button type="button" class="btn btn-sm btn-outline-secondary" data-editar-template="${template.id}" aria-label="Editar template">
                                <i class="bi bi-pencil"></i>
                            </button>
                            <button type="button" class="btn btn-sm btn-outline-danger" data-excluir-template="${template.id}" aria-label="Excluir template">
                                <i class="bi bi-trash"></i>
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `).join("")
        : '<div class="col-12 text-muted">Nenhum template criado ainda.</div>';

    lista.querySelectorAll("[data-usar-template]").forEach(btn => {
        btn.addEventListener("click", () => usarTemplate(Number(btn.dataset.usarTemplate)));
    });
    lista.querySelectorAll("[data-editar-template]").forEach(btn => {
        btn.addEventListener("click", () => abrirModalTemplateEdicao(Number(btn.dataset.editarTemplate)));
    });
    lista.querySelectorAll("[data-excluir-template]").forEach(btn => {
        btn.addEventListener("click", () => excluirTemplate(Number(btn.dataset.excluirTemplate)));
    });
}

function fecharPainelTemplates() {
    painelTemplates?.classList.add("d-none");
    colunaTemplatesAtual = null;
}

async function usarTemplate(templateId) {
    const colunaId = colunaTemplatesAtual;
    if (!templateId || !colunaId) return;

    try {
        const data = await fetchJson("?handler=CriarCartaoDeTemplate", {
            templateId,
            colunaId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível criar o cartão pelo template.");
            return;
        }

        inserirCardNoBoard(data.id, {
            colunaId: data.colunaId || colunaId,
            titulo: data.titulo || "Template",
            corCapa: data.corCapa,
            compartilharGrupo: !!data.compartilharGrupo,
            membrosVisiveis: data.membrosVisiveis || []
        });
        modalSelecionarTemplate?.hide();
        fecharPainelTemplates();
        mostrarToast("Cartão criado pelo template com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível criar o cartão pelo template.");
    }
}

function abrirModalTemplateNovo() {
    document.getElementById("formTemplateCartao").reset();
    setValue("templateId", "");
    setValue("templateCorCapa", "#0d6efd");
    document.getElementById("templateModalTitulo").textContent = "Novo template";
    modalSelecionarTemplate?.hide();
    modalTemplateCartao.show();
}

function abrirModalTemplateEdicao(templateId) {
    const template = templatesAtuais.find(item => Number(item.id) === templateId);
    if (!template) return;

    document.getElementById("formTemplateCartao").reset();
    setValue("templateId", template.id);
    setValue("templateNome", template.nome);
    setValue("templateDescricao", template.descricao);
    setValue("templatePrioridade", template.prioridade);
    setValue("templateCriticidade", template.criticidade);
    setValue("templateUrgencia", template.urgencia);
    setValue("templateCorCapa", template.corCapa || "#0d6efd");
    document.getElementById("templateModalTitulo").textContent = "Editar template";
    modalSelecionarTemplate?.hide();
    modalTemplateCartao.show();
}

async function salvarTemplate(event) {
    event.preventDefault();

    const templateId = toNullableInt(getValue("templateId"));
    const payload = {
        templateId: templateId || 0,
        grupoId: getGrupoId(),
        nome: getValue("templateNome"),
        descricao: getNullableString("templateDescricao"),
        prioridade: getNullableString("templatePrioridade"),
        criticidade: getNullableString("templateCriticidade"),
        urgencia: getNullableString("templateUrgencia"),
        corCapa: getNullableString("templateCorCapa")
    };

    try {
        const data = await fetchJson(templateId ? "?handler=EditarTemplate" : "?handler=CriarTemplate", payload);
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível salvar o template.");
            return;
        }

        modalTemplateCartao.hide();
        document.getElementById("formTemplateCartao").reset();
        await carregarTemplatesPainel();
        renderizarTemplatesModal();
        mostrarToast("Template salvo com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível salvar o template.");
    }
}

async function excluirTemplate(templateId) {
    if (!templateId || !confirm("Excluir este template?")) return;

    try {
        const data = await fetchJson("?handler=ExcluirTemplate", {
            templateId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível excluir o template.");
            return;
        }

        templatesAtuais = templatesAtuais.filter(item => Number(item.id) !== templateId);
        renderizarPainelTemplates();
        renderizarTemplatesModal();
        mostrarToast("Template excluído com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível excluir o template.");
    }
}

function mostrarSalvarComoTemplate() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartão antes de criar um template.");
        return;
    }

    document.getElementById("salvarTemplateInline")?.classList.remove("d-none");
    setValue("nomeTemplateCartao", getValue("cartaoTitulo"));
    setTimeout(() => document.getElementById("nomeTemplateCartao")?.focus(), 50);
}

function ocultarSalvarComoTemplate() {
    document.getElementById("salvarTemplateInline")?.classList.add("d-none");
    setValue("nomeTemplateCartao", "");
}

async function salvarComoTemplate() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    const nomeTemplate = getValue("nomeTemplateCartao").trim();
    if (!cartaoId || !nomeTemplate) return;

    try {
        const data = await fetchJson("?handler=SalvarComoTemplate", {
            cartaoId,
            grupoId: getGrupoId(),
            nomeTemplate
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível salvar o template.");
            return;
        }

        ocultarSalvarComoTemplate();
        mostrarToast("Template salvo com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível salvar o template.");
    }
}

function normalizarCorTemplate(cor) {
    return /^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?$/.test(cor || "") ? cor : "#64748b";
}

async function abrirModalMembrosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartão antes de editar os membros.");
        return;
    }

    const grupoTodo = document.getElementById("cartaoGrupoTodoModal");
    if (grupoTodo) {
        grupoTodo.checked = !!cartaoAtualEstado.compartilharGrupo;
    }

    try {
        const response = await fetch(`?handler=MembrosCartao&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar os membros.");
            return;
        }

        renderizarMembrosCartao(data.membros || []);
        atualizarMembrosModalGrupoTodo();
        modalMembrosCartao?.show();
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar os membros.");
    }
}

function atualizarMembrosModalGrupoTodo() {
    const grupoTodo = !!document.getElementById("cartaoGrupoTodoModal")?.checked;
    document.querySelectorAll("[data-membro-cartao]").forEach(input => {
        input.disabled = grupoTodo;
    });
}

function renderizarMembrosCartao(membros) {
    const lista = document.getElementById("cartaoMembrosLista");
    const select = document.getElementById("cartaoMembros");
    if (!lista || !select) return;

    select.innerHTML = "";
    lista.innerHTML = membros.length
        ? membros.map(membro => {
            const id = Number(membro.usuarioId);
            const selecionado = cartaoAtualEstado.membros.map(Number).includes(id);
            const nome = `${membro.nomeExibicao} (${membro.permissao})`;
            return `
                <label class="list-group-item d-flex gap-2 align-items-center">
                    <input class="form-check-input m-0" type="checkbox" value="${id}" data-membro-cartao ${selecionado ? "checked" : ""}>
                    <span>${escapeHtml(nome)}</span>
                </label>
            `;
        }).join("")
        : '<div class="list-group-item text-muted">Nenhum membro ativo encontrado.</div>';

    membros.forEach(membro => {
        const option = document.createElement("option");
        option.value = membro.usuarioId;
        option.textContent = `${membro.nomeExibicao} (${membro.permissao})`;
        option.selected = cartaoAtualEstado.membros.map(Number).includes(Number(membro.usuarioId));
        select.appendChild(option);
    });
}

async function salvarMembrosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return;

    const grupoTodo = !!document.getElementById("cartaoGrupoTodoModal")?.checked;
    const membrosIds = grupoTodo
        ? []
        : [...document.querySelectorAll("[data-membro-cartao]:checked")]
            .map(input => Number(input.value))
            .filter(Number.isFinite);

    try {
        const data = await fetchJson("?handler=SalvarMembrosCartao", {
            cartaoId,
            grupoId: getGrupoId(),
            membrosIds,
            grupoTodo
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível salvar os membros.");
            return;
        }

        cartaoAtualEstado.membros = membrosIds;
        cartaoAtualEstado.membrosVisiveis = data.membrosVisiveis || [];
        if (Array.isArray(data.chamadosVinculados)) {
            cartaoAtualEstado.chamadosVinculados = data.chamadosVinculados;
            cartaoAtualEstado.chamados = data.chamadosVinculados.map(chamado => Number(chamado.id)).filter(Number.isFinite);
            atualizarIndicadorChamadosCard(cartaoId, cartaoAtualEstado.chamadosVinculados);
            selecionarMultiplos("cartaoChamados", cartaoAtualEstado.chamados);
            atualizarResumoChamados(cartaoAtualEstado.chamadosVinculados);
        }
        cartaoAtualEstado.compartilharGrupo = grupoTodo;
        atualizarEstadoModalCartao();
        atualizarIndicadorAcessoCard(cartaoId, cartaoAtualEstado.membrosVisiveis, grupoTodo);
        document.getElementById("cartaoCompartilharGrupo").checked = grupoTodo;
        document.getElementById("cartaoPrivacidadeBadge").textContent = grupoTodo ? "Grupo" : "Privado";
        selecionarMultiplos("cartaoMembros", membrosIds);
        atualizarResumoMembros();
        modalMembrosCartao?.hide();
        mostrarToast("Membros atualizados com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível salvar os membros.");
    }
}

async function abrirModalChamadosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartão antes de vincular chamados.");
        return;
    }

    chamadosSelecionadosModal = new Set((cartaoAtualEstado.chamados || []).map(Number).filter(Number.isFinite));
    chamadosRemovidosModal = new Set();
    setValue("cartaoChamadosBusca", "");
    await carregarChamadosModal("");
    modalChamadosCartao?.show();
    return;
    /*

    try {
        const response = await fetch(`?handler=ChamadosVisivelParaTodos&cartaoId=${encodeURIComponent(cartaoId)}&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar os chamados.");
            return;
        }

        renderizarChamadosCartao(data.chamados || []);
        modalChamadosCartao?.show();
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar os chamados.");
    }
    */
}

function cancelarBuscaChamadosModal() {
    clearTimeout(buscaChamadosCartaoTimer);
    buscaChamadosCartaoController?.abort();
    buscaChamadosCartaoController = null;
}

async function carregarChamadosModal(termo = "") {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return;

    sincronizarSelecionadosChamadosModal();

    try {
        buscaChamadosCartaoController?.abort();
        buscaChamadosCartaoController = new AbortController();
        const query = new URLSearchParams({
            handler: "ChamadosVisivelParaTodos",
            cartaoId: String(cartaoId),
            grupoId: String(getGrupoId()),
            termo: String(termo || "")
        });
        const response = await fetch(`?${query.toString()}`, { signal: buscaChamadosCartaoController.signal });
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Nao foi possivel carregar os chamados.");
            return;
        }

        renderizarChamadosCartao(data.chamados || []);
    } catch (error) {
        if (error.name === "AbortError") return;
        mostrarToast(error.message || "Nao foi possivel carregar os chamados.");
    }
}

function sincronizarSelecionadosChamadosModal() {
    document.querySelectorAll("[data-chamado-cartao]").forEach(input => {
        const id = Number(input.value);
        if (!Number.isFinite(id)) return;

        if (input.checked) {
            chamadosSelecionadosModal.add(id);
            chamadosRemovidosModal.delete(id);
        } else {
            chamadosSelecionadosModal.delete(id);
            chamadosRemovidosModal.add(id);
        }
    });
}

function renderizarChamadosCartao(chamados) {
    const lista = document.getElementById("cartaoChamadosLista");
    const select = document.getElementById("cartaoChamados");
    if (!lista || !select) return;

    select.innerHTML = "";
    lista.innerHTML = chamados.length
        ? chamados.map(chamado => {
            const id = Number(chamado.id);
            const titulo = `#${chamado.numeroChamadoGrupo} - ${chamado.titulo}`;
            const selecionado = (chamadosSelecionadosModal.has(id) || !!chamado.vinculado) && !chamadosRemovidosModal.has(id);
            if (selecionado) {
                chamadosSelecionadosModal.add(id);
            } else {
                chamadosSelecionadosModal.delete(id);
            }
            return `
                <label class="list-group-item d-flex gap-2 align-items-center">
                    <input class="form-check-input m-0" type="checkbox" value="${id}" data-chamado-cartao ${selecionado ? "checked" : ""}>
                    <span>${escapeHtml(titulo)}</span>
                </label>
            `;
        }).join("")
        : '<div class="list-group-item text-muted">Nenhum chamado encontrado para esta busca.</div>';

    chamados.forEach(chamado => {
        const id = Number(chamado.id);
        const option = document.createElement("option");
        option.value = id;
        option.textContent = `#${chamado.numeroChamadoGrupo} - ${chamado.titulo}`;
        option.selected = chamadosSelecionadosModal.has(id);
        select.appendChild(option);
    });

    lista.querySelectorAll("[data-chamado-cartao]").forEach(input => {
        input.addEventListener("change", () => {
            const id = Number(input.value);
            if (!Number.isFinite(id)) return;

            if (input.checked) {
                chamadosSelecionadosModal.add(id);
                chamadosRemovidosModal.delete(id);
            } else {
                chamadosSelecionadosModal.delete(id);
                chamadosRemovidosModal.add(id);
            }
        });
    });
}

async function salvarChamadosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return;

    sincronizarSelecionadosChamadosModal();

    const chamadosIds = [...chamadosSelecionadosModal]
        .map(Number)
        .filter(Number.isFinite);

    try {
        const data = await fetchJson("?handler=SalvarChamadosCartao", {
            cartaoId,
            grupoId: getGrupoId(),
            chamadosIds
        });

        if (!data.success) {
            mostrarToast(data.message || "Não foi possível salvar os chamados.");
            return;
        }

        cartaoAtualEstado.chamados = chamadosIds;
        cartaoAtualEstado.chamadosVinculados = data.chamadosVinculados || [];
        atualizarEstadoModalCartao();
        selecionarMultiplos("cartaoChamados", chamadosIds);
        atualizarResumoChamados(cartaoAtualEstado.chamadosVinculados);
        atualizarIndicadorChamadosCard(cartaoId, cartaoAtualEstado.chamadosVinculados);
        modalChamadosCartao?.hide();
        mostrarToast("Chamados atualizados com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Não foi possível salvar os chamados.");
    }
}

async function carregarChamadosPermitidos() {
    const select = document.getElementById("cartaoChamados");
    if (!select) return;

    const selecionados = getSelectedNumbers("cartaoChamados");
    const query = new URLSearchParams({
        handler: "ChamadosPermitidos",
        grupoId: String(getGrupoId()),
        cartaoId: String(toNullableInt(getValue("cartaoId")) || 0),
        membrosIds: getSelectedNumbers("cartaoMembros").join(","),
        compartilharGrupo: String(!!document.getElementById("cartaoCompartilharGrupo")?.checked)
    });

    try {
        const response = await fetch(`?${query.toString()}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar os chamados.");
            return;
        }

        substituirOpcoesChamados(data.chamados || []);
        selecionarMultiplos("cartaoChamados", selecionados);
        atualizarResumoChamados();
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar os chamados.");
    }
}

function substituirOpcoesChamados(chamados) {
    const select = document.getElementById("cartaoChamados");
    if (!select) return;

    select.innerHTML = "";
    chamados.forEach(chamado => {
        const option = document.createElement("option");
        option.value = chamado.id;
        option.textContent = `#${chamado.numeroChamadoGrupo} - ${chamado.titulo}`;
        select.appendChild(option);
    });
}

function atualizarResumoMembros() {
    const resumo = document.getElementById("cartaoMembrosResumo");
    if (!resumo) return;

    if (cartaoAtualEstado.compartilharGrupo) {
        resumo.textContent = "Visivel para todo o grupo.";
        return;
    }

    const selecionados = [...document.getElementById("cartaoMembros")?.selectedOptions || []]
        .map(option => option.textContent.trim());

    resumo.textContent = selecionados.length
        ? selecionados.join(", ")
        : "Nenhum membro selecionado.";
}

function atualizarResumoChamados(chamadosVinculados) {
    const resumo = document.getElementById("cartaoChamadosResumo");
    if (!resumo) return;

    const selecionados = chamadosVinculados?.length
        ? chamadosVinculados.map(chamado => `#${chamado.numeroChamadoGrupo} - ${chamado.titulo}`)
        : [...document.getElementById("cartaoChamados")?.selectedOptions || []].map(option => option.textContent.trim());

    resumo.textContent = selecionados.length
        ? selecionados.join(", ")
        : "Nenhum chamado vinculado.";
}

function corEtiquetaSegura(cor) {
    return /^#[0-9a-fA-F]{6}$/.test(String(cor || "")) ? cor : "#6c757d";
}

function alternarPainelEtiquetasTarefa() {
    const painel = document.getElementById("painelEtiquetasTarefa");
    const botao = document.getElementById("btnAlternarEtiquetasTarefa");
    if (!painel || !botao) return;

    const abrir = painel.classList.contains("d-none");
    painel.classList.toggle("d-none", !abrir);
    botao.classList.toggle("is-open", abrir);
    botao.querySelector("i")?.classList.toggle("bi-plus-lg", !abrir);
    botao.querySelector("i")?.classList.toggle("bi-x-lg", abrir);
}

function renderizarEtiquetasTarefa(disponiveis, aplicadas) {
    cartaoAtualEstado.etiquetasDisponiveis = disponiveis || [];
    cartaoAtualEstado.etiquetasAplicadas = aplicadas || [];
    const podeGerenciar = cartaoAtualEstado.podeGerenciarEtiquetas !== false;
    const aplicadasIds = new Set(cartaoAtualEstado.etiquetasAplicadas.map(e => Number(e.id)));
    const resumo = document.getElementById("cartaoEtiquetasResumo");
    const lista = document.getElementById("listaEtiquetasTarefa");

    if (resumo) {
        resumo.innerHTML = cartaoAtualEstado.etiquetasAplicadas.length
            ? cartaoAtualEstado.etiquetasAplicadas.map(e => `<span class="task-label-chip" style="background:${corEtiquetaSegura(e.cor)}">${escapeHtml(e.nome)}</span>`).join("")
            : '<span class="text-muted small">Nenhuma</span>';
    }

    if (!lista) return;
    lista.innerHTML = cartaoAtualEstado.etiquetasDisponiveis.length
        ? cartaoAtualEstado.etiquetasDisponiveis.map(e => `
            <label class="task-check-row">
                <input type="checkbox" class="form-check-input" data-etiqueta-tarefa="${e.id}" ${aplicadasIds.has(Number(e.id)) ? "checked" : ""} ${podeGerenciar ? "" : "disabled"}>
                <span class="task-label-chip" style="background:${corEtiquetaSegura(e.cor)}">${escapeHtml(e.nome)}</span>
                <button type="button" class="btn btn-sm btn-link text-light ms-auto" data-editar-etiqueta="${e.id}" ${podeGerenciar ? "" : "disabled"}><i class="bi bi-pencil"></i></button>
                <button type="button" class="btn btn-sm btn-link text-danger" data-excluir-etiqueta="${e.id}" ${podeGerenciar ? "" : "disabled"}><i class="bi bi-trash"></i></button>
            </label>
        `).join("")
        : '<div class="text-muted small">Crie uma etiqueta para usar nesta tarefa.</div>';

    lista.querySelectorAll("[data-etiqueta-tarefa]").forEach(input => input.addEventListener("change", salvarEtiquetasCartao));
    lista.querySelectorAll("[data-editar-etiqueta]").forEach(btn => btn.addEventListener("click", editarEtiquetaTarefa));
    lista.querySelectorAll("[data-excluir-etiqueta]").forEach(btn => btn.addEventListener("click", excluirEtiquetaTarefa));
}

async function carregarEtiquetasTarefaUsuario() {
    try {
        const response = await fetch(`?handler=EtiquetasTarefaUsuario&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);
        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Nao foi possivel carregar as etiquetas.");
            return;
        }

        cartaoAtualEstado.etiquetasDisponiveis = data.dados || [];
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel carregar as etiquetas.");
    }
}

async function criarEtiquetaTarefa() {
    try {
        const data = await fetchJson("?handler=CriarEtiquetaTarefa", { grupoId: getGrupoId(), nome: getValue("novaEtiquetaNome"), cor: getValue("novaEtiquetaCor") || "#dc3545" });
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel criar a etiqueta.");
        cartaoAtualEstado.etiquetasDisponiveis.push(data.dados);
        setValue("novaEtiquetaNome", "");
        renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, cartaoAtualEstado.etiquetasAplicadas);
        mostrarToast("Etiqueta criada.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel criar a etiqueta.");
    }
}

async function editarEtiquetaTarefa(event) {
    const etiqueta = cartaoAtualEstado.etiquetasDisponiveis.find(e => Number(e.id) === Number(event.currentTarget.dataset.editarEtiqueta));
    if (!etiqueta) return;
    const nome = prompt("Nome da etiqueta", etiqueta.nome);
    if (!nome) return;
    const cor = prompt("Cor hexadecimal", etiqueta.cor);
    if (!cor) return;
    try {
        const data = await fetchJson("?handler=EditarEtiquetaTarefa", { grupoId: getGrupoId(), etiquetaId: etiqueta.id, nome, cor });
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel editar a etiqueta.");
        cartaoAtualEstado.etiquetasDisponiveis = cartaoAtualEstado.etiquetasDisponiveis.map(e => Number(e.id) === Number(etiqueta.id) ? data.dados : e);
        cartaoAtualEstado.etiquetasAplicadas = cartaoAtualEstado.etiquetasAplicadas.map(e => Number(e.id) === Number(etiqueta.id) ? data.dados : e);
        renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, cartaoAtualEstado.etiquetasAplicadas);
        atualizarEtiquetasCard(toNullableInt(getValue("cartaoId")), cartaoAtualEstado.etiquetasAplicadas);
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel editar a etiqueta.");
    }
}

async function excluirEtiquetaTarefa(event) {
    const etiquetaId = Number(event.currentTarget.dataset.excluirEtiqueta);
    if (!confirm("Excluir esta etiqueta? Ela sera removida das tarefas onde estiver aplicada.")) return;
    try {
        const data = await fetchJson("?handler=ExcluirEtiquetaTarefa", { grupoId: getGrupoId(), etiquetaId });
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel excluir a etiqueta.");
        cartaoAtualEstado.etiquetasDisponiveis = cartaoAtualEstado.etiquetasDisponiveis.filter(e => Number(e.id) !== etiquetaId);
        cartaoAtualEstado.etiquetasAplicadas = cartaoAtualEstado.etiquetasAplicadas.filter(e => Number(e.id) !== etiquetaId);
        renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, cartaoAtualEstado.etiquetasAplicadas);
        atualizarEtiquetasCard(toNullableInt(getValue("cartaoId")), cartaoAtualEstado.etiquetasAplicadas);
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel excluir a etiqueta.");
    }
}

async function salvarEtiquetasCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    const etiquetasIds = [...document.querySelectorAll("[data-etiqueta-tarefa]:checked")].map(input => Number(input.dataset.etiquetaTarefa));
    if (!cartaoId) {
        const selecionadas = cartaoAtualEstado.etiquetasDisponiveis.filter(e => etiquetasIds.includes(Number(e.id)));
        renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, selecionadas);
        return;
    }

    try {
        const data = await fetchJson("?handler=SalvarEtiquetasCartao", { grupoId: getGrupoId(), cartaoId, etiquetasIds });
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel salvar as etiquetas.");
        renderizarEtiquetasTarefa(cartaoAtualEstado.etiquetasDisponiveis, data.dados || []);
        atualizarEtiquetasCard(cartaoId, data.dados || []);
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel salvar as etiquetas.");
    }
}

function atualizarEtiquetasCard(cartaoId, etiquetas) {
    const card = document.querySelector(`.task-card[data-card-id="${cartaoId}"]`);
    if (!card) return;
    card.querySelector(".task-card-labels")?.remove();
    if (!etiquetas?.length) return;
    const labels = document.createElement("div");
    labels.className = "task-card-labels";
    labels.innerHTML = etiquetas.map(e => `<span class="task-label-chip" style="background:${corEtiquetaSegura(e.cor)}">${escapeHtml(e.nome)}</span>`).join("");
    card.querySelector(".task-card-main")?.after(labels);
}

function renderizarChecklistsTarefa(checklists) {
    cartaoAtualEstado.checklists = checklists || [];
    const progresso = calcularProgressoChecklists(cartaoAtualEstado.checklists);
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (cartaoId) {
        atualizarIndicadorChecklistCard(cartaoId, progresso.concluidos, progresso.total);
    }

    const container = document.getElementById("listaChecklistsTarefa");
    if (!container) return;
    container.innerHTML = cartaoAtualEstado.checklists.length ? cartaoAtualEstado.checklists.map(checklist => {
        const total = checklist.itens?.length || 0;
        const feitos = (checklist.itens || []).filter(i => i.concluido).length;
        return `<div class="task-checklist" data-checklist-id="${checklist.id}">
            <div class="task-checklist-title"><strong>${escapeHtml(checklist.titulo)}</strong><span class="small text-muted">${feitos}/${total}</span><button type="button" class="btn btn-sm btn-link text-light" data-editar-checklist="${checklist.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><i class="bi bi-pencil"></i></button><button type="button" class="btn btn-sm btn-link text-danger" data-excluir-checklist="${checklist.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><i class="bi bi-trash"></i></button></div>
            <div class="task-check-list">${(checklist.itens || []).map(item => `<label class="task-check-row"><input type="checkbox" class="form-check-input" data-item-checklist="${item.id}" ${item.concluido ? "checked" : ""} ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><span class="${item.concluido ? "text-decoration-line-through text-muted" : ""}">${escapeHtml(item.descricao)}</span><button type="button" class="btn btn-sm btn-link text-light ms-auto" data-editar-item-checklist="${item.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><i class="bi bi-pencil"></i></button><button type="button" class="btn btn-sm btn-link text-danger" data-excluir-item-checklist="${item.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><i class="bi bi-trash"></i></button></label>`).join("")}</div>
            <div class="input-group input-group-sm mt-2"><input type="text" class="form-control" maxlength="255" placeholder="Novo item" data-novo-item-checklist="${checklist.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><button type="button" class="btn btn-outline-primary" data-criar-item-checklist="${checklist.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"}><i class="bi bi-plus-lg"></i></button></div>
        </div>`;
    }).join("") : '<div class="text-muted small">Nenhum checklist criado.</div>';

    container.querySelectorAll("[data-editar-checklist]").forEach(btn => btn.addEventListener("click", editarChecklistTarefa));
    container.querySelectorAll("[data-excluir-checklist]").forEach(btn => btn.addEventListener("click", excluirChecklistTarefa));
    container.querySelectorAll("[data-criar-item-checklist]").forEach(btn => btn.addEventListener("click", criarItemChecklistTarefa));
    container.querySelectorAll("[data-item-checklist]").forEach(input => input.addEventListener("change", alternarItemChecklistTarefa));
    container.querySelectorAll("[data-editar-item-checklist]").forEach(btn => btn.addEventListener("click", editarItemChecklistTarefa));
    container.querySelectorAll("[data-excluir-item-checklist]").forEach(btn => btn.addEventListener("click", excluirItemChecklistTarefa));
}

async function criarChecklistTarefa() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return mostrarToast("Salve a tarefa antes de criar checklists.", "warning");
    await atualizarChecklists("?handler=CriarChecklistTarefa", { grupoId: getGrupoId(), cartaoId, titulo: getValue("novoChecklistTitulo") }, () => setValue("novoChecklistTitulo", ""));
}

async function editarChecklistTarefa(event) {
    const checklistId = Number(event.currentTarget.dataset.editarChecklist);
    const checklist = cartaoAtualEstado.checklists.find(c => Number(c.id) === checklistId);
    const titulo = prompt("Titulo do checklist", checklist?.titulo || "");
    if (titulo) await atualizarChecklists("?handler=EditarChecklistTarefa", { grupoId: getGrupoId(), checklistId, titulo });
}

async function excluirChecklistTarefa(event) {
    const checklistId = Number(event.currentTarget.dataset.excluirChecklist);
    if (confirm("Excluir este checklist?")) await atualizarChecklists("?handler=ExcluirChecklistTarefa", { grupoId: getGrupoId(), checklistId });
}

async function criarItemChecklistTarefa(event) {
    const checklistId = Number(event.currentTarget.dataset.criarItemChecklist);
    const input = document.querySelector(`[data-novo-item-checklist="${checklistId}"]`);
    await atualizarChecklists("?handler=CriarItemChecklistTarefa", { grupoId: getGrupoId(), checklistId, descricao: input?.value || "" }, () => { if (input) input.value = ""; });
}

async function alternarItemChecklistTarefa(event) {
    await atualizarChecklists("?handler=AlternarItemChecklistTarefa", { grupoId: getGrupoId(), itemId: Number(event.currentTarget.dataset.itemChecklist), concluido: !!event.currentTarget.checked });
}

async function editarItemChecklistTarefa(event) {
    const itemId = Number(event.currentTarget.dataset.editarItemChecklist);
    const item = cartaoAtualEstado.checklists.flatMap(c => c.itens || []).find(i => Number(i.id) === itemId);
    const descricao = prompt("Descricao do item", item?.descricao || "");
    if (descricao) await atualizarChecklists("?handler=EditarItemChecklistTarefa", { grupoId: getGrupoId(), itemId, descricao });
}

async function excluirItemChecklistTarefa(event) {
    const itemId = Number(event.currentTarget.dataset.excluirItemChecklist);
    if (confirm("Excluir este item?")) await atualizarChecklists("?handler=ExcluirItemChecklistTarefa", { grupoId: getGrupoId(), itemId });
}

async function atualizarChecklists(handler, payload, aoSucesso) {
    try {
        const data = await fetchJson(handler, payload);
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel atualizar o checklist.");
        aoSucesso?.();
        renderizarChecklistsTarefa(data.dados || []);
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel atualizar o checklist.");
    }
}

function renderizarAnexosTarefa(anexos) {
    cartaoAtualEstado.anexos = anexos || [];
    const container = document.getElementById("listaAnexosTarefa");
    if (!container) return;
    container.innerHTML = cartaoAtualEstado.anexos.length ? cartaoAtualEstado.anexos.map(anexo => {
        const tipo = anexo.tipoVisualizacao || window.CallStationAnexos?.tipoPorExtensao(anexo.extensao) || "download";
        const podeVisualizar = Boolean(anexo.podeVisualizar);
        const icone = window.CallStationAnexos?.iconePorExtensao(anexo.extensao) || "bi-paperclip";
        const urlPreview = `?handler=VisualizarAnexoTarefa&anexoId=${encodeURIComponent(anexo.id)}&grupoId=${encodeURIComponent(getGrupoId())}`;
        const urlDownload = `?handler=BaixarAnexoTarefa&anexoId=${encodeURIComponent(anexo.id)}&grupoId=${encodeURIComponent(getGrupoId())}`;
        const botaoPreview = podeVisualizar
            ? tipo === "pdf"
                ? `<a class="btn btn-sm btn-outline-light" href="${urlPreview}" target="_blank" rel="noopener" title="Visualizar"><i class="bi bi-eye"></i></a>`
                : `<button type="button" class="btn btn-sm btn-outline-light" data-preview-anexo="${anexo.id}" title="Visualizar"><i class="bi bi-eye"></i></button>`
            : "";

        return `<div class="task-attachment-row"><div class="min-w-0"><div class="fw-semibold text-truncate"><i class="bi ${escapeHtml(icone)} me-1"></i>${escapeHtml(anexo.nomeOriginal)}</div><div class="small text-muted">${escapeHtml(formatarTamanhoArquivo(anexo.tamanhoBytes))} - ${escapeHtml(anexo.usuario || "")} - ${escapeHtml(formatActivityDateTime(anexo.dataUpload))}</div></div><div class="d-flex gap-1">${botaoPreview}<a class="btn btn-sm btn-outline-light" href="${urlDownload}" title="Baixar"><i class="bi bi-download"></i></a><button type="button" class="btn btn-sm btn-outline-danger" data-excluir-anexo="${anexo.id}" ${cartaoAtualEstado.podeEditar ? "" : "disabled"} title="Excluir"><i class="bi bi-trash"></i></button></div></div>`;
    }).join("") : '<div class="text-muted small">Nenhum anexo enviado.</div>';
    container.querySelectorAll("[data-preview-anexo]").forEach(btn => btn.addEventListener("click", visualizarAnexoTarefa));
    container.querySelectorAll("[data-excluir-anexo]").forEach(btn => btn.addEventListener("click", excluirAnexoTarefa));
}

async function enviarAnexoTarefa() {
    if (uploadAnexoTarefaController) {
        cancelarUploadAnexoTarefa();
        return;
    }

    const cartaoId = toNullableInt(getValue("cartaoId"));
    const input = document.getElementById("arquivoAnexoTarefa");
    const botao = document.getElementById("btnEnviarAnexoTarefa");
    if (!cartaoId || !input?.files?.length) return;
    const arquivo = input.files[0];
    const validacao = window.CallStationAnexos?.validarArquivo(arquivo);
    if (validacao && !validacao.ok) {
        mostrarToast(validacao.mensagem);
        return;
    }

    const arquivoEnvio = window.CallStationAnexos
        ? await window.CallStationAnexos.prepararParaEnvio(arquivo)
        : { arquivo, compactado: false };

    const formData = new FormData();
    formData.append("grupoId", getGrupoId());
    formData.append("cartaoId", cartaoId);
    formData.append("arquivo", arquivoEnvio.arquivo);
    formData.append("arquivoCompactado", arquivoEnvio.compactado ? "true" : "false");
    uploadAnexoTarefaController = new AbortController();
    const htmlOriginal = botao?.innerHTML;
    const tituloOriginal = botao?.title || "";
    if (botao) {
        botao.innerHTML = '<i class="bi bi-x-lg"></i>';
        botao.title = "Cancelar envio";
    }

    try {
        const response = await fetch("?handler=EnviarAnexoTarefa", { method: "POST", body: formData, signal: uploadAnexoTarefaController.signal, headers: { "RequestVerificationToken": getToken() } });
        const data = await response.json();
        if (!response.ok || !data.success) return mostrarToast(data.message || "Nao foi possivel enviar o anexo.");
        input.value = "";
        renderizarAnexosTarefa(data.dados || []);
        mostrarToast("Anexo enviado.", "success");
    } catch (error) {
        if (error.name === "AbortError") {
            mostrarToast("Envio cancelado.", "warning");
            return;
        }

        mostrarToast(error.message || "Nao foi possivel enviar o anexo.");
    } finally {
        uploadAnexoTarefaController = null;
        if (botao) {
            botao.innerHTML = htmlOriginal || '<i class="bi bi-upload"></i>';
            botao.title = tituloOriginal;
        }
    }
}

function cancelarUploadAnexoTarefa() {
    uploadAnexoTarefaController?.abort();
}

async function visualizarAnexoTarefa(event) {
    limparPreviewAnexoTarefa();

    const anexoId = Number(event.currentTarget.dataset.previewAnexo);
    const anexo = cartaoAtualEstado.anexos.find(a => Number(a.id) === anexoId);
    const tipo = anexo?.tipoVisualizacao || window.CallStationAnexos?.tipoPorExtensao(anexo?.extensao) || "download";
    const url = `?handler=VisualizarAnexoTarefa&anexoId=${encodeURIComponent(anexoId)}&grupoId=${encodeURIComponent(getGrupoId())}`;
    const titulo = document.getElementById("modalVisualizarAnexoTarefaTitulo");
    const conteudo = document.getElementById("conteudoPreviewAnexoTarefa");
    if (!conteudo) return;

    if (titulo) titulo.textContent = anexo?.nomeOriginal || "Anexo";
    conteudo.innerHTML = '<div class="text-muted small">Carregando anexo...</div>';

    if (tipo === "imagem") {
        conteudo.innerHTML = `<img class="img-fluid rounded" src="${url}" alt="${escapeHtml(anexo?.nomeOriginal || "Anexo")}">`;
    } else if (tipo === "video") {
        conteudo.innerHTML = `<video class="task-modal-video-preview" controls preload="metadata" src="${url}"></video>`;
    } else if (tipo === "texto") {
        previewAnexoTarefaController = new AbortController();
        try {
            const response = await fetch(url, {
                credentials: "same-origin",
                signal: previewAnexoTarefaController.signal
            });
            if (!response.ok) throw new Error("Nao foi possivel carregar o texto.");
            const texto = await response.text();
            conteudo.innerHTML = `<pre class="task-modal-text-preview">${escapeHtml(texto)}</pre>`;
        } catch (error) {
            if (error.name === "AbortError") return;
            conteudo.innerHTML = `<div class="text-danger small">${escapeHtml(error.message || "Nao foi possivel carregar o texto.")}</div>`;
        } finally {
            previewAnexoTarefaController = null;
        }
    } else {
        window.open(url, "_blank", "noopener");
        return;
    }

    modalVisualizarAnexoTarefa?.show();
}

function limparPreviewAnexoTarefa() {
    previewAnexoTarefaController?.abort();
    previewAnexoTarefaController = null;

    const conteudo = document.getElementById("conteudoPreviewAnexoTarefa");
    if (!conteudo) return;

    conteudo.querySelectorAll("video, audio").forEach(media => {
        media.pause();
        media.removeAttribute("src");
        media.load();
    });

    conteudo.querySelectorAll("img").forEach(imagem => {
        imagem.removeAttribute("src");
    });

    conteudo.innerHTML = '<div class="text-muted small">Carregando anexo...</div>';
}

async function excluirAnexoTarefa(event) {
    const anexoId = Number(event.currentTarget.dataset.excluirAnexo);
    if (!confirm("Excluir este anexo?")) return;
    try {
        const data = await fetchJson("?handler=ExcluirAnexoTarefa", { grupoId: getGrupoId(), anexoId });
        if (!data.success) return mostrarToast(data.message || "Nao foi possivel excluir o anexo.");
        renderizarAnexosTarefa(data.dados || []);
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel excluir o anexo.");
    }
}

function formatarTamanhoArquivo(bytes) {
    const valor = Number(bytes || 0);
    if (valor < 1024) return `${valor} B`;
    if (valor < 1024 * 1024) return `${(valor / 1024).toFixed(1)} KB`;
    return `${(valor / 1024 / 1024).toFixed(1)} MB`;
}

function atualizarContadorLista(lista) {
    const contador = lista?.querySelector(".task-list-count");
    const total = lista?.querySelectorAll(".task-card").length ?? 0;
    if (contador) {
        contador.textContent = String(total);
    }
}

function sincronizarSelectColunasComBoard() {
    const selectColunas = document.getElementById("cartaoColunaSelect");
    if (!selectColunas) return;

    const opcoesPorId = new Map([...selectColunas.options].map(option => [option.value, option]));
    capturarSnapshotListas().forEach(colunaId => {
        const option = opcoesPorId.get(String(colunaId));
        if (option) {
            selectColunas.appendChild(option);
        }
    });
}

async function adicionarComentario() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    const textoInput = document.getElementById("comentarioTexto");
    const mensagemVisivel = (textoInput?.value || "").trim();
    const mensagem = serializarTextoCampo(textoInput).trim();
    if (!cartaoId || !mensagemVisivel) return;
    if (mensagemVisivel.length > 250) {
        mostrarToast("O comentário não pode exceder 250 caracteres.");
        return;
    }

    let data;
    try {
        data = await fetchJson("?handler=AdicionarComentario", {
            grupoId: getGrupoId(),
            cartaoId,
            mensagem
        });
    } catch (error) {
        mostrarToast(error.message || "Não foi possível adicionar o comentário.");
        return;
    }

    if (!data.success) {
        mostrarToast(data.message || "Não foi possível adicionar o comentário.");
        return;
    }

    setValue("comentarioTexto", "");
    await abrirModalCartaoExistente(cartaoId);
}

async function fetchJson(url, payload) {
    const response = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": getToken()
        },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        let message = `Erro HTTP: ${response.status}`;
        try {
            const data = await response.json();
            message = data.message || message;
        } catch {
        }
        throw new Error(message);
    }

    return await response.json();
}

function configurarEdicao(podeEditar) {
    document.querySelectorAll("#formCartao input, #formCartao textarea, #formCartao select").forEach(el => {
        if (el.id === "comentarioTexto") return;
        if (el.closest("#secaoEtiquetasTarefa")) return;
        el.disabled = !podeEditar;
    });

    document.querySelectorAll("#cartaoMembros, #cartaoChamados").forEach(el => {
        el.disabled = !podeEditar;
    });

    document.getElementById("btnFocoMembros").disabled = !podeEditar;
    document.getElementById("btnFocoChamados").disabled = !podeEditar;
    document.getElementById("btnMostrarSalvarTemplate").disabled = !podeEditar;
    document.getElementById("btnConfirmarSalvarTemplate").disabled = !podeEditar;
    document.querySelector('#formCartao button[type="submit"]').disabled = !podeEditar;

    const btnArquivar = document.getElementById("btnArquivarCartao");
    if (btnArquivar) {
        const cartaoId = toNullableInt(getValue("cartaoId"));
        btnArquivar.classList.toggle("d-none", !podeEditar || !cartaoId);
        btnArquivar.textContent = cartaoAtualEstado.arquivado ? "Restaurar" : "Arquivar";
        btnArquivar.classList.toggle("btn-outline-danger", !cartaoAtualEstado.arquivado);
        btnArquivar.classList.toggle("btn-outline-success", cartaoAtualEstado.arquivado);
    }

    const btnSair = document.getElementById("btnSairCartao");
    if (btnSair) {
        const cartaoId = toNullableInt(getValue("cartaoId"));
        btnSair.classList.toggle("d-none", !cartaoId || !cartaoAtualEstado.podeSairVinculo);
    }
}

function renderizarAtividade(itens) {
    const container = document.getElementById("cartaoAtividade");
    if (!container) return;

    if (!itens.length) {
        container.innerHTML = '<div class="text-muted small">Sem atividade registrada.</div>';
        return;
    }

    container.innerHTML = itens.map(item => {
        const inicial = (item.usuario || "U").trim().charAt(0).toUpperCase();
        const data = formatActivityDateTime(item.data);
        return `
            <div class="activity-item">
                <div class="activity-avatar">${escapeHtml(inicial)}</div>
                <div>
                    <div><strong>${escapeHtml(item.usuario || "Usuário")}</strong> ${renderizarTextoSeguro(item.texto || "")}</div>
                    <div class="text-muted small">${escapeHtml(data)}</div>
                </div>
            </div>
        `;
    }).join("");
}

function formatActivityDateTime(value) {
    if (!value) return "";

    const texto = String(value).trim();
    const localMatch = texto.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
    const temFusoExplicito = /[zZ]|[+-]\d{2}:\d{2}$/.test(texto);

    if (localMatch && !temFusoExplicito) {
        const [, ano, mes, dia, hora, minuto] = localMatch;
        return `${dia}/${mes}/${ano} ${hora}:${minuto}`;
    }

    const data = new Date(texto);
    if (Number.isNaN(data.getTime())) return "";

    return data.toLocaleString("pt-BR", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
    });
}

function selecionarMultiplos(id, values) {
    const set = new Set((values || []).map(String));
    document.querySelectorAll(`#${id} option`).forEach(option => {
        option.selected = set.has(option.value);
    });
}

function getSelectedNumbers(id) {
    return [...document.getElementById(id)?.selectedOptions || []]
        .map(option => Number(option.value))
        .filter(Number.isFinite);
}

function formatDateTimeLocal(value) {
    if (!value) return "";
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return "";
    const tzoffset = dt.getTimezoneOffset() * 60000;
    return new Date(dt.getTime() - tzoffset).toISOString().slice(0, 16);
}

function focarSecao(id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "center" });
}

function atualizarEstadoModalCartao() {
    const modal = document.getElementById("modalCartao");
    if (!modal) return;

    modal.dataset.membros = (cartaoAtualEstado.membros || []).join(",");
    modal.dataset.chamados = (cartaoAtualEstado.chamados || []).join(",");
    modal.dataset.compartilharGrupo = String(!!cartaoAtualEstado.compartilharGrupo);
    modal.dataset.arquivado = String(!!cartaoAtualEstado.arquivado);
    modal.dataset.podeSairVinculo = String(!!cartaoAtualEstado.podeSairVinculo);
}

function getBoard() {
    return document.getElementById("board");
}

function getGrupoId() {
    return Number(getBoard()?.dataset.grupoId || 0);
}

function getToken() {
    return document.getElementById("requestVerificationToken")?.value || "";
}

function getValue(id) {
    return document.getElementById(id)?.value || "";
}

function setValue(id, value) {
    const el = document.getElementById(id);
    if (el) el.value = value ?? "";
}

function setValueComMencoes(id, value) {
    const el = document.getElementById(id);
    if (!el) return;

    if (window.aplicarTextoComMencoesCampo) {
        window.aplicarTextoComMencoesCampo(el, value ?? "");
    } else {
        el.value = value ?? "";
    }
}

function getNullableString(id) {
    const value = getValue(id).trim();
    return value === "" ? null : value;
}

function getNullableStringSerializado(id) {
    const campo = document.getElementById(id);
    const value = serializarTextoCampo(campo).trim();
    return value === "" ? null : value;
}

function toNullableInt(value) {
    if (!value) return null;
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function renderizarTextoSeguro(value) {
    return escapeHtml(value)
        .replace(/@\[todos\]\(todos\)/gi, '<span class="mention-token mention-token-all">@todos</span>')
        .replace(/@\[([^\]\r\n]{1,100})\]\(usuario:(\d{1,10})\)/g, '<span class="mention-token">@$1</span>');
}

function serializarTextoCampo(campo) {
    return window.serializarTextoComMencoes
        ? window.serializarTextoComMencoes(campo)
        : String(campo?.value || "");
}
