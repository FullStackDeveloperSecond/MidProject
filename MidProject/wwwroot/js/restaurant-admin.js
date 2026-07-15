/*
  Kefan 餐廳模組（Restaurants / Tags）前端行為。
  負責：新增/編輯餐廳的 AJAX 彈出視窗、營業時間動態表單、標籤 pill、
  圖片選擇預覽、詳細頁圖片燈箱。
*/
const RestaurantAdmin = (() => {
    const DAY_LABELS = ["一", "二", "三", "四", "五", "六", "日"];
    let hoursState = [];

    function modalRoot() {
        return document.getElementById("modalRoot");
    }

    function openModalHtml(html) {
        const root = modalRoot();
        if (!root) return;
        root.innerHTML = html;
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        bindModalChrome();
        initFormModal();
    }

    function closeModal() {
        const root = modalRoot();
        if (!root) return;
        root.classList.remove("open");
        root.setAttribute("aria-hidden", "true");
        root.innerHTML = "";
    }

    function bindModalChrome() {
        const root = modalRoot();
        root.querySelectorAll("[data-action='close-modal']").forEach(btn => {
            btn.addEventListener("click", closeModal);
        });
        root.addEventListener("click", event => {
            if (event.target === root) closeModal();
        });
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

    function openImageLightbox(src) {
        const root = modalRoot();
        if (!root) return;
        root.innerHTML = `
            <div class="ra-modal lightbox-modal" role="dialog" aria-modal="true" aria-label="圖片檢視">
                <div class="modal-head">
                    <h2>圖片檢視</h2>
                    <button type="button" class="btn btn-icon" data-action="close-modal" aria-label="關閉">×</button>
                </div>
                <div class="lightbox-body"><img class="lightbox-image" src="${src}" alt="圖片"></div>
            </div>`;
        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        bindModalChrome();
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
                    hoursState[dayIndex].slots.splice(slotIndex, 1);
                    renderHours();
                }
            });
        });

        container.querySelectorAll("[data-field]").forEach(input => {
            input.addEventListener("input", event => {
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
        const monday = JSON.parse(JSON.stringify(hoursState[0]));
        hoursState = hoursState.map(day => ({
            day: day.day,
            closed: monday.closed,
            slots: JSON.parse(JSON.stringify(monday.slots))
        }));
        renderHours();
    }

    // ---- New image file previews (upload itself happens via normal form file inputs) ----

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
                    const card = document.createElement("article");
                    card.className = "image-preview-card";
                    card.dataset.newCover = "1";
                    card.innerHTML = `<img src="${URL.createObjectURL(file)}" alt="新封面圖預覽"><div><strong>新封面圖預覽</strong><p class="muted">${file.name}</p></div>`;
                    preview.appendChild(card);
                }
            });
        }

        if (envInput) {
            envInput.addEventListener("change", () => {
                preview.querySelectorAll("[data-new-env]").forEach(el => el.remove());
                Array.from(envInput.files).forEach(file => {
                    const card = document.createElement("article");
                    card.className = "image-preview-card";
                    card.dataset.newEnv = "1";
                    card.innerHTML = `<img src="${URL.createObjectURL(file)}" alt="新環境圖預覽"><div><strong>新環境圖預覽</strong><p class="muted">${file.name}</p></div>`;
                    preview.appendChild(card);
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

                const text = await response.text();
                let json = null;
                try {
                    json = JSON.parse(text);
                } catch (error) {
                    json = null;
                }

                if (json && json.success) {
                    window.location.href = json.redirectUrl;
                    return;
                }

                openModalHtml(text);
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

    function initFormModal() {
        if (!document.getElementById("hoursJsonInput")) {
            return;
        }

        hoursState = parseHoursInput();
        renderHours();
        bindImageInputs();
        bindFormSubmit();
        initDistrictCascade();

        const copyButton = document.getElementById("copyFirstDayBtn");
        if (copyButton) {
            copyButton.addEventListener("click", copyFirstDayToWeek);
        }
    }

    return {
        openCreateModal,
        openEditModal,
        openImageLightbox,
        closeModal,
        toast
    };
})();
