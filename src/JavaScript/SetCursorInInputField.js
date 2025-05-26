(function () {
    var el = document.activeElement;
    if (!el) return;
    if (typeof el.selectionStart === 'number') {
        var len = el.value.length;
        el.setSelectionRange(len, len);
        el.focus();
    }
    else if (el.isContentEditable) {
        var range = document.createRange();
        var sel = window.getSelection();
        range.selectNodeContents(el);
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
        el.focus();
    }
})();