# PaneShift website

The landing page lives in `site/`. It uses plain HTML, CSS and JavaScript, local fonts and existing project artwork/screenshots. There are no build dependencies, analytics, cookies, external font requests or embedded third-party widgets. Downloads remain on GitHub Releases.

Run `./scripts/Build-Site.ps1` with PowerShell 7 to assemble `artifacts/site/`. Serve this folder with any local static HTTP server. The same script runs in `.github/workflows/pages.yml`; only explicitly copied public files are uploaded. GitHub Pages must use **GitHub Actions** as its publishing source.

The initial public URL is `https://ken5998.github.io/PaneShift/`. Relative asset paths work both there and at a custom-domain root. When a new app release ships, update the version text, release links, and versioned installer/ZIP links in `site/index.html` together. The page intentionally links to a specific existing release rather than constructing a future filename under `/latest/download/`.

## Custom domain

Planned hostname: `paneshift.ksmvc.ch`.

1. Add that exact hostname under the repository's Settings → Pages → Custom domain before changing DNS. Verifying domain ownership in the account's Pages settings is recommended.
2. At the DNS provider for `ksmvc.ch`, add a **CNAME** named `paneshift` targeting `ken5998.github.io` (no scheme or repository path).
   On Cloudflare, use **DNS only** while GitHub verifies the hostname and issues its certificate; leave TTL on Auto.
3. Wait for GitHub's DNS check and HTTPS certificate to succeed, then enable **Enforce HTTPS**.
4. Update the Open Graph image URL in `site/index.html` and the website links once the custom hostname is serving successfully.

With an Actions-based Pages deployment, the custom domain is configured in repository settings; a `CNAME` file in the build is not used. See [GitHub's domain instructions](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site).
