# Storefront home

The public homepage shows the store name, featured products, and links into the catalog.

## Sub-features

- `home-open` loads the storefront from `/`.
- `home-featured` shows sample featured products including Build your own computer and Apple MacBook Pro.

## How to get to it (user POV)

- Open the site root `/`.
- Choose the store logo, which links to the homepage.

## Driving it with verify-nop

Preconditions:

- `verify-nop.sh doctor` succeeded for this run.

- **Open home.** Request `GET /`. The title is `Your store. Home page title`. The body contains `Featured products`, `Build your own computer`, and `Apple MacBook Pro`.
- **Open a featured product.** Follow the `Apple MacBook Pro` link. The product URL is `/apple-macbook-pro` and the page contains `Add to cart`.
- **Proof.** Save the homepage HTML and a screenshot of `/` under `/opt/cursor/artifacts/verify-nopcommerce/<run id>/storefront-home/`.

## Gotchas

- Before installation finishes, `/` redirects to `/install`. Doctor fails that state. Do not treat the installer as the homepage.
- Featured products come from the sample catalog. A database created without `InstallSampleData` will not show Apple MacBook Pro.
