// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// 各模組詳情頁共用的「← 返回」行為：優先回到使用者實際的上一頁（history.back()），
// 只有在沒有同源上一頁可回（例如直接開連結、新分頁打開）時，才 fallback 用 href 導到列表頁。
document.addEventListener('DOMContentLoaded', function () {
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
