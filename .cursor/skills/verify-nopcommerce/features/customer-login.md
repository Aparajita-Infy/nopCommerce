# Customer login

A returning customer signs in with email and password from the login page.

## Sub-features

- `login-open` shows the returning-customer form.
- `login-submit` signs in the verification admin customer.
- `login-reject` keeps an unknown password on the login page.

## How to get to it (user POV)

- Choose `Log in` in the header.
- Open `/login/`.

## Driving it with verify-nop

Preconditions:

- `verify-nop.sh doctor` succeeded.
- This instance's customer is `verify-admin@example.com` / `Verify_123`.

- **Open the form.** `GET /login/`. The page title contains `Login`, the email field is `#Email`, the password field is `#Password`, and the submit control is `button.login-button` with text `Log in`.
- **Sign in.** Submit those fields with the verification customer. The next page is the account area or the return URL, and the header shows `My account` rather than only `Log in`.
- **Reject a bad password.** Submit `#Email` as `verify-admin@example.com` and `#Password` as `wrong-password`. The response stays on the login form and shows `Login was unsuccessful`.
- **Proof.** Save both HTML responses. The successful response is not the login form. Do not use the shared store's `admin@yourStore.com` account for this instance.

## Gotchas

- The form includes an antiforgery token. Replay the token from the GET.
- Phone login controls exist in the markup and are hidden for the sample store. Fill `#Email` and `#Password`.
- A successful login sets an authentication cookie for this port only.
