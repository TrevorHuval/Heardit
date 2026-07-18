// Heardit client behaviour. Loaded on every page; each initializer is a no-op
// unless its markup is present. No external dependencies (CSP: script-src 'self').

(function () {
    'use strict';

    var STEPS = 10;

    // ----- Rating level meter -------------------------------------------------
    // A discrete 1–10 control rendered as rising green bars (an "turn it up"
    // level, not a clipping meter). Drives a hidden <input> and, optionally,
    // a readout and a submit button that stays disabled until a rating is set.
    function initMeter(meter) {
        var input = document.getElementById(meter.getAttribute('data-input'));
        if (!input) return;
        var readout = document.getElementById(meter.getAttribute('data-output'));
        var submitId = meter.getAttribute('data-submit');
        var submit = submitId ? document.getElementById(submitId) : null;

        var initial = parseFloat(input.value);
        var value = isNaN(initial) ? 0 : Math.min(STEPS, Math.max(0, Math.round(initial)));

        var bars = [];
        for (var i = 0; i < STEPS; i++) {
            var bar = document.createElement('span');
            bar.className = 'bar';
            bar.style.height = (38 + i * (62 / (STEPS - 1))) + '%';
            meter.appendChild(bar);
            bars.push(bar);
        }

        function paint(v, preview) {
            for (var i = 0; i < STEPS; i++) {
                var n = i + 1;
                bars[i].classList.toggle('on', n <= v);
                bars[i].classList.toggle('hot', v > 0 && n <= v && n >= v - 1);
                bars[i].classList.toggle('preview', preview != null && n <= preview && n > v);
            }
        }

        function commit(v) {
            value = v;
            input.value = v > 0 ? v : '';
            if (readout) {
                readout.textContent = v > 0 ? (v + ' / 10') : 'Not rated';
                readout.classList.toggle('is-rated', v > 0);
            }
            meter.setAttribute('aria-valuenow', v);
            meter.setAttribute('aria-valuetext', v > 0 ? (v + ' out of 10') : 'Not rated');
            if (submit) submit.disabled = v < 1;
            paint(v);
        }

        function fromClientX(clientX) {
            var rect = meter.getBoundingClientRect();
            return Math.max(1, Math.min(STEPS, Math.ceil((clientX - rect.left) / rect.width * STEPS)));
        }

        meter.addEventListener('click', function (e) { commit(fromClientX(e.clientX)); });
        meter.addEventListener('mousemove', function (e) { paint(value, fromClientX(e.clientX)); });
        meter.addEventListener('mouseleave', function () { paint(value); });
        meter.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowRight' || e.key === 'ArrowUp') { commit(Math.min(STEPS, value + 1)); e.preventDefault(); }
            else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') { commit(Math.max(1, value - 1)); e.preventDefault(); }
            else if (e.key === 'Home') { commit(1); e.preventDefault(); }
            else if (e.key === 'End') { commit(STEPS); e.preventDefault(); }
        });

        commit(value);
    }

    // ----- Edit-your-review toggle -------------------------------------------
    function initEditToggle() {
        var btn = document.querySelector('[data-edit-review]');
        var composer = document.getElementById('composer');
        var summary = document.getElementById('yourReview');
        if (!btn || !composer) return;

        btn.addEventListener('click', function () {
            composer.hidden = false;
            if (summary) summary.hidden = true;
            composer.scrollIntoView({ behavior: 'smooth', block: 'center' });
            var meter = composer.querySelector('.js-rating-meter');
            if (meter) meter.focus();
        });

        var cancel = composer.querySelector('[data-cancel-edit]');
        if (cancel) cancel.addEventListener('click', function () {
            composer.hidden = true;
            if (summary) summary.hidden = false;
        });
    }

    // ----- Client-side review sort -------------------------------------------
    function initSort() {
        var group = document.querySelector('[data-sort-group]');
        var list = document.getElementById('reviewList');
        if (!group || !list) return;

        group.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-sort]');
            if (!btn) return;
            var mode = btn.getAttribute('data-sort');

            group.querySelectorAll('[data-sort]').forEach(function (b) {
                var active = b === btn;
                b.classList.toggle('active', active);
                b.setAttribute('aria-pressed', active ? 'true' : 'false');
            });

            var items = Array.prototype.slice.call(list.querySelectorAll('.review'));
            items.sort(function (a, b) {
                if (mode === 'high') return parseFloat(b.dataset.rating) - parseFloat(a.dataset.rating);
                return parseFloat(b.dataset.created) - parseFloat(a.dataset.created); // newest
            });
            items.forEach(function (el) { list.appendChild(el); });
        });
    }

    // ----- Dismissible flash message -----------------------------------------
    function initFlash() {
        var flash = document.querySelector('[data-flash]');
        if (!flash) return;
        var close = flash.querySelector('[data-flash-close]');
        if (close) close.addEventListener('click', function () { flash.remove(); });
        window.setTimeout(function () {
            flash.classList.add('is-leaving');
            window.setTimeout(function () { flash.remove(); }, 300);
        }, 4000);
    }

    document.querySelectorAll('.js-rating-meter').forEach(initMeter);
    initEditToggle();
    initSort();
    initFlash();
})();
