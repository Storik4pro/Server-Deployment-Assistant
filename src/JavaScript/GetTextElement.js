// Only for compatibility. Avoid using it
(function () {

    var json = {};
    var isText = false;
    var activeElement = document.activeElement;
    if (activeElement) {
        if (activeElement.tagName.toLowerCase() === 'textarea') {
            isText = true;
        } else {
            if (activeElement.tagName.toLowerCase() === 'input') {
                if (activeElement.hasAttribute('type')) {
                    var inputType = activeElement.getAttribute('type').toLowerCase();
                    if (inputType === 'text' || inputType === 'email' || inputType === 'password' || inputType === 'tel' || inputType === 'number' || inputType === 'range' || inputType === 'search' || inputType === 'url') {
                        isText = true;
                    }
                }
            }
        }
    }
    if (isText) {

    }

    json.isText = isText;
    json.text = document.activeElement.value;

    //return isText;
    return JSON.stringify(json);
})();