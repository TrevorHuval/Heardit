namespace Heardit.Services
{
    /// <summary>
    /// The baseline response headers every page carries. Kept here, rather than inline in Program.cs,
    /// so the policy can be tested.
    /// </summary>
    public static class SecurityHeaders
    {
        /// <summary>
        /// The CSP permits the Spotify embeds the views use and Google Analytics (the gtag.js script plus its
        /// collection endpoints; the init code is the same-origin wwwroot/js/analytics.js).
        /// script-src has no 'unsafe-inline': every script is a same-origin file, so markup that slips
        /// into a page can't run as script. Keep it that way: no inline &lt;script&gt; blocks or on* handlers
        /// in views (SecurityHeadersTests checks). Styles still allow inline for the SVG/style attributes.
        /// form-action also allows Google: "Continue with Google" posts to this app, which redirects the
        /// form on to accounts.google.com, and browsers apply form-action to that redirect too.
        /// </summary>
        public const string ContentSecurityPolicy =
            "default-src 'self'; " +
            "script-src 'self' https://open.spotify.com https://*.spotifycdn.com https://www.googletagmanager.com; " +
            "style-src 'self' 'unsafe-inline'; " +
            "font-src 'self' data:; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' https://*.spotify.com https://*.spotifycdn.com https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com; " +
            "frame-src https://open.spotify.com https://*.spotify.com https://*.spotifycdn.com; " +
            "object-src 'none'; base-uri 'self'; form-action 'self' https://accounts.google.com; frame-ancestors 'self'";

        /// <summary>The app uses none of these device features; the Spotify player frame gets only what it is granted in markup.</summary>
        public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

        public static void Apply(IHeaderDictionary headers)
        {
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }
    }
}
