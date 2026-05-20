(function () {
    "use strict";

    const ticketForm = document.getElementById("tutorialTicketForm");
    const ticketTitle = document.getElementById("tutorialTicketTitle");
    const ticketStage = document.getElementById("tutorialTicketStage");
    const resetTicket = document.getElementById("tutorialResetChamado");
    const moveTask = document.getElementById("tutorialMoveTask");
    const board = document.getElementById("tutorialBoard");
    const addNotification = document.getElementById("tutorialAddNotification");
    const notificationBadge = document.getElementById("tutorialNotificationBadge");
    const notificationList = document.getElementById("tutorialNotificationList");
    const sidebarOpacity = document.getElementById("tutorialSidebarOpacity");
    const modalOpacity = document.getElementById("tutorialModalOpacity");
    const appearanceShell = document.getElementById("tutorialAppearanceShell");

    let ticketCounter = 1;
    let taskColumn = 0;
    let notificationCounter = 0;

    function clampPercent(value) {
        const numeric = Number(value);
        if (Number.isNaN(numeric)) {
            return 100;
        }

        return Math.min(100, Math.max(30, numeric));
    }

    function setText(element, value) {
        if (element) {
            element.textContent = value;
        }
    }

    function clearElement(element) {
        if (!element) {
            return;
        }

        while (element.firstChild) {
            element.removeChild(element.firstChild);
        }
    }

    function renderEmptyState(container, text) {
        clearElement(container);
        const empty = document.createElement("div");
        empty.className = "demo-empty-state";
        empty.textContent = text;
        container.appendChild(empty);
    }

    function createTicket(title) {
        const ticket = document.createElement("article");
        ticket.className = "demo-ticket";

        const icon = document.createElement("div");
        icon.className = "demo-ticket-icon";
        icon.innerHTML = '<i class="bi bi-ticket-detailed" aria-hidden="true"></i>';

        const body = document.createElement("div");
        const titleElement = document.createElement("div");
        titleElement.className = "demo-ticket-title";
        titleElement.textContent = title;

        const meta = document.createElement("div");
        meta.className = "demo-ticket-meta";
        meta.textContent = "Aberto - Prioridade media - Agora";

        const number = document.createElement("div");
        number.className = "demo-ticket-number";
        number.textContent = `#${String(ticketCounter).padStart(3, "0")}`;

        body.append(titleElement, meta);
        ticket.append(icon, body, number);
        ticketCounter += 1;

        return ticket;
    }

    function resetTicketDemo() {
        renderEmptyState(ticketStage, "Crie um chamado de exemplo.");
        if (ticketTitle) {
            ticketTitle.value = "";
            ticketTitle.focus();
        }
    }

    ticketForm?.addEventListener("submit", event => {
        event.preventDefault();

        const title = String(ticketTitle?.value || "").trim().slice(0, 70);
        if (!title || !ticketStage) {
            return;
        }

        clearElement(ticketStage);
        ticketStage.appendChild(createTicket(title));
        ticketTitle.value = "";
    });

    resetTicket?.addEventListener("click", resetTicketDemo);

    moveTask?.addEventListener("click", () => {
        const task = board?.querySelector("[data-demo-task='true']");
        if (!task || !board) {
            return;
        }

        taskColumn = (taskColumn + 1) % 3;
        board.querySelector(`[data-column='${taskColumn}']`)?.appendChild(task);
        task.textContent = taskColumn === 0
            ? "Conferir dados do chamado"
            : taskColumn === 1
                ? "Executando atendimento"
                : "Atendimento concluido";
    });

    addNotification?.addEventListener("click", () => {
        if (!notificationList || !notificationBadge) {
            return;
        }

        if (notificationCounter === 0) {
            clearElement(notificationList);
        }

        notificationCounter += 1;
        setText(notificationBadge, String(notificationCounter));

        const item = document.createElement("div");
        item.className = "demo-notification-item";

        const title = document.createElement("strong");
        title.textContent = notificationCounter % 2 === 0
            ? "Comentario recebido"
            : "Status atualizado";

        const detail = document.createElement("span");
        detail.textContent = notificationCounter % 2 === 0
            ? "Um novo comentario foi anexado ao chamado de exemplo."
            : "O chamado de exemplo avancou no fluxo de atendimento.";

        item.append(title, detail);
        notificationList.prepend(item);
    });

    function updateAppearanceDemo() {
        if (!appearanceShell) {
            return;
        }

        appearanceShell.style.setProperty("--demo-sidebar-opacity", String(clampPercent(sidebarOpacity?.value) / 100));
        appearanceShell.style.setProperty("--demo-modal-opacity", String(clampPercent(modalOpacity?.value) / 100));
    }

    sidebarOpacity?.addEventListener("input", updateAppearanceDemo);
    modalOpacity?.addEventListener("input", updateAppearanceDemo);
    updateAppearanceDemo();
})();
