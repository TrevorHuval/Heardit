// Google Analytics 4 bootstrap. A same-origin file rather than an inline snippet, so the CSP never
// needs 'unsafe-inline' for it. The layout only emits this tag when a measurement id is configured,
// and passes it as data-ga-id.
(function () {
    'use strict';

    var tag = document.currentScript;
    var id = tag && tag.getAttribute('data-ga-id');
    if (!id || !/^G-[A-Z0-9]{6,12}$/.test(id)) return;

    // Respect Do Not Track: load nothing and report nothing.
    if (navigator.doNotTrack === '1') return;

    var script = document.createElement('script');
    script.async = true;
    script.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(id);
    document.head.appendChild(script);

    window.dataLayer = window.dataLayer || [];
    window.gtag = function () { window.dataLayer.push(arguments); };
    window.gtag('js', new Date());

    // Account links carry one-time codes (email confirmation, password reset) in the query string;
    // report those pages by path only so the codes never reach Google.
    var location = window.location.href;
    if (/\/Identity\//i.test(window.location.pathname)) {
        location = window.location.origin + window.location.pathname;
    }

    // The default page_view stays on: every navigation here is a full page load. Signals and ad
    // personalization stay off.
    window.gtag('config', id, {
        page_location: location,
        allow_google_signals: false,
        allow_ad_personalization_signals: false
    });
    window.gaEnabled = true;
})();
