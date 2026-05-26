const dashboardCharts = new Map();
let dashboardResumoCarregado = false;

document.addEventListener("DOMContentLoaded", () => {
    configurarDatasPadrao();
    document.getElementById("btnAplicarDashboard")?.addEventListener("click", async () => {
        dashboardResumoCarregado = false;
        await carregarGraficoPrincipal();
        await carregarResumoDashboard();
    });

    carregarGraficoPrincipal();
    configurarLazyDashboard();
});

function configurarDatasPadrao() {
    const dataInicial = document.getElementById("dashboardDataInicial");
    const dataFinal = document.getElementById("dashboardDataFinal");
    if (!dataInicial || !dataFinal) return;

    const hoje = new Date();
    const inicio = new Date();
    inicio.setDate(hoje.getDate() - 29);
    dataInicial.value = formatDateInput(inicio);
    dataFinal.value = formatDateInput(hoje);
}

function configurarLazyDashboard() {
    const blocos = document.getElementById("dashboardBlocos");
    if (!blocos) return;

    if (!("IntersectionObserver" in window)) {
        carregarResumoDashboard();
        return;
    }

    const observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) {
            observer.disconnect();
            carregarResumoDashboard();
        }
    }, { rootMargin: "240px" });

    observer.observe(blocos);
}

async function carregarGraficoPrincipal() {
    const loading = document.getElementById("dashboardLoadingPrincipal");
    loading?.classList.remove("d-none");

    try {
        const data = await fetchJson(`?handler=ObterGraficoPrincipal&grupoId=${encodeURIComponent(getGrupoId())}`, montarPayloadFiltros());
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar o gráfico principal.");
            return;
        }

        const grafico = data.dados;
        document.getElementById("dashboardTituloPrincipal").textContent = grafico.titulo || "Dashboard";
        renderizarGrafico("dashboardGraficoPrincipal", grafico.tipo || "line", grafico.pontos || [], {
            label: grafico.titulo || "Chamados",
            principal: true
        });
    } catch (error) {
        mostrarToast(error.message || "Não foi possível carregar o gráfico principal.");
        alternarEstadoGrafico("dashboardGraficoPrincipal", true);
    } finally {
        loading?.classList.add("d-none");
    }
}

async function carregarResumoDashboard() {
    if (dashboardResumoCarregado) return;
    dashboardResumoCarregado = true;

    try {
        const data = await fetchJson(`?handler=ObterResumoDashboard&grupoId=${encodeURIComponent(getGrupoId())}`, montarPayloadFiltros());
        if (!data.success) {
            mostrarToast(data.message || "Não foi possível carregar o dashboard.");
            return;
        }

        renderizarResumo(data.dados || {});
    } catch (error) {
        dashboardResumoCarregado = false;
        mostrarToast(error.message || "Não foi possível carregar o dashboard.");
    }
}

function renderizarResumo(dados) {
    const indicadores = dados.indicadores || {};
    const tipoSla = dados.tipoSla || getValue("dashboardTipoSla") || "liquido";
    setMetric("totalChamados", indicadores.totalChamados);
    setMetric("totalFechados", indicadores.totalFechados);
    setMetric("totalReabertos", indicadores.totalReabertos);
    setMetric("tempoMedioSolucaoHoras", formatHoras(indicadores.tempoMedioSolucaoHoras));
    setMetricLabel("tempoMedioSolucaoHoras", `Tempo médio de solução SLA ${tipoSla === "bruto" ? "bruto" : "líquido"}`);

    renderizarGrafico("dashboardGraficoSetor", "bar", dados.porSetor || [], { label: "Chamados" });
    renderizarGrafico("dashboardGraficoTipo", "bar", dados.porTipo || [], { label: "Chamados" });
    renderizarGrafico("dashboardGraficoStatus", "doughnut", dados.porStatus || [], { label: "Chamados" });
    renderizarGrafico("dashboardGraficoTarefas", "doughnut", dados.tarefasPorStatus || [], { label: "Tarefas" });

    if (dados.podeVerUsuarios) {
        renderizarGrafico("dashboardGraficoUsuario", "bar", dados.porUsuario || [], { label: "Chamados" });
    }

    renderizarTabelaReabertos(dados.problemasReabertos || []);
    renderizarPrevisao(dados.previsao || {});
}

function montarPayloadFiltros() {
    return {
        dataInicial: getValue("dashboardDataInicial") || null,
        dataFinal: getValue("dashboardDataFinal") || null,
        mes: toNullableInt(getValue("dashboardMes")),
        setorId: toNullableInt(getValue("dashboardSetor")),
        usuarioId: toNullableInt(getValue("dashboardUsuario")),
        tipoId: toNullableInt(getValue("dashboardTipo")),
        status: getValue("dashboardStatus") || null,
        apenasFechados: document.getElementById("dashboardFechados")?.checked || false,
        apenasReabertos: document.getElementById("dashboardReabertos")?.checked || false,
        incluirPrevisao: document.getElementById("dashboardPrevisao")?.checked || false,
        dimensaoPrincipal: getValue("dashboardDimensao") || "evolucao",
        metricaCruzada: getValue("dashboardMetrica") || "quantidade",
        tipoSla: getValue("dashboardTipoSla") || "liquido"
    };
}

