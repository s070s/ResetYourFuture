// Focus trap + focus restore for ConfirmModal/FormModal (WCAG 2.4.3). Called once when a
// dialog opens; needs no matching "close" call because both behaviors clean themselves up:
// the Tab-trap listener is bound to the dialog element, which Blazor removes from the DOM on
// close, and the MutationObserver disconnects itself the moment it sees that removal.
window.modalInterop = {
    activate: function (dialogEl) {
        if (!dialogEl) return;

        const trigger = document.activeElement;
        const focusableSelector =
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

        function getFocusable() {
            return Array.prototype.slice.call(dialogEl.querySelectorAll(focusableSelector))
                .filter(function (el) { return el.offsetParent !== null; });
        }

        dialogEl.addEventListener('keydown', function (e) {
            if (e.key !== 'Tab') return;
            const focusable = getFocusable();
            if (focusable.length === 0) return;
            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        });

        const observer = new MutationObserver(function () {
            if (!document.body.contains(dialogEl)) {
                observer.disconnect();
                if (trigger && typeof trigger.focus === 'function' && document.body.contains(trigger)) {
                    trigger.focus();
                }
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }
};
