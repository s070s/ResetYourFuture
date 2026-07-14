// Applies the classic loadCSS "media=print until loaded" pattern via an external script
// instead of inline onload="" attributes, so the app's CSP doesn't need script-src
// 'unsafe-inline' (SEC-5). Targets the two non-critical stylesheets deferred in App.razor.
(function () {
    document.querySelectorAll('link[media="print"][data-lazy-css]').forEach(function (link) {
        var applyAll = function () {
            link.media = 'all';
        };
        if (link.sheet) {
            // Already fetched/parsed by the time this script ran (e.g. cached).
            applyAll();
        } else {
            link.addEventListener('load', applyAll, { once: true });
        }
    });
})();
