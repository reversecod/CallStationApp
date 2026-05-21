(function () {
    "use strict";

    const ticketForm = document.getElementById("tutorialTicketForm");
    const ticketTitle = document.getElementById("tutorialTicketTitle");
    const ticketStage = document.getElementById("tutorialTicketStage");
    const resetTicket = document.getElementById("tutorialResetChamado");
    const newTicketButton = document.getElementById("tutorialNewTicketButton");
    const board = document.getElementById("tutorialBoard");
    const addListButton = document.getElementById("tutorialAddListButton");
    const resetBoard = document.getElementById("tutorialResetBoard");
    const addNotification = document.getElementById("tutorialAddNotification");
    const notificationBadge = document.getElementById("tutorialNotificationBadge");
    const notificationList = document.getElementById("tutorialNotificationList");
    const sidebarOpacity = document.getElementById("tutorialSidebarOpacity");
    const modalOpacity = document.getElementById("tutorialModalOpacity");
    const appearanceShell = document.getElementById("tutorialAppearanceShell");

    let ticketCounter = 1;
    let listCounter = 1;
    let cardCounter = 1;
    let draggedList = null;
    let draggedCard = null;
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
        ticketForm?.classList.add("d-none");
        if (ticketTitle) {
            ticketTitle.value = "";
        }
    }

    function initializeNewTicketAnimation(button) {
        let hoverTimer = null;
        const delayMs = 180;

        const show = () => {
            clearTimeout(hoverTimer);
            hoverTimer = setTimeout(() => {
                button.classList.add("ticket-preview-visivel");
            }, delayMs);
        };

        const hide = () => {
            clearTimeout(hoverTimer);
            button.classList.remove("ticket-preview-visivel");
        };

        button.addEventListener("mouseenter", show);
        button.addEventListener("mouseleave", hide);
        button.addEventListener("focusin", show);
        button.addEventListener("focusout", hide);
    }

    newTicketButton?.addEventListener("click", () => {
        ticketForm?.classList.remove("d-none");
        ticketTitle?.focus();
    });

    if (newTicketButton) {
        initializeNewTicketAnimation(newTicketButton);
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

    function createButton(className, text, iconClass) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        if (iconClass) {
            const icon = document.createElement("i");
            icon.className = `bi ${iconClass}`;
            icon.setAttribute("aria-hidden", "true");
            button.appendChild(icon);
        }
        button.appendChild(document.createTextNode(text));
        return button;
    }

    function updateListCount(list) {
        const count = list?.querySelectorAll(".demo-mini-card").length || 0;
        setText(list?.querySelector(".demo-mini-list-count"), String(count));
    }

    function showInlineForm(container, placeholder, onSubmit) {
        if (!container || container.querySelector(".demo-inline-form")) {
            return;
        }

        const form = document.createElement("form");
        form.className = "demo-inline-form";
        form.autocomplete = "off";

        const input = document.createElement("input");
        input.type = "text";
        input.maxLength = 50;
        input.placeholder = placeholder;

        const actions = document.createElement("div");
        actions.className = "demo-inline-actions";

        const save = createButton("btn btn-sm btn-primary", "Criar");
        save.type = "submit";
        const cancel = createButton("btn btn-sm btn-outline-secondary", "Cancelar");

        actions.append(save, cancel);
        form.append(input, actions);
        container.appendChild(form);
        input.focus();

        form.addEventListener("submit", event => {
            event.preventDefault();
            const value = input.value.trim().slice(0, 50);
            if (!value) {
                return;
            }

            form.remove();
            onSubmit(value);
        });

        cancel.addEventListener("click", () => form.remove());
    }

    function getCardAfter(container, y) {
        const cards = [...container.querySelectorAll(".demo-mini-card:not(.is-dragging)")];
        return cards.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && offset > closest.offset) {
                return { offset, element: child };
            }
            return closest;
        }, { offset: Number.NEGATIVE_INFINITY, element: null }).element;
    }

    function initializeCard(card) {
        card.draggable = true;
        card.addEventListener("dragstart", () => {
            draggedCard = card;
            card.classList.add("is-dragging");
        });
        card.addEventListener("dragend", () => {
            card.classList.remove("is-dragging");
            document.querySelectorAll(".demo-mini-cards").forEach(cards => {
                cards.classList.remove("is-drop-target");
                updateListCount(cards.closest(".demo-mini-list"));
            });
            draggedCard = null;
        });
    }

    function addCard(list, title) {
        const cards = list?.querySelector(".demo-mini-cards");
        if (!cards) {
            return;
        }

        const card = document.createElement("div");
        card.className = "demo-mini-card";
        card.dataset.demoCardId = String(cardCounter++);
        card.textContent = title;
        initializeCard(card);
        cards.appendChild(card);
        updateListCount(list);
    }

    function getListAfter(container, x) {
        const lists = [...container.querySelectorAll(".demo-mini-list:not(.is-dragging)")];
        return lists.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = x - box.left - box.width / 2;
            if (offset < 0 && offset > closest.offset) {
                return { offset, element: child };
            }
            return closest;
        }, { offset: Number.NEGATIVE_INFINITY, element: null }).element;
    }

    function initializeList(list) {
        const handle = list.querySelector(".demo-mini-list-handle");
        const cards = list.querySelector(".demo-mini-cards");
        const addCardButton = list.querySelector(".demo-add-card-button");

        handle?.addEventListener("dragstart", () => {
            draggedList = list;
            list.classList.add("is-dragging");
        });
        handle?.addEventListener("dragend", () => {
            list.classList.remove("is-dragging");
            draggedList = null;
        });

        cards?.addEventListener("dragover", event => {
            if (!draggedCard) {
                return;
            }

            event.preventDefault();
            cards.classList.add("is-drop-target");
            const after = getCardAfter(cards, event.clientY);
            if (after) {
                cards.insertBefore(draggedCard, after);
            } else {
                cards.appendChild(draggedCard);
            }
        });

        cards?.addEventListener("dragleave", () => {
            cards.classList.remove("is-drop-target");
        });

        addCardButton?.addEventListener("click", () => {
            showInlineForm(list, "Nome da tarefa", value => addCard(list, value));
        });
    }

    function createList(title) {
        if (!board || !addListButton) {
            return null;
        }

        const list = document.createElement("section");
        list.className = "demo-mini-list";
        list.dataset.demoListId = String(listCounter++);

        const header = document.createElement("div");
        header.className = "demo-mini-list-header";

        const handle = document.createElement("span");
        handle.className = "demo-mini-list-handle";
        handle.draggable = true;
        handle.title = "Mover lista";
        handle.setAttribute("aria-label", "Mover lista");
        handle.innerHTML = '<i class="bi bi-grip-vertical" aria-hidden="true"></i>';

        const titleElement = document.createElement("div");
        titleElement.className = "demo-mini-list-title";
        titleElement.textContent = title;

        const count = document.createElement("span");
        count.className = "demo-mini-list-count";
        count.textContent = "0";

        const cards = document.createElement("div");
        cards.className = "demo-mini-cards";

        const addCardButton = createButton("demo-add-card-button", "Adicionar tarefa", "bi-plus-lg");

        header.append(handle, titleElement, count);
        list.append(header, cards, addCardButton);
        board.insertBefore(list, addListButton.closest(".demo-add-list-panel"));
        initializeList(list);
        return list;
    }

    board?.addEventListener("dragover", event => {
        if (!draggedList || !board) {
            return;
        }

        event.preventDefault();
        const after = getListAfter(board, event.clientX);
        const addPanel = board.querySelector(".demo-add-list-panel");
        if (after) {
            board.insertBefore(draggedList, after);
        } else if (addPanel) {
            board.insertBefore(draggedList, addPanel);
        }
    });

    addListButton?.addEventListener("click", () => {
        const panel = addListButton.closest(".demo-add-list-panel");
        showInlineForm(panel, "Nome da lista", value => {
            const list = createList(value);
            if (list) {
                addCard(list, "Primeira tarefa");
            }
        });
    });

    resetBoard?.addEventListener("click", () => {
        board?.querySelectorAll(".demo-mini-list").forEach(list => list.remove());
        listCounter = 1;
        cardCounter = 1;
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
