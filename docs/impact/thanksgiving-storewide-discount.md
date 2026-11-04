# Impact: Thanksgiving 5 percent

## Request

offer 5% discount for all the products on the occasion of thanks giving

## Map selection

Touch (behavior changes):

- `.cursor/skills/verify-nopcommerce/features/storefront-home.md` — `home-featured` — featured products on `/` show a price, and a discount on every product changes that price.
- `.cursor/skills/verify-nopcommerce/features/shopping-cart.md` — `cart-read` — `/cart/` shows the unit price and the total for Apple MacBook Pro, and 5% off every product changes that total.

Neighbors (shared entry, behavior stays):

- `.cursor/skills/verify-nopcommerce/features/catalog-search.md` — opened because `/search?q=MacBook` renders the same product card price as the homepage. `search-match` price text can change. `search-header` and `search-empty` still only decide which products match.

Out (do not load these feature files):

- `customer-login.md` — the returning-customer form has no product price.
- `admin-dashboard.md` — the administration home is the welcome panel, not a discount editor.

## Behavior change

| Sub-feature | User sees today | User sees after | Proof to re-run |
| --- | --- | --- | --- |
| `home-featured` | `/` lists Build your own computer at $1,200.00 and Apple MacBook Pro at $1,800.00 in `.actual-price`. | Each featured price is 5% lower while the Thanksgiving discount applies: $1,140.00 and $1,710.00, or the current price plus the existing old-price slot. | `GET /` |
| `home-open` | Title is `Your store. Home page title`. | Title stays. | `GET /` |
| `cart-read` | Two Apple MacBook Pro (`AP_MBP_13`) at $1,800.00 unit price, line total $3,600.00. Subtotal discount row is absent unless a discount is already applied. | The cart total for that line is $3,420.00 while the discount applies. The existing `.order-subtotal-discount` row shows the 5% when the total is reduced there instead of in the unit price. | Add from `/apple-macbook-pro` with `button.add-to-cart-button`, then `GET /cart/` |
| `search-match` | `q=MacBook` shows Apple MacBook Pro at $1,800.00. | The product still matches. Its card price follows the same 5% rule as the homepage. | `GET /search?q=MacBook` |

## Context pack

Load only these ranges for implementation. Each range is enough for the decision named on that line.

| Path | Lines | Load this because |
| --- | --- | --- |
| `.cursor/skills/verify-nopcommerce/features/storefront-home.md` | 5-13 | Homepage proof is the featured product names on `GET /` |
| `.cursor/skills/verify-nopcommerce/features/shopping-cart.md` | 5-16 | Cart proof is SKU `AP_MBP_13`, quantity 2, unit $1,800.00, line $3,600.00 |
| `src/Presentation/Nop.Web/Views/Shared/Components/HomepageProducts/Default.cshtml` | 1-16 | Featured products render through `_ProductBox` |
| `src/Presentation/Nop.Web/Views/Shared/_ProductBox.cshtml` | 91-96 | Homepage and search cards print `ProductPrice.Price` and optional `OldPrice` |
| `src/Presentation/Nop.Web/Views/Product/_ProductPrice.cshtml` | 27-61 | Product page already has a discounted price block beside the regular price |
| `src/Presentation/Nop.Web/Controllers/ShoppingCartController.cs` | 1246-1258 | `GET /cart/` loads the cart model and returns the cart view |
| `src/Presentation/Nop.Web/Views/Shared/_ShoppingCartItems.cshtml` | 140-143 | Cart line prints `item.UnitPrice` |
| `src/Presentation/Nop.Web/Views/Shared/Components/OrderTotals/Default.cshtml` | 5-22 | Cart totals print `SubTotal` and, when set, `SubTotalDiscount` |

## Do not load

- `customer-login.md`, `admin-dashboard.md` — no product price on those screens.
- `src/Presentation/Nop.Web/Controllers/CatalogController.cs` `SearchTermAutoComplete` — suggestions are not the price proof.
- `src/Presentation/Nop.Web/Views/Shared/Components/SearchBox/Default.cshtml` — header search still submits only the keyword.
- Plugins, localization XML, upgrade scripts, and `src/Tests` — none of the handles above are defined there.
- The discount entity editor — the map has no promotions feature, so it is not in this pack.

## Verification

Re-run:

- `home-featured` via `GET /` and confirm both featured prices reflect 5% off.
- `cart-read` via `button.add-to-cart-button` on `/apple-macbook-pro`, then `GET /cart/`. Quantity stays 2. The line is $3,420.00, or the unit price stays $1,800.00 and `.order-subtotal-discount` shows the 5%.
- `search-match` via `GET /search?q=MacBook`. Apple MacBook Pro remains, at the discounted card price.

Leave as-is:

- `search-header` and `search-empty` — matching and the no-results sentence do not depend on price.
- `customer-login` and `admin-dashboard` — their forms and welcome panel do not show this discount.

## Unknowns

- The map does not say whether Thanksgiving is a date range, a named discount, or a coupon the shopper must enter. The cart page has a discount-code box; these ranges do not show it applying itself.
- These ranges do not show the code that fills `ProductPrice.Price`, `PriceWithDiscount`, `UnitPrice`, or `SubTotalDiscount`. Which of those slots moves by 5% is not settled here.
- They do not show whether the 5% stacks with a price that is already discounted.
- Start and end of "Thanksgiving" are not in the map or these ranges.
