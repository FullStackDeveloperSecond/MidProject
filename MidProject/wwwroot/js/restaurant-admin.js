/*
  Kefan 餐廳模組（Restaurants / Tags）前端行為。
  負責：新增/編輯餐廳的 AJAX 彈出視窗、營業時間動態表單、標籤 pill、
  圖片選擇預覽、詳細頁圖片燈箱。
*/
const RestaurantAdmin = (() => {
    const DAY_LABELS = ["一", "二", "三", "四", "五", "六", "日"];
    let hoursState = [];
    let formIsDirty = false;

    function modalRoot() {
        return document.getElementById("modalRoot");
    }

    // Filter form submits and pagination clicks fetch just the content partial
    // and swap it into `contentId`, instead of a full-page GET navigation. Two
    // timing rules keep the loading overlay from flashing on fast responses:
    // it only appears if the request is still running after SHOW_DELAY_MS (most
    // filter queries resolve well under that), and once shown it stays for at
    // least MIN_VISIBLE_MS so it never reads as a single flicker.
    const AJAX_SHOW_DELAY_MS = 300;
    const AJAX_MIN_VISIBLE_MS = 450;

    function bindAjaxContent(config) {
        const content = document.getElementById(config.contentId);
        const overlay = config.overlayId ? document.getElementById(config.overlayId) : null;
        if (!content) {
            return;
        }

        function bindInteractions() {
            const form = document.getElementById(config.formId);
            if (form) {
                form.addEventListener("submit", (e) => {
                    e.preventDefault();
                    const params = new URLSearchParams(new FormData(form));
                    navigate(form.getAttribute("action") + "?" + params.toString());
                });
            }

            content.querySelectorAll(".sp-pagination .page-link").forEach(link => {
                link.addEventListener("click", (e) => {
                    e.preventDefault();
                    navigate(link.getAttribute("href"));
                });
            });

            if (typeof config.afterRender === "function") {
                config.afterRender();
            }
        }

        function navigate(url, updateHistory = true) {
            let overlayShownAt = null;
            const showTimer = overlay ? setTimeout(() => {
                overlay.classList.add("show");
                overlayShownAt = Date.now();
            }, AJAX_SHOW_DELAY_MS) : null;

            fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } })
                .then(res => {
                    if (!res.ok) {
                        throw new Error("navigation failed");
                    }
                    return res.text();
                })
                .then(html => {
                    clearTimeout(showTimer);
                    const apply = () => {
                        content.innerHTML = html;
                        if (overlay) {
                            overlay.classList.remove("show");
                        }
                        if (updateHistory) {
                            history.pushState({ raAjax: true }, "", url);
                        }
                        bindInteractions();
                    };
                    const elapsed = overlayShownAt !== null ? Date.now() - overlayShownAt : 0;
                    const remaining = overlayShownAt !== null ? AJAX_MIN_VISIBLE_MS - elapsed : 0;
                    if (remaining > 0) {
                        setTimeout(apply, remaining);
                    } else {
                        apply();
                    }
                })
                .catch(() => {
                    clearTimeout(showTimer);
                    if (overlay) {
                        overlay.classList.remove("show");
                    }
                    window.location.href = url;
                });
        }

        bindInteractions();

        window.addEventListener("popstate", () => navigate(window.location.href, false));
    }

    function openModalHtml(html, options = {}) {
        const root = modalRoot();
        if (!root) return;
        root.innerHTML = html;
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        bindModalChrome();
        initFormModal(options.preserveDirty);
    }

    function closeModal() {
        const root = modalRoot();
        if (!root) return;
        root.onkeydown = null;
        root.classList.remove("open");
        root.setAttribute("aria-hidden", "true");
        root.innerHTML = "";
        formIsDirty = false;
    }

    function bindModalChrome() {
        const root = modalRoot();
        root.querySelectorAll("[data-action='close-modal']").forEach(btn => {
            btn.addEventListener("click", requestCloseModal);
        });
        root.addEventListener("click", event => {
            if (event.target === root) requestCloseModal();
        });
    }

    // Gate closing the Create/Edit form modal behind a confirmation if the user
    // has changed anything (name, tags, images, business hours...) that hasn't
    // been saved yet. Other modals (image lightbox, disable-reason) have no form
    // in them, so `form` is null and this just closes immediately as before.
    function requestCloseModal() {
        const form = document.getElementById("restaurantForm");
        if (form && formIsDirty) {
            openDiscardConfirm();
            return;
        }
        closeModal();
    }

    function markFormDirty() {
        formIsDirty = true;
    }

    function bindDirtyTracking() {
        const form = document.getElementById("restaurantForm");
        if (!form) return;
        form.addEventListener("input", markFormDirty);
        form.addEventListener("change", markFormDirty);
    }

    function ensureDiscardConfirmRoot() {
        let root = document.getElementById("discardConfirmRoot");
        if (root) return root;

        root = document.createElement("div");
        root.id = "discardConfirmRoot";
        root.className = "modal-root";
        root.innerHTML = `
            <div class="ra-modal confirm-modal" role="alertdialog" aria-modal="true" aria-label="尚有未儲存的變更">
                <div class="modal-body">
                    <p>尚有未儲存的變更內容，是否要儲存呢？</p>
                </div>
                <div class="modal-foot">
                    <button type="button" class="btn" data-confirm="cancel">取消</button>
                    <button type="button" class="btn btn-danger" data-confirm="discard">不儲存</button>
                    <button type="button" class="btn btn-primary" data-confirm="save">儲存</button>
                </div>
            </div>`;
        document.body.appendChild(root);

        root.addEventListener("click", event => {
            if (event.target === root) hideDiscardConfirm();
        });

        return root;
    }

    function hideDiscardConfirm() {
        const root = document.getElementById("discardConfirmRoot");
        if (!root) return;
        root.classList.remove("open");
        root.setAttribute("aria-hidden", "true");
    }

    function openDiscardConfirm() {
        const root = ensureDiscardConfirmRoot();
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");

        root.querySelector("[data-confirm='cancel']").onclick = () => {
            hideDiscardConfirm();
        };
        root.querySelector("[data-confirm='discard']").onclick = () => {
            hideDiscardConfirm();
            formIsDirty = false;
            closeModal();
        };
        root.querySelector("[data-confirm='save']").onclick = () => {
            hideDiscardConfirm();
            const form = document.getElementById("restaurantForm");
            if (!form) return;
            if (form.requestSubmit) {
                form.requestSubmit();
            } else {
                form.dispatchEvent(new Event("submit", { cancelable: true, bubbles: true }));
            }
        };
    }

    async function openCreateModal() {
        const res = await fetch("/Restaurants/CreateForm");
        openModalHtml(await res.text());
    }

    async function openEditModal(id) {
        const res = await fetch(`/Restaurants/EditForm/${id}`);
        if (!res.ok) {
            toast("找不到餐廳資料。");
            return;
        }
        openModalHtml(await res.text());
    }

    function openImageLightbox(images, initialIndex = 0) {
        const root = modalRoot();
        if (!root) return;
        const sources = (Array.isArray(images) ? images : [images]).filter(Boolean);
        if (sources.length === 0) return;
        let currentIndex = Math.min(Math.max(Number(initialIndex) || 0, 0), sources.length - 1);
        root.innerHTML = `
            <div class="ra-modal lightbox-modal" role="dialog" aria-modal="true" aria-label="圖片檢視">
                <div class="modal-head">
                    <h2>圖片檢視</h2>
                    <span class="lightbox-counter" data-lightbox-counter></span>
                    <button type="button" class="btn btn-icon" data-action="close-modal" aria-label="關閉">×</button>
                </div>
                <div class="lightbox-body">
                    <button type="button" class="lightbox-nav lightbox-nav-prev" data-lightbox-nav="prev" aria-label="上一張圖片">
                        <i class="fas fa-chevron-left" aria-hidden="true"></i>
                    </button>
                    <img class="lightbox-image" alt="餐廳圖片">
                    <button type="button" class="lightbox-nav lightbox-nav-next" data-lightbox-nav="next" aria-label="下一張圖片">
                        <i class="fas fa-chevron-right" aria-hidden="true"></i>
                    </button>
                </div>
            </div>`;

        const image = root.querySelector(".lightbox-image");
        const counter = root.querySelector("[data-lightbox-counter]");
        const previous = root.querySelector("[data-lightbox-nav='prev']");
        const next = root.querySelector("[data-lightbox-nav='next']");

        function render() {
            image.src = sources[currentIndex];
            image.alt = `餐廳圖片 ${currentIndex + 1}，共 ${sources.length} 張`;
            counter.textContent = `${currentIndex + 1} / ${sources.length}`;
            previous.hidden = sources.length <= 1;
            next.hidden = sources.length <= 1;
        }

        function move(direction) {
            currentIndex = (currentIndex + direction + sources.length) % sources.length;
            render();
        }

        previous.addEventListener("click", () => move(-1));
        next.addEventListener("click", () => move(1));
        root.onkeydown = event => {
            if (event.key === "ArrowLeft") move(-1);
            if (event.key === "ArrowRight") move(1);
            if (event.key === "Escape") closeModal();
        };
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        root.tabIndex = -1;
        root.focus();
        bindModalChrome();
        render();
    }

    function initDetailImageGallery(images) {
        const sources = (Array.isArray(images) ? images : []).filter(Boolean);
        document.querySelectorAll(".js-view-image[data-gallery-index]").forEach(button => {
            button.addEventListener("click", () => {
                openImageLightbox(sources, Number(button.dataset.galleryIndex));
            });
        });

        const carousel = document.querySelector("[data-environment-gallery]");
        if (!carousel) return;
        const items = Array.from(carousel.querySelectorAll("[data-environment-image]"));
        const pageSize = Math.max(Number(carousel.dataset.pageSize) || 6, 1);
        const pageCount = Math.max(Math.ceil(items.length / pageSize), 1);
        const previous = carousel.querySelector("[data-gallery-page='prev']");
        const next = carousel.querySelector("[data-gallery-page='next']");
        const indicator = carousel.querySelector("[data-gallery-page-indicator]");
        let page = 0;

        function renderPage() {
            items.forEach((item, index) => {
                item.classList.toggle("d-none", Math.floor(index / pageSize) !== page);
            });
            if (previous) previous.disabled = page === 0;
            if (next) next.disabled = page >= pageCount - 1;
            if (indicator) indicator.textContent = `${page + 1} / ${pageCount}`;
        }

        previous?.addEventListener("click", () => {
            if (page > 0) {
                page--;
                renderPage();
            }
        });
        next?.addEventListener("click", () => {
            if (page < pageCount - 1) {
                page++;
                renderPage();
            }
        });
        renderPage();
    }

    function toast(message) {
        const el = document.getElementById("toast");
        if (!el) return;
        el.textContent = message;
        el.classList.add("show");
        window.clearTimeout(toast.timer);
        toast.timer = window.setTimeout(() => el.classList.remove("show"), 2200);
    }

    // ---- Business hours editor (JSON hidden input, no fragile list-index binding) ----

    function parseHoursInput() {
        const input = document.getElementById("hoursJsonInput");
        if (!input) return defaultHours();
        try {
            const parsed = JSON.parse(input.value || "[]");
            const isValidShape = Array.isArray(parsed) && parsed.length === 7 &&
                parsed.every(row => typeof row.day === "number" && Array.isArray(row.slots));
            if (isValidShape) {
                return parsed;
            }
        } catch (error) {
            // fall through to default
        }
        return defaultHours();
    }

    function defaultHours() {
        const rows = [];
        for (let day = 1; day <= 7; day++) {
            rows.push({ day, closed: false, slots: [{ open: "11:00", close: "21:00" }] });
        }
        return rows;
    }

    function syncHoursInput() {
        const input = document.getElementById("hoursJsonInput");
        if (input) {
            input.value = JSON.stringify(hoursState);
        }
    }

    function renderHours() {
        const container = document.getElementById("hoursForm");
        if (!container) return;

        container.innerHTML = hoursState.map((day, dayIndex) => `
            <div class="hour-form-row" data-day-index="${dayIndex}">
                <strong>週${DAY_LABELS[day.day - 1]}</strong>
                <label class="switch">
                    <input type="checkbox" class="js-toggle-day" ${day.closed ? "" : "checked"}>
                    營業
                </label>
                <div class="slot-list">
                    ${day.closed
                        ? '<span class="muted">公休</span>'
                        : day.slots.map((slot, slotIndex) => slotRowHtml(slot, slotIndex)).join("") +
                          '<button type="button" class="btn js-add-slot">+ 新增時段</button>'}
                </div>
            </div>
        `).join("");

        bindHoursEvents();
        syncHoursInput();
    }

    function slotRowHtml(slot, slotIndex) {
        return `
            <div class="slot-row" data-slot-index="${slotIndex}">
                <input class="input" type="time" data-field="open" value="${slot.open}">
                <span>至</span>
                <input class="input" type="time" data-field="close" value="${slot.close}">
                <button type="button" class="btn btn-icon js-remove-slot">×</button>
            </div>
        `;
    }

    function bindHoursEvents() {
        const container = document.getElementById("hoursForm");
        if (!container) return;

        container.querySelectorAll(".js-toggle-day").forEach(checkbox => {
            checkbox.addEventListener("change", event => {
                markFormDirty();
                const dayIndex = Number(event.target.closest("[data-day-index]").dataset.dayIndex);
                hoursState[dayIndex].closed = !event.target.checked;
                if (!hoursState[dayIndex].closed && hoursState[dayIndex].slots.length === 0) {
                    hoursState[dayIndex].slots = [{ open: "11:00", close: "21:00" }];
                }
                renderHours();
            });
        });

        container.querySelectorAll(".js-add-slot").forEach(button => {
            button.addEventListener("click", event => {
                markFormDirty();
                const dayIndex = Number(event.target.closest("[data-day-index]").dataset.dayIndex);
                hoursState[dayIndex].slots.push({ open: "17:00", close: "21:00" });
                renderHours();
            });
        });

        container.querySelectorAll(".js-remove-slot").forEach(button => {
            button.addEventListener("click", event => {
                const dayRow = event.target.closest("[data-day-index]");
                const slotRow = event.target.closest("[data-slot-index]");
                const dayIndex = Number(dayRow.dataset.dayIndex);
                const slotIndex = Number(slotRow.dataset.slotIndex);
                if (hoursState[dayIndex].slots.length > 1) {
                    markFormDirty();
                    hoursState[dayIndex].slots.splice(slotIndex, 1);
                    renderHours();
                }
            });
        });

        container.querySelectorAll("[data-field]").forEach(input => {
            input.addEventListener("input", event => {
                markFormDirty();
                const dayRow = event.target.closest("[data-day-index]");
                const slotRow = event.target.closest("[data-slot-index]");
                const dayIndex = Number(dayRow.dataset.dayIndex);
                const slotIndex = Number(slotRow.dataset.slotIndex);
                hoursState[dayIndex].slots[slotIndex][event.target.dataset.field] = event.target.value;
                syncHoursInput();
            });
        });
    }

    function copyFirstDayToWeek() {
        if (!hoursState[0]) return;
        markFormDirty();
        const monday = JSON.parse(JSON.stringify(hoursState[0]));
        hoursState = hoursState.map(day => ({
            day: day.day,
            closed: monday.closed,
            slots: JSON.parse(JSON.stringify(monday.slots))
        }));
        renderHours();
    }

    // ---- New image file previews (upload itself happens via normal form file inputs) ----

    function createImagePreviewCard(file, title, datasetKey) {
        const card = document.createElement("article");
        card.className = "image-preview-card";
        card.dataset[datasetKey] = "1";

        const image = document.createElement("img");
        image.src = URL.createObjectURL(file);
        image.alt = title;

        const details = document.createElement("div");
        const heading = document.createElement("strong");
        heading.textContent = title;
        const fileName = document.createElement("p");
        fileName.className = "muted";
        fileName.textContent = file.name;

        details.append(heading, fileName);
        card.append(image, details);
        return card;
    }

    function bindImageInputs() {
        const coverInput = document.getElementById("coverImageInput");
        const envInput = document.getElementById("envImageInput");
        const preview = document.getElementById("newImagePreview");
        if (!preview) return;

        if (coverInput) {
            coverInput.addEventListener("change", () => {
                preview.querySelectorAll("[data-new-cover]").forEach(el => el.remove());
                const file = coverInput.files[0];
                if (file) {
                    preview.appendChild(createImagePreviewCard(file, "新封面圖預覽", "newCover"));
                }
            });
        }

        if (envInput) {
            envInput.addEventListener("change", () => {
                preview.querySelectorAll("[data-new-env]").forEach(el => el.remove());
                Array.from(envInput.files).forEach(file => {
                    preview.appendChild(createImagePreviewCard(file, "新環境圖預覽", "newEnv"));
                });
            });
        }
    }

    // ---- AJAX submit ----

    function bindFormSubmit() {
        const form = document.getElementById("restaurantForm");
        if (!form) return;

        form.addEventListener("submit", async event => {
            event.preventDefault();

            const checkedTags = form.querySelectorAll("input[name='SelectedTagIds']:checked");
            if (checkedTags.length === 0) {
                toast("請至少選擇一個標籤。");
                return;
            }

            syncHoursInput();

            const submitButton = form.querySelector("button[type='submit']");
            if (submitButton) {
                submitButton.disabled = true;
            }

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form)
                });

                if (response.status === 403) {
                    toast("無法識別管理員身分，請重新登入後再試。");
                    if (submitButton) {
                        submitButton.disabled = false;
                    }
                    return;
                }

                const text = await response.text();
                let json = null;
                try {
                    json = JSON.parse(text);
                } catch (error) {
                    json = null;
                }

                if (json && json.success) {
                    const idField = form.querySelector("[name='Id']");
                    const isEditMode = Boolean(idField && idField.value);
                    if (isEditMode) {
                        // Edited from a list page (with its own search/filter/sort in the
                        // URL) or from the Details page — either way we never navigated
                        // away to open the modal, so reloading the current URL lands back
                        // exactly where the user was, filters and all.
                        window.location.reload();
                    } else {
                        // The list page is kept in the browser back-forward cache.
                        // Remove the modal before leaving so returning to the filtered
                        // list cannot revive the already-completed create form.
                        closeModal();
                        window.location.href = json.redirectUrl;
                    }
                    return;
                }

                openModalHtml(text, { preserveDirty: true });
            } catch (error) {
                toast("儲存失敗，請稍後再試。");
                if (submitButton) {
                    submitButton.disabled = false;
                }
            }
        });
    }

    function initDistrictCascade() {
        // Must be scoped to the modal form specifically: the Restaurants/Index page
        // underneath the modal has its own City *filter* <select name="City">, and an
        // unscoped querySelector("select[name='City']") would silently grab that one
        // instead (it comes first in document order), leaving this listener attached
        // to the wrong element entirely.
        const citySelect = document.getElementById("citySelect");
        const districtSelect = document.getElementById("districtSelect");
        const dataEl = document.getElementById("cityDistrictsJson");
        if (!citySelect || !districtSelect || !dataEl) {
            return;
        }

        let cityDistrictMap = {};
        try {
            cityDistrictMap = JSON.parse(dataEl.textContent);
        } catch (error) {
            return;
        }

        // The modal is injected into the same page repeatedly via AJAX (never a full
        // page load), so some browsers restore the <select name="City"> value from
        // their own form-autofill memory after this script runs, without firing a
        // "change" event. autocomplete="off" helps but isn't honored everywhere, so
        // force the field back to the server-rendered value before the first populate.
        const serverSelectedCity = citySelect.dataset.selected || "";
        if (citySelect.value !== serverSelectedCity) {
            citySelect.value = serverSelectedCity;
        }

        function populate(selectedDistrict) {
            const districts = cityDistrictMap[citySelect.value] || [];
            const placeholder = citySelect.value ? "請選擇" : "請先選擇縣市";
            districtSelect.innerHTML = `<option value="">${placeholder}</option>` +
                districts.map(d => `<option value="${d}" ${d === selectedDistrict ? "selected" : ""}>${d}</option>`).join("");
        }

        citySelect.addEventListener("change", () => populate(""));
        populate(districtSelect.dataset.selected || "");
    }

    function initFormModal(preserveDirty) {
        if (!document.getElementById("hoursJsonInput")) {
            return;
        }

        // Fresh open (Create/Edit): nothing changed yet. Re-render after a failed
        // save (server-side validation errors): the user's input never made it to
        // the database, so it must stay flagged dirty or closing right after a
        // failed submit would silently discard it without asking.
        formIsDirty = Boolean(preserveDirty);

        hoursState = parseHoursInput();
        renderHours();
        bindImageInputs();
        bindFormSubmit();
        bindDirtyTracking();
        initDistrictCascade();

        const copyButton = document.getElementById("copyFirstDayBtn");
        if (copyButton) {
            copyButton.addEventListener("click", copyFirstDayToWeek);
        }
    }

    // ---- 餐廳一覽：表格 / 卡片檢視切換（純前端顯示形式切換，選擇記在 localStorage） ----

    const VIEW_STORAGE_KEY = "ra-restaurant-view";

    function initViewToggle() {
        const buttons = document.querySelectorAll(".js-view-btn");
        const panels = document.querySelectorAll(".js-view-panel");
        if (!buttons.length || !panels.length) return;

        function applyView(view) {
            buttons.forEach(btn => {
                const active = btn.dataset.view === view;
                btn.classList.toggle("active", active);
                btn.setAttribute("aria-pressed", active ? "true" : "false");
            });
            panels.forEach(panel => {
                panel.hidden = panel.dataset.view !== view;
            });
        }

        buttons.forEach(btn => {
            btn.addEventListener("click", () => {
                const view = btn.dataset.view;
                window.localStorage.setItem(VIEW_STORAGE_KEY, view);
                applyView(view);
            });
        });

        applyView(window.localStorage.getItem(VIEW_STORAGE_KEY) || "table");
    }

    // ---- 通用確認視窗（例如：解除停用餐廳前的二次確認） ----

    function openConfirmModal(options) {
        const root = modalRoot();
        if (!root) return;
        const confirmLabel = options.confirmLabel || "確定";
        const confirmClass = options.danger ? "btn-danger" : "btn-primary";

        // Message/title may come from user-entered data (e.g. a restaurant name), so
        // they're set via textContent below rather than interpolated into this HTML
        // string, to avoid re-introducing an XSS hole through this shared helper.
        root.innerHTML = `
            <div class="ra-modal confirm-modal" role="alertdialog" aria-modal="true">
                <div class="modal-body">
                    <p data-confirm-message></p>
                </div>
                <div class="modal-foot">
                    <button type="button" class="btn" data-confirm="cancel">取消</button>
                    <button type="button" class="btn ${confirmClass}" data-confirm="ok">${confirmLabel}</button>
                </div>
            </div>`;
        root.querySelector(".ra-modal").setAttribute("aria-label", options.title || "確認");
        root.querySelector("[data-confirm-message]").textContent = options.message || "確定要執行這個操作嗎？";
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        bindModalChrome();

        root.querySelector("[data-confirm='cancel']").onclick = () => closeModal();
        root.querySelector("[data-confirm='ok']").onclick = () => {
            closeModal();
            if (typeof options.onConfirm === "function") {
                options.onConfirm();
            }
        };
    }

    // ---- 餐廳標籤一覽：Pinterest 風格拖曳排序 ----

    function initTagReorder(reorderUrl, antiForgeryToken) {
        const grid = document.querySelector(".tag-grid[data-reorderable]");
        if (!grid) return;

        let dragged = null;

        grid.querySelectorAll(".tag-card:not(.inactive)").forEach(card => {
            card.setAttribute("draggable", "true");

            card.addEventListener("dragstart", () => {
                dragged = card;
                card.classList.add("dragging");
            });

            card.addEventListener("dragend", () => {
                card.classList.remove("dragging");
                grid.querySelectorAll(".tag-card").forEach(c => c.classList.remove("drag-over"));
            });

            card.addEventListener("dragover", event => {
                if (!dragged || dragged === card) return;
                event.preventDefault();
                card.classList.add("drag-over");
            });

            card.addEventListener("dragleave", () => {
                card.classList.remove("drag-over");
            });

            card.addEventListener("drop", event => {
                event.preventDefault();
                card.classList.remove("drag-over");
                if (!dragged || dragged === card) return;

                const cards = Array.from(grid.querySelectorAll(".tag-card:not(.inactive)"));
                const draggedIndex = cards.indexOf(dragged);
                const targetIndex = cards.indexOf(card);
                if (draggedIndex < targetIndex) {
                    card.after(dragged);
                } else {
                    card.before(dragged);
                }

                const orderedIds = Array.from(grid.querySelectorAll(".tag-card:not(.inactive)"))
                    .map(c => c.dataset.tagId);

                const body = new URLSearchParams();
                orderedIds.forEach(id => body.append("orderedIds", id));
                body.append("__RequestVerificationToken", antiForgeryToken);

                fetch(reorderUrl, {
                    method: "POST",
                    headers: { "Content-Type": "application/x-www-form-urlencoded" },
                    body: body.toString()
                }).then(res => {
                    if (res.ok) {
                        toast("已更新標籤排序，餐廳列表的標籤顯示順序將同步更新。");
                    } else {
                        toast("排序儲存失敗，請重新整理後再試一次。");
                    }
                }).catch(() => {
                    toast("排序儲存失敗，請重新整理後再試一次。");
                });
            });
        });
    }

    function initFilterDistrictCascade() {
        const citySelect = document.getElementById("filterCitySelect");
        const districtSelect = document.getElementById("filterDistrictSelect");
        const dataEl = document.getElementById("filterCityDistrictsJson");
        if (!citySelect || !districtSelect || !dataEl) {
            return;
        }

        const cityDistrictMap = JSON.parse(dataEl.textContent);

        function populate(selectedDistrict) {
            const city = citySelect.value;
            let optionsHtml;
            if (!city) {
                optionsHtml = Object.keys(cityDistrictMap).map(c => {
                    const opts = cityDistrictMap[c]
                        .map(d => `<option value="${d}" ${d === selectedDistrict ? "selected" : ""}>${d}</option>`)
                        .join("");
                    return `<optgroup label="${c}">${opts}</optgroup>`;
                }).join("");
            } else {
                const districts = cityDistrictMap[city] || [];
                optionsHtml = districts
                    .map(d => `<option value="${d}" ${d === selectedDistrict ? "selected" : ""}>${d}</option>`)
                    .join("");
            }
            districtSelect.innerHTML = `<option value="">全部行政區</option>${optionsHtml}`;
        }

        citySelect.addEventListener("change", () => {
            populate("");
            window.prepareFilterAutoSubmit?.(citySelect.form);
            citySelect.form.requestSubmit();
        });

        populate(districtSelect.dataset.selected || "");
    }

    return {
        openCreateModal,
        openEditModal,
        openImageLightbox,
        initDetailImageGallery,
        closeModal,
        toast,
        bindAjaxContent,
        initFilterDistrictCascade,
        initViewToggle,
        openConfirmModal,
        initTagReorder
    };
})();
