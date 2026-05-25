(function () {
    const seletorCampos = [
        "#editDescricao",
        "#editSolucao",
        "#textoNovoComentarioChamado",
        "#cartaoDescricao",
        "#comentarioTexto",
        "#textoNovoComentarioHistorico",
        "#editHistoricoDescricao",
        "#editHistoricoSolucao",
        "[data-texto-edicao-comentario]"
    ].join(",");

    let menu = null;
    let campoAtivo = null;
    let estado = null;
    let indiceAtivo = 0;
    let debounceId = null;

    document.addEventListener("focusin", event => {
        const campo = event.target.closest?.(seletorCampos);
        if (campo) registrarCampo(campo);
    });

    document.addEventListener("input", event => {
        const campo = event.target.closest?.(seletorCampos);
        if (!campo) return;
        registrarCampo(campo);
        atualizarSugestoes(campo);
    });

    document.addEventListener("keydown", event => {
        if (!menu || menu.classList.contains("d-none")) return;
        if (!["ArrowDown", "ArrowUp", "Enter", "Escape", "Tab"].includes(event.key)) return;

        const itens = [...menu.querySelectorAll("[data-mention-user-id]")];
        if (!itens.length) return;

        if (event.key === "Escape") {
            fecharMenu();
            return;
        }

        event.preventDefault();
        if (event.key === "ArrowDown")
            indiceAtivo = (indiceAtivo + 1) % itens.length;
        else if (event.key === "ArrowUp")
            indiceAtivo = (indiceAtivo - 1 + itens.length) % itens.length;
        else
            selecionarMencao(itens[indiceAtivo]);

        marcarAtivo(itens);
    });

    document.addEventListener("mousedown", event => {
        const item = event.target.closest?.("[data-mention-user-id]");
        if (item) {
            event.preventDefault();
            selecionarMencao(item);
            return;
        }

        if (menu && !event.target.closest(".mention-menu"))
            fecharMenu();
    });

    window.renderizarTextoComMencoes = function (texto) {
        const seguro = escapeMentionHtml(String(texto || ""));
        return seguro.replace(/@\[([^\]\r\n]{1,100})\]\(usuario:(\d{1,10})\)/g, '<span class="mention-token">@$1</span>');
    };

    window.desserializarTextoComMencoes = function (texto) {
        return desserializarTextoMencoes(String(texto || "")).texto;
    };

    window.aplicarTextoComMencoesCampo = function (campo, texto) {
        if (!campo) return;

        const resultado = desserializarTextoMencoes(String(texto || ""));
        campo.value = resultado.texto;
        campo.dataset.mentions = JSON.stringify(resultado.mencoes);
    };

    window.serializarTextoComMencoes = function (campoOuTexto) {
        if (!campoOuTexto || typeof campoOuTexto !== "object" || !("value" in campoOuTexto)) {
            return String(campoOuTexto || "");
        }

        const campo = campoOuTexto;
        const texto = String(campo.value || "");
        const mencoes = lerMencoesCampo(campo);
        if (!mencoes.length) return texto;

        const substituicoes = [];
        const ocupados = [];
        for (const mencao of mencoes) {
            const exibicao = `@${mencao.nome}`;
            const indice = localizarMencao(texto, exibicao, Number(mencao.inicio || 0), ocupados);
            if (indice < 0) continue;

            const fim = indice + exibicao.length;
            ocupados.push({ inicio: indice, fim });
            substituicoes.push({
                inicio: indice,
                fim,
                token: `@[${mencao.nome}](usuario:${Number(mencao.usuarioId || 0)})`
            });
        }

        return substituicoes
            .sort((a, b) => b.inicio - a.inicio)
            .reduce((resultado, item) => resultado.slice(0, item.inicio) + item.token + resultado.slice(item.fim), texto);
    };

    window.inicializarMencoes = function (root) {
        (root || document).querySelectorAll(seletorCampos).forEach(registrarCampo);
    };

    document.addEventListener("DOMContentLoaded", () => window.inicializarMencoes(document));

    function registrarCampo(campo) {
        if (campo.dataset.mentionReady === "true") return;
        campo.dataset.mentionReady = "true";
        campo.setAttribute("autocomplete", "off");
    }

    function atualizarSugestoes(campo) {
        const gatilho = obterGatilho(campo);
        if (!gatilho) {
            fecharMenu();
            return;
        }

        campoAtivo = campo;
        estado = gatilho;
        clearTimeout(debounceId);
        debounceId = setTimeout(() => buscarMembros(gatilho.termo), 120);
    }

    function obterGatilho(campo) {
        const pos = campo.selectionStart ?? 0;
        const texto = campo.value || "";
        const antes = texto.slice(0, pos);
        const match = antes.match(/(^|\s)@([^\s@[\](){},.;:!?]{0,40})$/);
        if (!match) return null;

        const inicio = pos - match[2].length - 1;
        return { inicio, fim: pos, termo: match[2] || "" };
    }

    async function buscarMembros(termo) {
        if (!campoAtivo || !estado) return;

        try {
            const url = montarUrlBusca(termo);
            const response = await fetch(url);
            const data = await response.json();
            if (!response.ok || !data.success) throw new Error(data.message || "Nao foi possivel buscar membros.");
            renderizarMenu(data.dados || []);
        } catch {
            fecharMenu();
        }
    }

    function montarUrlBusca(termo) {
        const grupoId = obterGrupoIdAtual();
        const params = new URLSearchParams({ handler: "MembrosMencao", grupoId, termo: termo || "" });
        const chamadoId = document.getElementById("editId")?.value || document.getElementById("comentariosChamadoId")?.value || "";
        const cartaoId = document.getElementById("cartaoId")?.value || "";
        if (cartaoId) params.set("cartaoId", cartaoId);
        if (chamadoId) params.set("chamadoId", chamadoId);
        return `?${params.toString()}`;
    }

    function renderizarMenu(membros) {
        if (!campoAtivo || !estado || !membros.length) {
            fecharMenu();
            return;
        }

        garantirMenu();
        indiceAtivo = 0;
        menu.innerHTML = membros.map((membro, index) => `
            <button type="button" class="mention-menu-item ${index === 0 ? "active" : ""}"
                    data-mention-user-id="${Number(membro.usuarioId || 0)}"
                    data-mention-name="${escapeMentionHtml(membro.nomeExibicao || membro.nomeUsuario || "")}">
                <span class="mention-menu-name">${escapeMentionHtml(membro.nomeExibicao || "")}</span>
                <span class="mention-menu-user">@${escapeMentionHtml(membro.nomeUsuario || "")}</span>
            </button>
        `).join("");

        posicionarMenu();
        menu.classList.remove("d-none");
    }

    function garantirMenu() {
        if (menu) return;
        menu = document.createElement("div");
        menu.className = "mention-menu d-none";
        document.body.appendChild(menu);
    }

    function posicionarMenu() {
        const rect = campoAtivo.getBoundingClientRect();
        const lineHeight = Number.parseFloat(getComputedStyle(campoAtivo).lineHeight) || 20;
        const linhas = (campoAtivo.value.slice(0, campoAtivo.selectionStart || 0).match(/\n/g) || []).length;
        menu.style.left = `${Math.min(window.innerWidth - 280, Math.max(8, rect.left + window.scrollX + 12))}px`;
        menu.style.top = `${Math.min(window.innerHeight - 180 + window.scrollY, rect.top + window.scrollY + 12 + Math.min(linhas, 5) * lineHeight)}px`;
    }

    function marcarAtivo(itens) {
        itens.forEach((item, index) => item.classList.toggle("active", index === indiceAtivo));
        itens[indiceAtivo]?.scrollIntoView({ block: "nearest" });
    }

    function selecionarMencao(item) {
        if (!campoAtivo || !estado) return;

        const usuarioId = Number(item.dataset.mentionUserId || 0);
        const nome = item.dataset.mentionName || "";
        if (!usuarioId || !nome) return;

        const exibicao = `@${nome}`;
        const texto = campoAtivo.value || "";
        const inserirEspaco = texto.slice(estado.fim, estado.fim + 1) !== " ";
        const textoInserido = `${exibicao}${inserirEspaco ? " " : ""}`;
        campoAtivo.value = texto.slice(0, estado.inicio) + textoInserido + texto.slice(estado.fim);
        registrarMencaoCampo(campoAtivo, {
            usuarioId,
            nome,
            inicio: estado.inicio,
            fim: estado.inicio + exibicao.length
        });

        const pos = estado.inicio + textoInserido.length;
        campoAtivo.setSelectionRange?.(pos, pos);
        campoAtivo.dispatchEvent(new Event("input", { bubbles: true }));
        campoAtivo.focus();
        fecharMenu();
    }

    function fecharMenu() {
        if (menu) menu.classList.add("d-none");
        estado = null;
    }

    function lerMencoesCampo(campo) {
        try {
            const mencoes = JSON.parse(campo.dataset.mentions || "[]");
            return Array.isArray(mencoes)
                ? mencoes.filter(m => Number(m.usuarioId || 0) > 0 && m.nome)
                : [];
        } catch {
            return [];
        }
    }

    function registrarMencaoCampo(campo, mencao) {
        const mencoes = lerMencoesCampo(campo);
        mencoes.push(mencao);
        campo.dataset.mentions = JSON.stringify(mencoes);
    }

    function desserializarTextoMencoes(texto) {
        const mencoes = [];
        let textoVisivel = "";
        let ultimoIndice = 0;
        const regex = /@\[([^\]\r\n]{1,100})\]\(usuario:(\d{1,10})\)/g;

        for (const match of texto.matchAll(regex)) {
            const nome = match[1];
            const usuarioId = Number(match[2] || 0);
            const exibicao = `@${nome}`;
            const inicio = textoVisivel.length + match.index - ultimoIndice;

            textoVisivel += texto.slice(ultimoIndice, match.index) + exibicao;
            ultimoIndice = match.index + match[0].length;

            if (usuarioId > 0 && nome) {
                mencoes.push({
                    usuarioId,
                    nome,
                    inicio,
                    fim: inicio + exibicao.length
                });
            }
        }

        textoVisivel += texto.slice(ultimoIndice);
        return { texto: textoVisivel, mencoes };
    }

    function localizarMencao(texto, exibicao, inicioPreferido, ocupados) {
        if (texto.slice(inicioPreferido, inicioPreferido + exibicao.length) === exibicao &&
            !sobrepoeOcupado(inicioPreferido, inicioPreferido + exibicao.length, ocupados)) {
            return inicioPreferido;
        }

        let indice = texto.indexOf(exibicao);
        while (indice >= 0) {
            const fim = indice + exibicao.length;
            if (!sobrepoeOcupado(indice, fim, ocupados)) return indice;
            indice = texto.indexOf(exibicao, fim);
        }

        return -1;
    }

    function sobrepoeOcupado(inicio, fim, ocupados) {
        return ocupados.some(item => inicio < item.fim && fim > item.inicio);
    }

    function obterGrupoIdAtual() {
        return document.getElementById("grupoIdAtual")?.value ||
            document.getElementById("board")?.dataset.grupoId ||
            document.querySelector("[name='grupoId']")?.value ||
            new URLSearchParams(window.location.search).get("grupoId") ||
            "";
    }

    function escapeMentionHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }
})();
