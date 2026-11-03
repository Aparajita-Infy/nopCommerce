# Admin dashboard

The administration home shows store setup guidance after a store owner signs in.

## Sub-features

- `admin-open` loads `/admin` for the signed-in owner.
- `admin-welcome` shows the dashboard heading and the setup steps.

## How to get to it (user POV)

- Sign in, then choose `Administration` or open `/admin`.
- Open `/admin` directly. An anonymous session is sent to the login page and returned to admin after a successful login.

## Driving it with verify-nop

Preconditions:

- `verify-nop.sh doctor` succeeded.
- Complete [Customer login](./customer-login.md) in the same cookie jar, or submit the login form with return URL `/admin`.

- **Open dashboard.** `GET /admin` with the authentication cookie. The title contains `Dashboard`, the main heading is `Dashboard`, and the body contains `Welcome to your store!`.
- **See navigation.** The admin menu contains `Catalog`, `Sales`, `Customers`, and `Configuration`.
- **Proof.** Save the `/admin` HTML and a screenshot. A login page in that capture means the owner is not signed in.

## Gotchas

- `/admin` on a different port does not reuse a cookie from port `5000`.
- The first admin request can take several seconds while views compile. Wait for the `Dashboard` heading.
- Sample news blocks under the welcome panel load from nopCommerce's public news feed. A news-feed failure does not fail the dashboard when `Welcome to your store!` is visible.
