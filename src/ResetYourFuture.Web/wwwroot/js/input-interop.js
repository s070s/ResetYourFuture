// PERF-6: read/clear an input's value on demand so message/assistant inputs don't have to
// two-way bind every keystroke through the SignalR circuit. The value is read only when the
// user actually sends (Enter or the Send button).
window.inputInterop = {
    read: function (el) {
        return el ? el.value : '';
    },
    clear: function (el) {
        if (el) {
            el.value = '';
            el.style.height = '';
        }
    }
};
