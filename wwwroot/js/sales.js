// Sales page cart.
//
// The cart lives in the browser purely for speed - it is a display aid. On submit only
// product ids and quantities are posted; the server re-reads every price from the database
// and calculates the authoritative total. Nothing here is trusted server-side.
(function () {
    'use strict';

    var STORAGE_KEY = 'candyvan.cart';
    var MAX_QTY = 1000;

    var money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

    var catalogue = {};   // id -> { name, price }
    var cart = [];        // [{ id, qty }]

    var activeCategory = 'all';

    var els = {
        search: document.getElementById('product-search'),
        categoryFilter: document.getElementById('category-filter'),
        noMatch: document.getElementById('no-match'),
        body: document.getElementById('cart-body'),
        wrapper: document.getElementById('cart-wrapper'),
        empty: document.getElementById('cart-empty'),
        total: document.getElementById('cart-total'),
        inputs: document.getElementById('cart-inputs'),
        submit: document.getElementById('complete-sale'),
        form: document.getElementById('complete-sale-form'),
        clear: document.getElementById('clear-cart')
    };

    function readCatalogue() {
        document.querySelectorAll('.add-product').forEach(function (btn) {
            catalogue[btn.dataset.id] = {
                name: btn.dataset.name,
                price: parseFloat(btn.dataset.price)
            };
        });
    }

    function load() {
        var raw = null;
        try {
            raw = window.localStorage.getItem(STORAGE_KEY);
        } catch (e) {
            return; // private mode / storage disabled - start with an empty cart
        }
        if (!raw) {
            return;
        }
        try {
            var parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return;
            }
            // Drop anything no longer on offer (product deactivated or renamed away).
            cart = parsed
                .filter(function (line) { return line && catalogue[line.id]; })
                .map(function (line) {
                    return { id: String(line.id), qty: clampQty(parseInt(line.qty, 10)) };
                })
                .filter(function (line) { return line.qty > 0; });
        } catch (e) {
            cart = [];
        }
    }

    function save() {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(cart));
        } catch (e) {
            /* not fatal - the cart just will not survive a refresh */
        }
    }

    function clampQty(qty) {
        if (isNaN(qty)) {
            return 0;
        }
        return Math.min(Math.max(qty, 0), MAX_QTY);
    }

    function find(id) {
        for (var i = 0; i < cart.length; i++) {
            if (cart[i].id === id) {
                return cart[i];
            }
        }
        return null;
    }

    function addProduct(id) {
        if (!catalogue[id]) {
            return;
        }
        var line = find(id);
        if (line) {
            line.qty = clampQty(line.qty + 1);
        } else {
            cart.push({ id: id, qty: 1 });
        }
        render();
    }

    function changeQty(id, delta) {
        var line = find(id);
        if (!line) {
            return;
        }
        line.qty = clampQty(line.qty + delta);
        if (line.qty <= 0) {
            removeProduct(id);
            return;
        }
        render();
    }

    function setQty(id, value) {
        var line = find(id);
        if (!line) {
            return;
        }
        var qty = clampQty(parseInt(value, 10));
        if (qty <= 0) {
            removeProduct(id);
            return;
        }
        line.qty = qty;
        render();
    }

    function removeProduct(id) {
        cart = cart.filter(function (line) { return line.id !== id; });
        render();
    }

    function render() {
        els.body.innerHTML = '';
        els.inputs.innerHTML = '';

        var total = 0;

        cart.forEach(function (line, index) {
            var product = catalogue[line.id];
            var lineTotal = product.price * line.qty;
            total += lineTotal;

            var row = document.createElement('tr');

            var nameCell = document.createElement('td');
            nameCell.textContent = product.name;
            row.appendChild(nameCell);

            var priceCell = document.createElement('td');
            priceCell.className = 'text-end';
            priceCell.textContent = money.format(product.price);
            row.appendChild(priceCell);

            var qtyCell = document.createElement('td');
            qtyCell.className = 'text-center';
            qtyCell.innerHTML =
                '<div class="btn-group btn-group-sm" role="group" aria-label="Quantity for ' + escapeAttr(product.name) + '">' +
                '<button type="button" class="btn btn-outline-secondary qty-down" data-id="' + escapeAttr(line.id) + '" aria-label="Decrease">&minus;</button>' +
                '<input type="text" inputmode="numeric" class="form-control form-control-sm text-center qty-input" ' +
                'style="max-width:3.25rem" value="' + line.qty + '" data-id="' + escapeAttr(line.id) + '" aria-label="Quantity" />' +
                '<button type="button" class="btn btn-outline-secondary qty-up" data-id="' + escapeAttr(line.id) + '" aria-label="Increase">+</button>' +
                '</div>';
            row.appendChild(qtyCell);

            var totalCell = document.createElement('td');
            totalCell.className = 'text-end fw-semibold';
            totalCell.textContent = money.format(lineTotal);
            row.appendChild(totalCell);

            var removeCell = document.createElement('td');
            removeCell.className = 'text-end';
            removeCell.innerHTML =
                '<button type="button" class="btn btn-sm btn-outline-danger remove-line" data-id="' +
                escapeAttr(line.id) + '" aria-label="Remove ' + escapeAttr(product.name) + '">&times;</button>';
            row.appendChild(removeCell);

            els.body.appendChild(row);

            // Hidden inputs are what actually gets posted: ids and quantities only.
            els.inputs.appendChild(hidden('Items[' + index + '].ProductId', line.id));
            els.inputs.appendChild(hidden('Items[' + index + '].Quantity', line.qty));
        });

        els.total.textContent = money.format(total);

        var hasLines = cart.length > 0;
        els.wrapper.classList.toggle('d-none', !hasLines);
        els.empty.classList.toggle('d-none', hasLines);
        els.clear.classList.toggle('d-none', !hasLines);
        els.submit.disabled = !hasLines;

        save();
    }

    function hidden(name, value) {
        var input = document.createElement('input');
        input.type = 'hidden';
        input.name = name;
        input.value = value;
        return input;
    }

    function escapeAttr(value) {
        return String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    // Search text and the category pills are applied together, so picking "Chocolate"
    // and typing "mars" narrows to chocolate products matching "mars".
    function applyFilters() {
        var needle = els.search ? els.search.value.trim().toLowerCase() : '';
        var visible = 0;

        document.querySelectorAll('.category-group').forEach(function (group) {
            var inCategory = activeCategory === 'all' || group.dataset.category === activeCategory;
            var shown = 0;

            group.querySelectorAll('.product-cell').forEach(function (cell) {
                var match = inCategory && (!needle || cell.dataset.name.indexOf(needle) !== -1);
                cell.classList.toggle('d-none', !match);
                if (match) {
                    shown++;
                }
            });

            // Hide the category heading when none of its products are visible.
            group.classList.toggle('d-none', shown === 0);
            visible += shown;
        });

        if (els.noMatch) {
            els.noMatch.classList.toggle('d-none', visible !== 0);
        }
    }

    function selectCategory(category) {
        activeCategory = category;
        els.categoryFilter.querySelectorAll('.category-pill').forEach(function (pill) {
            var selected = pill.dataset.category === category;
            pill.classList.toggle('btn-primary', selected);
            pill.classList.toggle('btn-outline-primary', !selected);
            pill.setAttribute('aria-pressed', selected ? 'true' : 'false');
        });
        applyFilters();
    }

    function wireEvents() {
        // Delegated so it works across every category group.
        document.addEventListener('click', function (event) {
            var btn = event.target.closest('.add-product');
            if (btn) {
                addProduct(btn.dataset.id);
            }
        });

        if (els.categoryFilter) {
            els.categoryFilter.addEventListener('click', function (event) {
                var pill = event.target.closest('.category-pill');
                if (pill) {
                    selectCategory(pill.dataset.category);
                }
            });
        }

        els.body.addEventListener('click', function (event) {
            var target = event.target.closest('button');
            if (!target) {
                return;
            }
            if (target.classList.contains('qty-up')) {
                changeQty(target.dataset.id, 1);
            } else if (target.classList.contains('qty-down')) {
                changeQty(target.dataset.id, -1);
            } else if (target.classList.contains('remove-line')) {
                removeProduct(target.dataset.id);
            }
        });

        els.body.addEventListener('change', function (event) {
            if (event.target.classList.contains('qty-input')) {
                setQty(event.target.dataset.id, event.target.value);
            }
        });

        // Enter in a quantity box should commit the value, not submit the sale.
        els.body.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && event.target.classList.contains('qty-input')) {
                event.preventDefault();
                setQty(event.target.dataset.id, event.target.value);
            }
        });

        els.clear.addEventListener('click', function () {
            if (cart.length && window.confirm('Clear the current sale?')) {
                cart = [];
                render();
            }
        });

        if (els.search) {
            els.search.addEventListener('input', applyFilters);
        }

        els.form.addEventListener('submit', function (event) {
            if (cart.length === 0) {
                event.preventDefault();
                return;
            }
            if (!window.confirm('Complete this sale for ' + els.total.textContent + '?')) {
                event.preventDefault();
                return;
            }
            // The sale is about to be persisted server-side; drop the local copy so a
            // back-navigation cannot resubmit it.
            try {
                window.localStorage.removeItem(STORAGE_KEY);
            } catch (e) { /* ignore */ }
            els.submit.disabled = true;
            els.submit.textContent = 'Saving…';
        });
    }

    readCatalogue();
    load();
    wireEvents();
    render();
})();
