let draggedCard = null;
let modalLista = null;
let modalCartao = null;
let modalMembrosCartao = null;
let modalChamadosCartao = null;
let modalTemplateCartao = null;
let painelTemplates = null;
let colunaTemplatesAtual = null;
let templatesAtuais = [];
let snapshotBoardAntesDrag = null;
let cartaoAtualEstado = {
    membros: [],
    chamados: [],
    compartilharGrupo: false
};

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
    modalLista = new bootstrap.Modal(document.getElementById("modalLista"));
    modalCartao = new bootstrap.Modal(document.getElementById("modalCartao"));
    modalMembrosCartao = new bootstrap.Modal(document.getElementById("modalMembrosCartao"));
    modalChamadosCartao = new bootstrap.Modal(document.getElementById("modalChamadosCartao"));
    modalTemplateCartao = new bootstrap.Modal(document.getElementById("modalTemplateCartao"));

    document.getElementById("btnNovaLista")?.addEventListener("click", abrirModalLista);
    document.getElementById("btnAdicionarListaInline")?.addEventListener("click", abrirModalLista);
    document.getElementById("formLista")?.addEventListener("submit", criarLista);
    document.getElementById("formCartao")?.addEventListener("submit", salvarCartao);
    document.getElementById("formTemplateCartao")?.addEventListener("submit", salvarTemplate);
    document.getElementById("btnAdicionarComentario")?.addEventListener("click", adicionarComentario);
    document.getElementById("btnMostrarSalvarTemplate")?.addEventListener("click", mostrarSalvarComoTemplate);
    document.getElementById("btnConfirmarSalvarTemplate")?.addEventListener("click", salvarComoTemplate);
    document.getElementById("cartaoCorCapa")?.addEventListener("input", atualizarCorCapaModal);
    document.getElementById("cartaoCorCapa")?.addEventListener("change", atualizarCorCapaModal);

    document.getElementById("btnFocoDatas")?.addEventListener("click", () => focarSecao("secaoDatas"));
    document.getElementById("btnFocoMembros")?.addEventListener("click", abrirModalMembrosCartao);
    document.getElementById("btnFocoChamados")?.addEventListener("click", abrirModalChamadosCartao);
    document.getElementById("btnSalvarMembrosCartao")?.addEventListener("click", salvarMembrosCartao);
    document.getElementById("btnSalvarChamadosCartao")?.addEventListener("click", salvarChamadosCartao);
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

    inicializarBoard();
});

function inicializarBoard() {
    document.querySelectorAll(".task-card").forEach(inicializarCard);
    document.querySelectorAll(".task-cards").forEach(inicializarDropZone);
}

function inicializarCard(card) {
    card.addEventListener("click", () => abrirModalCartaoExistente(Number(card.dataset.cardId)));
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

function abrirModalCartaoNovo(colunaId) {
    document.getElementById("formCartao").reset();
    setValue("cartaoId", "");
    setValue("cartaoColunaId", colunaId);
    setValue("cartaoColunaSelect", colunaId);
    setValue("cartaoCorCapa", "#0d6efd");
    atualizarCorCapaModal();
    document.getElementById("cartaoCompartilharGrupo").checked = false;
    document.getElementById("cartaoPrivacidadeBadge").textContent = "Privado";
    document.getElementById("cartaoAtividade").innerHTML = '<div class="text-muted small">Sem atividade carregada.</div>';
    selecionarMultiplos("cartaoMembros", []);
    substituirOpcoesChamados([]);
    cartaoAtualEstado = {
        membros: [],
        chamados: [],
        compartilharGrupo: false
    };
    atualizarEstadoModalCartao();
    atualizarResumoMembros();
    atualizarResumoChamados();
    ocultarSalvarComoTemplate();
    modalCartao.show();
    setTimeout(() => document.getElementById("cartaoTitulo")?.focus(), 150);
}

async function abrirModalCartaoExistente(id) {
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
    setValue("cartaoDescricao", data.descricao);
    setValue("cartaoPrioridade", data.prioridade);
    setValue("cartaoCriticidade", data.criticidade);
    setValue("cartaoUrgencia", data.urgencia);
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
        chamados: data.chamados || [],
        compartilharGrupo: !!data.compartilharGrupo
    };
    atualizarEstadoModalCartao();
    atualizarResumoMembros();
    atualizarResumoChamados(data.chamadosVinculados || []);
    renderizarAtividade(data.atividade || []);
    ocultarSalvarComoTemplate();

    configurarEdicao(!!data.podeEditar);
    modalCartao.show();
}

