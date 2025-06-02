(function () {
    var el = document.activeElement;
    if (!el) {
        return {
            value: "",
            placeholder: "",
            x: 0,
            y: 0
        };
    }
    el.focus();

    var rect = el.getBoundingClientRect();

    return {
        value: el.value || "",
        placeholder: el.placeholder || "",
        x: Math.round(rect.left),
        y: Math.round(rect.bottom)
    };
})();