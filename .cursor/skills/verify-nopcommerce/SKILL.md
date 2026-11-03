---
name: verify-nopcommerce
description: Drive the nopCommerce public store and admin site over HTTP and a browser, on an isolated port and database. Use when proving storefront search, cart, login, homepage, or admin behavior.
---

# Verify nopCommerce

nopCommerce here is an ASP.NET Core store. Shoppers use the public site. Store owners use `/admin`. This skill starts a disposable store with the sample catalog, drives it, and keeps proof after teardown. Do not drive the shared development store on port `5000` or the `nopcommerce` database.

## Launch

From the repository root:

```bash
chmod +x .cursor/skills/verify-nopcommerce/scripts/verify-nop.sh
.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh launch
```

Launch requires PostgreSQL already accepting connections, `dotnet` on `PATH`, and `src/Presentation/Nop.Web/bin/Debug/net10.0/Nop.Web.dll`. Build that dll with `dotnet build src/NopCommerce.sln` when it is missing. The script does not install those tools.

What launch does:

1. Picks a free port from `5210` upward. It never binds `5000`.
2. Creates database `nopverify_<run id>` owned by role `nopcommerce` (password `nopCommerce_db_password`, the same local password as `docker-compose.yml`) and enables `citext` and `pgcrypto`.
3. Builds a private content root under `/tmp/nop-verify-<run id>/site`. `App_Data`, logs, and image upload/thumb folders are real directories. Source, views, plugins, and the rest of `wwwroot` are symlinks. The shared `App_Data/appsettings.json` is not copied.
4. Starts `Nop.Web.dll` with `ASPNETCORE_CONTENTROOT` set to that site and waits until `http://127.0.0.1:<port>/install` shows the title `nopCommerce installation`.
5. Submits the installer with sample data, country `US-en-US`, and admin `verify-admin@example.com` / `Verify_123`. Newsletter signup stays off.
6. Calls `/install/restartapplication`, which stops the process, then starts the dll again so the new `App_Data/appsettings.json` is loaded.
7. Ready means `GET /` returns HTTP 200 and the HTML title contains `Home page`. A database that only answers `pg_isready` is not ready.

The command prints `state.env` values and `ready http://127.0.0.1:<port>`. It points `/tmp/nop-verify-current` at the run directory. Two runs can exist together when each has its own `NOP_VERIFY_RUN_ID`, port, and database.

Teardown is `cleanup` below. Proof stays under `/opt/cursor/artifacts/verify-nopcommerce/<run id>/`.

## Doctor

```bash
.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh doctor
```

Doctor reads `/tmp/nop-verify-current` unless `NOP_VERIFY_RUN_ID` is set. It passes only when all of these are true:

- The recorded pid is alive and is the process this run started.
- That run's port is listening, and it is not `5000`.
- `GET /` is HTTP 200 with a title containing `Home page` and not `installation`.
- `nopverify_<run id>` has a `"Product"` table.

Run doctor before a drive whenever a page looks like the installer, a connection error, or another run's store.

## Drive

Use the URL from `state.env`. Prefer these stable handles over coordinates:

| Surface | Handle |
| --- | --- |
| Header search | `#small-searchterms` (`aria-label="Search store"`), submit `.search-box-button` (`Search`) |
| Search results | `GET /search?q=<terms>`, field `#q`, heading `Search` |
| Product box add to cart | `button.product-box-add-to-cart-button` |
| Product page add to cart | `button.add-to-cart-button` (`#add-to-cart-button-<productId>`) |
| Cart | `GET /cart/`, table `.cart` |
| Login | `GET /login/`, `#Email`, `#Password`, `button.login-button` (`Log in`) |
| Admin | `GET /admin` after login |

The scripted search drive is:

```bash
.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh search MacBook
```

That requests `/search?q=MacBook` and `/search?q=zzznomatchverify`, checks the sample product and the no-results sentence, and saves a headless Chrome screenshot when Chrome is installed. Other features are driven from `features/`. A path that is not listed there is not verified by visiting a different path.

Sample-catalog product page for the laptop is `/apple-macbook-pro`. Login for this instance is `verify-admin@example.com` / `Verify_123`.

## Evidence

Write proof to `/opt/cursor/artifacts/verify-nopcommerce/<run id>/`. Record the feature id, the entry point, the action, and the resulting state.

- HTTP proof is the response body, status, and final URL.
- UI proof is a screenshot of the same URL after the action.
- Mutation proof reads the outcome back from a second page or from the verification database (`SELECT` only). Do not treat a toast as the only proof.
- Exercise the storefront or admin UI. Do not call internal test setters or `/addproducttocart` unless the feature file names that route as the control the button uses.
- External payment, tax, and shipping providers stay at their sample-store defaults. Do not add live credentials to make a gateway succeed.

`search` writes `catalog-search/response.html`, `catalog-search/empty.html`, `catalog-search/headers.txt`, `catalog-search/proof.txt`, and `catalog-search/page.png` when Chrome can take the screenshot.

## Cleanup

```bash
NOP_VERIFY_RUN_ID=<run id> .cursor/skills/verify-nopcommerce/scripts/verify-nop.sh cleanup
```

Cleanup kills the pid recorded for that run (and its process group), drops only `nopverify_<run id>`, and deletes `/tmp/nop-verify-<run id>`. It does not delete `/opt/cursor/artifacts/verify-nopcommerce/<run id>/`. It does not stop PostgreSQL, and it does not touch port `5000` or the `nopcommerce` database.

## Helpers

`.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh` is the only helper. Subcommands are `launch`, `doctor`, `search [query]`, and `cleanup`.
