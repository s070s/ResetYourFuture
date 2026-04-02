// Quill.js interop for Blazor WYSIWYG editor component.
// Each editor instance is stored by element ID for multi-editor support.
window.quillInterop = {
    _instances: {},

    // Initialize a Quill editor on the given element.
    init: function (elementId, initialContent) {
        const container = document.getElementById(elementId);
        if (!container) return;

        const quill = new Quill(container, {
            theme: 'snow',
            modules: {
                toolbar: [
                    [{ 'header': [1, 2, 3, false] }],
                    ['bold', 'italic', 'underline', 'strike'],
                    [{ 'color': [] }, { 'background': [] }],
                    [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                    ['blockquote', 'code-block'],
                    ['link', 'image', 'video'],
                    ['clean']
                ]
            },
            placeholder: 'Write content here...'
        });

        if (initialContent) {
            quill.clipboard.dangerouslyPasteHTML(initialContent);
        }

        this._instances[elementId] = quill;
    },

    // Get HTML content from a Quill editor.
    getContent: function (elementId) {
        const quill = this._instances[elementId];
        if (!quill) return '';
        const html = quill.root.innerHTML;
        // Quill returns <p><br></p> for empty content.
        return html === '<p><br></p>' ? '' : html;
    },

    // Set HTML content in a Quill editor.
    setContent: function (elementId, content) {
        const quill = this._instances[elementId];
        if (!quill) return;
        if (!content) {
            quill.setContents([]);
        } else {
            quill.clipboard.dangerouslyPasteHTML(content);
        }
    },

    // Dispose a Quill editor instance.
    dispose: function (elementId) {
        delete this._instances[elementId];
    }
};