function renderizarGrafico(canvasId, tipo, pontos, opcoes = {}) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === "undefined") return;

    const valores = pontos.map(p => Number(p.valor || 0));
    const vazio = !pontos.length || valores.reduce((total, valor) => total + valor, 0) <= 0;
    alternarEstadoGrafico(canvasId, vazio);
    if (vazio) {
        destruirGrafico(canvasId);
        return;
    }

    destruirGrafico(canvasId);
    const labels = pontos.map(p => p.label || "Sem registro");
    const cores = gerarCores(labels.length);

    dashboardCharts.set(canvasId, new Chart(canvas, {
        type: tipo,
        data: {
            labels,
            datasets: [{
                label: opcoes.label || "Quantidade",
                data: valores,
                borderColor: "#2563eb",
                backgroundColor: tipo === "line" ? "#2563eb" : cores,
                tension: tipo === "line" ? 0 : .25,
                fill: false,
                pointRadius: tipo === "line" ? 4 : 3,
                pointHoverRadius: tipo === "line" ? 5 : 4,
                clip: 8,
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: tipo === "doughnut" },
                tooltip: { enabled: true }
            },
            scales: tipo === "doughnut" ? {} : {
                y: { beginAtZero: true, ticks: { precision: 0 } },
                x: { ticks: { maxRotation: 45, minRotation: 0 } }
            }
        }
    }));
}

function renderizarTabelaReabertos(pontos) {
    const container = document.getElementById("dashboardTabelaReabertos");
    if (!container) return;

    if (!pontos.length) {
        container.innerHTML = '<div class="state-box">Sem chamados reabertos no período.</div>';
        return;
    }

    const linhas = pontos.map(p => `
        <tr>
            <td>${escapeHtml(p.label || "Sem tipo")}</td>
            <td class="text-end fw-semibold">${escapeHtml(String(p.valor || 0))}</td>
        </tr>
    `).join("");

    container.innerHTML = `
        <table class="table table-sm dashboard-table align-middle mb-0">
            <thead>
                <tr>
                    <th>Problema</th>
                    <th class="text-end">Reaberturas</th>
                </tr>
            </thead>
            <tbody>${linhas}</tbody>
        </table>
    `;
}

function renderizarPrevisao(previsao) {
    const container = document.getElementById("dashboardPrevisaoResultado");
    if (!container) return;

    if (!previsao.disponivel) {
        container.innerHTML = `<div>${escapeHtml(previsao.mensagem || "Não há dados suficientes para previsão confiável.")}</div>`;
        return;
    }

    container.innerHTML = `
        <div class="w-100">
            <div class="text-muted small mb-1">Estimativa/tendência</div>
            <div class="metric-value">${escapeHtml(String(previsao.estimativaProximoMes ?? 0))}</div>
            <div class="small text-muted">${escapeHtml(previsao.mensagem || "")}</div>
            <span class="badge text-bg-warning mt-2">Confiabilidade ${escapeHtml(previsao.confiabilidade || "baixa")}</span>
        </div>
    `;
}

function alternarEstadoGrafico(canvasId, vazio) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    canvas.classList.toggle("d-none", vazio);
    const estado = canvas.parentElement?.querySelector(".state-box");
    estado?.classList.toggle("d-none", !vazio);
}

function destruirGrafico(canvasId) {
    const chart = dashboardCharts.get(canvasId);
    if (chart) {
        chart.destroy();
        dashboardCharts.delete(canvasId);
    }
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

function mostrarToast(mensagem, tipo = "danger") {
    let container = document.querySelector(".toast-container");
    if (!container) {
        container = document.createElement("div");
        container.className = "toast-container position-fixed bottom-0 end-0 p-3";
        document.body.appendChild(container);
    }

    const toastEl = document.createElement("div");
    toastEl.className = `toast align-items-center text-bg-${tipo} border-0`;
    toastEl.setAttribute("role", "alert");
    toastEl.setAttribute("aria-live", "assertive");
    toastEl.setAttribute("aria-atomic", "true");
    toastEl.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${escapeHtml(String(mensagem || "Ocorreu um erro."))}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Fechar"></button>
        </div>
    `;

    container.appendChild(toastEl);
    const toast = new bootstrap.Toast(toastEl, { delay: 4500 });
    toast.show();
    toastEl.addEventListener("hidden.bs.toast", () => toastEl.remove());
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function gerarCores(total) {
    const base = ["#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#7c3aed", "#0891b2", "#475569", "#db2777", "#65a30d", "#ea580c", "#0f766e", "#9333ea"];
    return Array.from({ length: total }, (_, i) => base[i % base.length]);
}

function setMetric(nome, valor) {
    const el = document.querySelector(`[data-metric="${nome}"]`);
    if (el) el.textContent = valor === null || valor === undefined || valor === "" ? "-" : String(valor);
}

function setMetricLabel(nome, valor) {
    const valorEl = document.querySelector(`[data-metric="${nome}"]`);
    const labelEl = valorEl?.parentElement?.querySelector(".metric-label");
    if (labelEl) labelEl.textContent = valor;
}

function formatHoras(valor) {
    if (valor === null || valor === undefined) return "-";
    return `${Number(valor).toLocaleString("pt-BR", { maximumFractionDigits: 1 })}h`;
}

function formatDateInput(data) {
    const ano = data.getFullYear();
    const mes = String(data.getMonth() + 1).padStart(2, "0");
    const dia = String(data.getDate()).padStart(2, "0");
    return `${ano}-${mes}-${dia}`;
}

function getValue(id) {
    return document.getElementById(id)?.value || "";
}

function getToken() {
    return document.getElementById("requestVerificationToken")?.value || "";
}

function getGrupoId() {
    return document.getElementById("dashboardGrupoId")?.value || "";
}

function toNullableInt(value) {
    const parsed = parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : null;
}
