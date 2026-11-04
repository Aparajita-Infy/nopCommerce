# Impact: typed price on search

## Request

Let shoppers type a minimum and maximum price on the search page instead of only using the slider.

## Map selection

Touch (behavior changes):

- `.cursor/skills/verify-nopcommerce/features/catalog-search.md` — `search-page`, `search-match`, `search-empty` — keyword search stays; the search page gains typed bounds that narrow the same result list.

Neighbors (shared entry, behavior stays):

- none opened. The header box `#small-searchterms` is an entry of catalog search and this request does not add price fields there.

Out (do not load these feature files):

- `storefront-home.md` — featured products on `/` do not take a price bound.
- `shopping-cart.md` — cart lines are unchanged by how the shopper filtered search.
- `customer-login.md` — login fields are not on the search form.
- `admin-dashboard.md` — the dashboard welcome panel does not render the search filter. Existing catalog settings already store the slider's allowed range; this request does not change that screen.

## Behavior change

| Sub-feature | User sees today | User sees after | Proof to re-run |
| --- | --- | --- | --- |
| `search-page` | `/search/` has `#q` and, when price filtering is on, a slider `#price-range-slider` under `Filter by price`. The slider reloads results with query `price=<from>-<to>`. | The same page also has typed minimum and maximum fields. Submitting them narrows results the same way the slider does. `#q` still submits the keyword. | `GET /search/?q=MacBook` and the same URL with a price bound |
| `search-match` | `q=MacBook` lists Apple MacBook Pro at $1,800.00. | The product remains when the typed range includes 1800 and disappears when the range excludes it. | `/search?q=MacBook` plus `price` |
| `search-empty` | `q=zzznomatchverify` shows `No products were found that matched your criteria.` | A keyword that matches, combined with a price range that matches nothing, shows that same sentence. | `/search?q=MacBook` with a range above every sample price |
| `search-header` | Header `Search` goes to `/search?q=` with the keyword only. | Unchanged. No typed price in the header. | Header search for `MacBook` still lists Apple MacBook Pro |

## Context pack

Load only these ranges for implementation. Each range is enough for the decision named on that line.

| Path | Lines | Load this because |
| --- | --- | --- |
| `.cursor/skills/verify-nopcommerce/features/catalog-search.md` | 7-33 | Sub-feature ids, the two entry points, and the rule that autocomplete is not this feature |
| `src/Presentation/Nop.Web/Views/Catalog/Search.cshtml` | 36-91 | The keyword form (`#q`) the typed prices must submit with |
| `src/Presentation/Nop.Web/Views/Catalog/_CatalogFilters.cshtml` | 10-14 | Search renders the price UI only when `PriceRangeFilter.Enabled` |
| `src/Presentation/Nop.Web/Views/Catalog/_FilterPriceBox.cshtml` | 41-69 | The slider posts `price=<from>-<to>` and then reloads products |
| `src/Presentation/Nop.Web/Controllers/CatalogController.cs` | 364-372 | `GET` search renders the page from `SearchModel` |
| `src/Presentation/Nop.Web/Controllers/CatalogController.cs` | 441-448 | Result refresh posts to `SearchProducts` and returns the product grid |
| `src/Presentation/Nop.Web/Factories/CatalogModelFactory.cs` | 151-180 | `command.Price` is parsed as `from-to` and converted to store currency |
| `src/Presentation/Nop.Web/Factories/CatalogModelFactory.cs` | 1717-1773 | Search applies that range to `priceMin` / `priceMax` when search price filtering is enabled |

## Do not load

- `storefront-home.md`, `shopping-cart.md`, `customer-login.md`, `admin-dashboard.md` — observables above do not move.
- `src/Presentation/Nop.Web/Controllers/CatalogController.cs` `SearchTermAutoComplete` — suggestions ignore the price range; the map treats autocomplete as a different surface.
- `src/Presentation/Nop.Web/Views/Shared/Components/SearchBox/Default.cshtml` — header search stays keyword-only.
- Admin catalog settings, plugins, localization XML, and `src/Tests` — the enabled flag and numeric bounds already exist; this request only changes the shopper control on `/search/`.

## Verification

Re-run:

- `search-page`, `search-match`, and `search-empty` on `/search/`, once with a range that includes $1,800.00 and once with a range that excludes it.
- `search-header` for `MacBook`, expecting Apple MacBook Pro with no price fields in the header.

Leave as-is:

- `storefront-home`, `shopping-cart`, `customer-login`, `admin-dashboard` — their entry points do not gain a price field.

`verify-nop.sh search` checks the keyword and the empty sentence only. It does not send `price`. Extend that journey before calling the new fields verified.

## Unknowns

- Whether typed fields replace the slider or sit beside it. The request says "instead of only using the slider" and does not say to remove it.
- How a blank minimum or maximum should map onto `command.Price`. The parser requires two segments separated by `-`.
- The sample store's `SearchPagePriceRangeFiltering` value was not read from the database. The search screenshot from the verification run showed `Filter by price`, so the control is on for that install.
