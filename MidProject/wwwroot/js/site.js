// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// 各模組詳情頁共用的「← 返回」行為：優先回到使用者實際的上一頁（history.back()），
// 只有在沒有同源上一頁可回（例如直接開連結、新分頁打開）時，才 fallback 用 href 導到列表頁。
document.addEventListener('DOMContentLoaded', function () {
    // GET 表單中的 select 與日期欄位視為列表篩選條件。有實際篩選值時，
    // 統一套用餐廳頁既有的 filter-changed 視覺提示。
    document.querySelectorAll('form[method="get"] select').forEach(function (select) {
        function syncFilterHighlight() {
            var defaultValue = select.options.length > 0 ? select.options[0].value : '';
            select.classList.toggle('filter-changed', select.value !== defaultValue);
        }

        select.addEventListener('change', syncFilterHighlight);
        syncFilterHighlight();
    });

    document.querySelectorAll(
        'form[method="get"] input[type="date"], form[method="get"] input[type="datetime-local"]'
    ).forEach(function (input) {
        function syncDateHighlight() {
            input.classList.toggle('filter-changed', Boolean(input.value));
        }

        input.addEventListener('change', syncDateHighlight);
        syncDateHighlight();
    });

    // 自動篩選前先把關鍵字還原成「伺服器目前已套用的值」，避免使用者只是先輸入文字、
    // 再調整下拉或日期時，尚未按搜尋的文字也被一併送出。
    function prepareFilterAutoSubmit(form) {
        if (!form) {
            return;
        }

        var keywordName = form.dataset.filterKeywordName;
        if (!keywordName) {
            return;
        }

        var keywordInput = form.elements.namedItem(keywordName);
        if (keywordInput) {
            keywordInput.value = form.dataset.appliedKeyword || '';
        }
    }
    window.prepareFilterAutoSubmit = prepareFilterAutoSubmit;

    // 使用事件委派，讓餐廳頁 AJAX 更新後新產生的下拉選單也能維持立即篩選。
    document.addEventListener('change', function (event) {
        var field = event.target.closest('[data-auto-submit]');
        if (!field || !field.form) {
            return;
        }

        prepareFilterAutoSubmit(field.form);
        if (field.form.checkValidity()) {
            field.form.requestSubmit();
        }
    });

    // 日期區間可從任一側開始：
    // 先選開始日期＝查該日以後；先選結束日期＝查該日以前；兩側都有值＝查完整區間。
    // 每次日期完成變更後立即送出，行為與下拉式篩選一致。
    document.querySelectorAll('form[data-date-range-filter]').forEach(function (form) {
        var start = form.querySelector('[data-date-range-start]');
        var end = form.querySelector('[data-date-range-end]');
        var error = form.querySelector('.js-date-range-error');
        if (!start || !end) {
            return;
        }

        function syncDateRange() {
            start.max = end.value || '';
            end.min = start.value || '';

            var invalid = Boolean(start.value && end.value && start.value > end.value);
            start.setCustomValidity(invalid ? '開始日期不可晚於結束日期' : '');
            end.setCustomValidity(invalid ? '結束日期不可早於開始日期' : '');
            if (error) {
                error.textContent = invalid
                    ? '開始日期不可晚於結束日期，結束日期也不可早於開始日期。'
                    : '';
            }
            return !invalid;
        }

        function submitDateFilter() {
            if (syncDateRange() && form.checkValidity()) {
                prepareFilterAutoSubmit(form);
                form.requestSubmit();
            } else {
                form.reportValidity();
            }
        }

        start.addEventListener('input', syncDateRange);
        end.addEventListener('input', syncDateRange);
        start.addEventListener('change', submitDateFilter);
        end.addEventListener('change', submitDateFilter);
        form.addEventListener('submit', function (event) {
            if (!syncDateRange()) {
                event.preventDefault();
                form.reportValidity();
            }
        });
        syncDateRange();
    });

    document.querySelectorAll('.js-back-btn').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            var sameOriginReferrer = document.referrer && new URL(document.referrer).origin === location.origin;
            if (sameOriginReferrer && window.history.length > 1) {
                e.preventDefault();
                history.back();
            }
        });
    });

    // 條件式返回按鈕：列表頁平常不需要返回鍵，但透過其他模組詳情頁的連結（例如餐廳詳情頁的
    // 「查看評論」「查看檢舉」）帶著篩選條件跳轉過來時，需要能一鍵回去。只有在 document.referrer
    // 符合指定來源路徑（data-referrer-pattern）時才顯示，其餘情況（側邊欄導覽、直接打網址等）
    // 維持列表頁原本沒有返回鍵的樣子。
    document.querySelectorAll('.js-back-btn-conditional').forEach(function (btn) {
        var pattern = btn.dataset.referrerPattern;
        if (!pattern || !document.referrer) {
            return;
        }
        try {
            var referrerUrl = new URL(document.referrer);
            if (referrerUrl.origin === location.origin && referrerUrl.pathname.indexOf(pattern) === 0) {
                btn.classList.remove('d-none');
                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    history.back();
                });
            }
        } catch (err) {
            // document.referrer 格式異常就當作沒有來源，維持隱藏
        }
    });
});
