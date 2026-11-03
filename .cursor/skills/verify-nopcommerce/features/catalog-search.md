# Catalog search

Search lets a shopper find products by name and see a clear empty state when nothing matches.

## Sub-features

- `search-header` submits the header search box.
- `search-page` repeats a query on the search page.
- `search-match` shows Apple MacBook Pro for `MacBook`.
- `search-empty` shows the no-results sentence for an unmatched term.

## How to get to it (user POV)

- Type in the header box labeled `Search store` (`#small-searchterms`) and choose the `Search` button (`.search-box-button`).
- Open `/search/` and submit the `Search term` field (`#q`).

## Driving it with verify-nop

Preconditions:

- `verify-nop.sh doctor` printed a homepage title containing `Home page`.
- The sample catalog is installed. Apple MacBook Pro is published.

- **Header entry.** Choose `Search` after typing `MacBook` in `#small-searchterms`. The browser goes to `/search?q=MacBook`. Run `.cursor/skills/verify-nopcommerce/scripts/verify-nop.sh search MacBook`. HTTP 200, `#q` is present, and the body contains `Apple MacBook Pro`.
- **Search page entry.** Open `/search?q=MacBook` directly. The same helper request is that entry. The heading text is `Search` and the product link points at `/apple-macbook-pro`.
- **Empty state.** Search for `zzznomatchverify`. The helper also requests `/search?q=zzznomatchverify`. The body contains `No products were found that matched your criteria.` and does not contain `Apple MacBook Pro`.
- **Proof.** The helper writes `catalog-search/response.html`, `catalog-search/empty.html`, `catalog-search/headers.txt`, `catalog-search/proof.txt`, and `catalog-search/page.png` under `/opt/cursor/artifacts/verify-nopcommerce/<run id>/`.

## Gotchas

- The header form cancels the default submit and redirects with JavaScript. An empty header query alerts `Please enter some search keyword` and stays on the page. The `/search/` page accepts an empty `q` and shows the no-results sentence.
- `MacBook` matches because the sample name is `Apple MacBook Pro`. A run without sample data fails this journey; record that in the failure instead of changing the query.
- Search results are HTML, not the autocomplete endpoint `catalog/searchtermautocomplete`. Autocomplete is a separate suggestion list and does not prove this feature.
