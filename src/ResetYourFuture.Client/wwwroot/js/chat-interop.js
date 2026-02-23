window.chatInterop = {
    initTopResize: function (handle, textarea) {
        if (!handle || !textarea) return;

        if (handle._resizeCleanup) handle._resizeCleanup();

        let startY, startHeight;

        function onDown(e) {
            startY = e.clientY;
            startHeight = parseInt(getComputedStyle(textarea).height, 10);
            document.addEventListener('pointermove', onMove);
            document.addEventListener('pointerup', onUp);
            e.preventDefault();
        }

        function onMove(e) {
            // Dragging up (negative delta) = taller
            const delta = startY - e.clientY;
            const newHeight = Math.min(300, Math.max(38, startHeight + delta));
            textarea.style.height = newHeight + 'px';
        }

        function onUp() {
            document.removeEventListener('pointermove', onMove);
            document.removeEventListener('pointerup', onUp);
        }

        handle.addEventListener('pointerdown', onDown);

        handle._resizeCleanup = function () {
            handle.removeEventListener('pointerdown', onDown);
            document.removeEventListener('pointermove', onMove);
            document.removeEventListener('pointerup', onUp);
        };
    }
};
