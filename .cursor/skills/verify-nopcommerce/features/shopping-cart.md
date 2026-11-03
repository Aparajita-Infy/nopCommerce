# Shopping cart

The cart lets a shopper add a product and see that line on the cart page.

## Sub-features

- `cart-add-box` adds a product from its catalog box.
- `cart-add-page` adds a product from the product page.
- `cart-read` shows the line, quantity, and total on `/cart/`.

## How to get to it (user POV)

- On a product box, choose `Add to cart` (`button.product-box-add-to-cart-button`).
- On the product page, choose `Add to cart` (`button.add-to-cart-button`).
- Open `Shopping cart` in the header, or go to `/cart/`.

## Driving it with verify-nop

Preconditions:

- `verify-nop.sh doctor` succeeded.
- Use a fresh cookie jar for this run so the cart starts empty.
- Apple MacBook Pro is at `/apple-macbook-pro`.

- **Product page entry.** Open `/apple-macbook-pro` and choose `button.add-to-cart-button`. The page reports that the product was added to the shopping cart.
- **Cart page.** Open `/cart/`. The table contains `Apple MacBook Pro`, SKU `AP_MBP_13`, and a line total. Quantity defaults to the product's minimum, which is `2` for this sample product, so the line total is `$3,600.00` when the unit price is `$1,800.00`.
- **Catalog box entry.** From `/search?q=MacBook`, choose `button.product-box-add-to-cart-button` on the Apple MacBook Pro box, then open `/cart/` again. The same SKU is present and the quantity has increased.
- **Proof.** Save the product-page HTML after the click and the `/cart/` HTML. Screenshot `/cart/`. A success banner alone is not proof.

## Gotchas

- Add to cart posts through `AjaxCart` with the antiforgery token already on the page. A bare POST without that cookie and token is rejected. Drive the button, then read `/cart/`.
- The sample laptop's entered quantity is `2`. Assert the cart quantity rather than assuming `1`.
- Gift wrapping on the cart is required before checkout. This feature stops at the cart contents and does not place an order.