async function salvarCartao(event) {
    event.preventDefault();

    const id = toNullableInt(getValue("cartaoId"));
    const payload = {
        id,
        grupoId: getGrupoId(),
        colunaId: Number(getValue("cartaoColunaSelect") || getValue("cartaoColunaId")),
        titulo: getValue("cartaoTitulo"),
        descricao: getNullableString("cartaoDescricao"),
        prioridade: getNullableString("cartaoPrioridade"),
        criticidade: getNullableString("cartaoCriticidade"),
        urgencia: getNullableString("cartaoUrgencia"),
        dataInicio: getNullableString("cartaoDataInicio"),
        dataVencimento: getNullableString("cartaoDataVencimento"),
        corCapa: getNullableString("cartaoCorCapa"),
        compartilharGrupo: !!document.getElementById("cartaoCompartilharGrupo")?.checked,
        membrosIds: getSelectedNumbers("cartaoMembros"),
        chamadosIds: getSelectedNumbers("cartaoChamados")
    };

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

async function salvarOrdemBoard() {
    const colunas = capturarSnapshotBoard();

    try {
        await fetchJson("?handler=ReordenarCartoes", {
            grupoId: getGrupoId(),
            colunas
        });
    } catch (error) {
        console.error(error);
        mostrarToast("Não foi possível salvar a nova ordem dos cartões.");
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

function inserirListaNoBoard(id, nome) {
    const board = getBoard();
    const addListPanel = board?.querySelector(".add-list-panel");
    if (!board || !addListPanel) return;

    const lista = document.createElement("section");
    lista.className = "task-list";
    lista.dataset.columnId = id;
    lista.innerHTML = `
        <div class="task-list-header">
            <h2>${escapeHtml(nome)}</h2>
            <span>0</span>
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

    const cardsContainer = lista.querySelector(".task-cards");
    if (cardsContainer) {
        inicializarDropZone(cardsContainer);
    }

    lista.querySelector("[data-add-card-column]")?.addEventListener("click", () =>
        abrirModalCartaoNovo(Number(id)));
    lista.querySelector("[data-template-column]")?.addEventListener("click", event =>
        abrirPainelTemplates(Number(id), event.currentTarget));

    const selectColunas = document.getElementById("cartaoColunaSelect");
    if (selectColunas) {
        const option = document.createElement("option");
        option.value = id;
        option.textContent = nome;
        selectColunas.appendChild(option);
    }
}

function inserirCardNoBoard(id, payload) {
    const colunaCards = document.querySelector(`.task-cards[data-column-cards="${payload.colunaId}"]`);
    if (!colunaCards) return;

    const card = document.createElement("article");
    card.className = "task-card";
    card.draggable = true;
    card.dataset.cardId = id;
    card.dataset.concluido = "false";
    card.innerHTML = `
        <div class="task-card-main">
            <button type="button" class="task-done-toggle" data-toggle-done="${id}" aria-label="Alternar conclusão">
                <i class="bi"></i>
            </button>
            <div class="task-title">${escapeHtml(payload.titulo)}</div>
        </div>
        <div class="task-meta">
            <span><i class="bi bi-lock"></i> ${payload.compartilharGrupo ? "Grupo" : "Privado"}</span>
        </div>
    `;

    colunaCards.appendChild(card);
    inicializarCard(card);
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
        abrirPainelTemplates(Number(botao.dataset.templateColumn), botao);
    });
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
            mostrarToast(data.message || "Nao foi possivel carregar os templates.");
            return;
        }

        templatesAtuais = data.templates || [];
        renderizarPainelTemplates();
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel carregar os templates.");
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

function fecharPainelTemplates() {
    painelTemplates?.classList.add("d-none");
    colunaTemplatesAtual = null;
}

async function usarTemplate(templateId) {
    const colunaId = colunaTemplatesAtual;
    if (!templateId || !colunaId) return;

    const template = templatesAtuais.find(item => Number(item.id) === templateId);

    try {
        const data = await fetchJson("?handler=CriarCartaoDeTemplate", {
            templateId,
            colunaId,
            grupoId: getGrupoId()
        });

        if (!data.success) {
            mostrarToast(data.message || "Nao foi possivel criar o cartao pelo template.");
            return;
        }

        inserirCardNoBoard(data.id, {
            colunaId,
            titulo: template?.nome || "Template",
            compartilharGrupo: false
        });
        fecharPainelTemplates();
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel criar o cartao pelo template.");
    }
}

function abrirModalTemplateNovo() {
    document.getElementById("formTemplateCartao").reset();
    setValue("templateId", "");
    setValue("templateCorCapa", "#0d6efd");
    document.getElementById("templateModalTitulo").textContent = "Novo template";
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
            mostrarToast(data.message || "Nao foi possivel salvar o template.");
            return;
        }

        modalTemplateCartao.hide();
        document.getElementById("formTemplateCartao").reset();
        await carregarTemplatesPainel();
        mostrarToast("Template salvo com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel salvar o template.");
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
            mostrarToast(data.message || "Nao foi possivel excluir o template.");
            return;
        }

        templatesAtuais = templatesAtuais.filter(item => Number(item.id) !== templateId);
        renderizarPainelTemplates();
        mostrarToast("Template excluido com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel excluir o template.");
    }
}

function mostrarSalvarComoTemplate() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartao antes de criar um template.");
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
            mostrarToast(data.message || "Nao foi possivel salvar o template.");
            return;
        }

        ocultarSalvarComoTemplate();
        mostrarToast("Template salvo com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel salvar o template.");
    }
}

function normalizarCorTemplate(cor) {
    return /^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?$/.test(cor || "") ? cor : "#64748b";
}

async function abrirModalMembrosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartao antes de editar os membros.");
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
            mostrarToast(data.message || "Nao foi possivel carregar os membros.");
            return;
        }

        renderizarMembrosCartao(data.membros || []);
        atualizarMembrosModalGrupoTodo();
        modalMembrosCartao?.show();
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel carregar os membros.");
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
            mostrarToast(data.message || "Nao foi possivel salvar os membros.");
            return;
        }

        cartaoAtualEstado.membros = membrosIds;
        cartaoAtualEstado.compartilharGrupo = grupoTodo;
        atualizarEstadoModalCartao();
        document.getElementById("cartaoCompartilharGrupo").checked = grupoTodo;
        document.getElementById("cartaoPrivacidadeBadge").textContent = grupoTodo ? "Grupo" : "Privado";
        selecionarMultiplos("cartaoMembros", membrosIds);
        atualizarResumoMembros();
        modalMembrosCartao?.hide();
        mostrarToast("Membros atualizados com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel salvar os membros.");
    }
}

async function abrirModalChamadosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) {
        mostrarToast("Salve o cartao antes de vincular chamados.");
        return;
    }

    try {
        const response = await fetch(`?handler=ChamadosVisivelParaTodos&cartaoId=${encodeURIComponent(cartaoId)}&grupoId=${encodeURIComponent(getGrupoId())}`);
        if (!response.ok) throw new Error(`Erro HTTP: ${response.status}`);

        const data = await response.json();
        if (!data.success) {
            mostrarToast(data.message || "Nao foi possivel carregar os chamados.");
            return;
        }

        renderizarChamadosCartao(data.chamados || []);
        modalChamadosCartao?.show();
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel carregar os chamados.");
    }
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
            return `
                <label class="list-group-item d-flex gap-2 align-items-center">
                    <input class="form-check-input m-0" type="checkbox" value="${id}" data-chamado-cartao ${chamado.vinculado ? "checked" : ""}>
                    <span>${escapeHtml(titulo)}</span>
                </label>
            `;
        }).join("")
        : '<div class="list-group-item text-muted">Nenhum chamado visivel para todos os membros.</div>';

    chamados.forEach(chamado => {
        const option = document.createElement("option");
        option.value = chamado.id;
        option.textContent = `#${chamado.numeroChamadoGrupo} - ${chamado.titulo}`;
        option.selected = !!chamado.vinculado;
        select.appendChild(option);
    });
}

async function salvarChamadosCartao() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    if (!cartaoId) return;

    const chamadosIds = [...document.querySelectorAll("[data-chamado-cartao]:checked")]
        .map(input => Number(input.value))
        .filter(Number.isFinite);

    try {
        const data = await fetchJson("?handler=SalvarChamadosCartao", {
            cartaoId,
            grupoId: getGrupoId(),
            chamadosIds
        });

        if (!data.success) {
            mostrarToast(data.message || "Nao foi possivel salvar os chamados.");
            return;
        }

        cartaoAtualEstado.chamados = chamadosIds;
        atualizarEstadoModalCartao();
        selecionarMultiplos("cartaoChamados", chamadosIds);
        atualizarResumoChamados();
        modalChamadosCartao?.hide();
        mostrarToast("Chamados atualizados com sucesso.", "success");
    } catch (error) {
        mostrarToast(error.message || "Nao foi possivel salvar os chamados.");
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

function atualizarContadorLista(lista) {
    const contador = lista?.querySelector(".task-list-header span");
    const total = lista?.querySelectorAll(".task-card").length ?? 0;
    if (contador) {
        contador.textContent = String(total);
    }
}

async function adicionarComentario() {
    const cartaoId = toNullableInt(getValue("cartaoId"));
    const mensagem = getValue("comentarioTexto").trim();
    if (!cartaoId || !mensagem) return;

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
        const data = item.data ? new Date(item.data).toLocaleString("pt-BR") : "";
        return `
            <div class="activity-item">
                <div class="activity-avatar">${escapeHtml(inicial)}</div>
                <div>
                    <div><strong>${escapeHtml(item.usuario || "Usuário")}</strong> ${escapeHtml(item.texto || "")}</div>
                    <div class="text-muted small">${escapeHtml(data)}</div>
                </div>
            </div>
        `;
    }).join("");
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

function getNullableString(id) {
    const value = getValue(id).trim();
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

