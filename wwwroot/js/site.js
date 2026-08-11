// Site-wide behaviour.
(function () {
    'use strict';

    // Any button carrying data-confirm asks before submitting its form.
    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('[data-confirm]');
        if (trigger && !window.confirm(trigger.dataset.confirm)) {
            event.preventDefault();
        }
    });
})();
