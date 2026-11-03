# nopCommerce verification map

This directory is the maintained source for verifying shopper and store-owner behavior. Read the index, then use the matching feature file.

## Baseline preconditions

- Launch with `.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh launch`.
- Use the printed `http://127.0.0.1:<port>`. Do not use port `5000` or database `nopcommerce`.
- The instance database is `nopverify_<run id>` and already contains the sample catalog, including Apple MacBook Pro at `/apple-macbook-pro`.
- Admin for this instance is `verify-admin@example.com` / `Verify_123`.
- Run `verify-nop.sh doctor` and require the homepage title `Your store. Home page title`.
- Drive only the instance this run started.

## Driving conventions

- Start every recipe from the baseline state unless its preconditions say otherwise.
- Prefer ids, ARIA labels, and CSS classes named in the feature file.
- `GET /search?q=` is the header search form's destination. Typing in `#small-searchterms` and choosing `Search` lands on that URL.
- Keep each run's cookie jar separate. A cart or login on one port does not apply to another.
- Leave proof files in `/opt/cursor/artifacts/verify-nopcommerce/<run id>/`.

## Proof and skip reporting

- Capture the action and the resulting page, not only the final screen.
- UI proof includes the response HTML and a screenshot of that URL.
- A cart or login proof includes a second read of `/cart/` or `/admin`.
- Record the feature id and the entry point with the artifacts.
- Report an unreachable entry with the command and the missing precondition.
- Do not mark a skipped entry verified through a different path.

## Feature entry contract

Each feature file has an H1, one opening paragraph, then four H2 sections: `Sub-features`, `How to get to it (user POV)`, `Driving it with verify-nop`, and `Gotchas`.

## Features

- [Storefront home](./storefront-home.md) covers the public homepage and featured products.
- [Catalog search](./catalog-search.md) covers header search, the search page, a match, and no results.
- [Shopping cart](./shopping-cart.md) covers adding Apple MacBook Pro and reading the cart.
- [Customer login](./customer-login.md) covers the returning-customer form.
- [Admin dashboard](./admin-dashboard.md) covers the administration home after login.
