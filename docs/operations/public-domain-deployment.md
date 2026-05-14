# Public domain deployment

## One URL demo

The simplest public demo uses one Render web service. The target URL is:

```text
https://savranpay.onrender.com
```

1. Push the repository to GitHub.
2. In Render, create a Blueprint from the repository.
3. Render creates:
   - `savranpay-postgres`;
   - `savranpay`;
   - `savranpay-worker`.
4. Confirm that the public product URL is:

```text
https://savranpay.onrender.com
```

5. Open:

```text
https://savranpay.onrender.com/health/ready
```

Expected result:

```json
{
  "status": "ready",
  "storage": "postgresql"
}
```

The backend exposes:

- `/health/live`
- `/health/ready`
- `/metrics`
- `/api/v1/auth/login`

The same web service also serves the Vue frontend, so these links work on the same domain:

```text
https://savranpay.onrender.com/
https://savranpay.onrender.com/cabinet/client
https://savranpay.onrender.com/cabinet/admin
```

## Optional Netlify frontend

Netlify remains available as an optional separate frontend. The target public frontend URL is:

```text
https://savranpay.netlify.app
```

1. Create a Netlify site from the same repository.
2. Set the Netlify site name to `savranpay`.
3. Keep the build settings from `netlify.toml`:

```text
base    = frontend/savranpay-web
command = npm run build
publish = dist
```

4. Set `VITE_API_BASE_URL` to:

```text
https://savranpay.onrender.com
```

5. Deploy the site.
6. Open:

```text
https://savranpay.netlify.app
```

If `savranpay.netlify.app` is already taken in Netlify, create the closest available Netlify site name and add a custom domain with the SavranPay name.

## Provider variables

Render backend:

```text
ASPNETCORE_HTTP_PORTS=8080
DISABLE_HTTPS_REDIRECTION=true
DataProtection__KeysPath=/var/lib/savranpay/dataprotection-keys
Cors__AllowedOrigins__0=https://savranpay.onrender.com
Cors__AllowedOrigins__1=https://savranpay.netlify.app
Jwt__RequireAuthorization=true
Jwt__SigningKey=<generated secret>
ConnectionStrings__Postgres=<from Render PostgreSQL>
```

Netlify frontend:

```text
VITE_API_BASE_URL=https://savranpay.onrender.com
```

## Demo credentials

When PostgreSQL starts for the first time, the backend migration and initializer create:

```text
client@savranpay.local / Client123!
support@savranpay.local / Support123!
aml@savranpay.local / Aml123!
fraud@savranpay.local / Fraud123!
admin@savranpay.local / Admin123!
audit@savranpay.local / Audit123!
```

## Production checklist

- Replace generated demo passwords before public use.
- Keep `Jwt__RequireAuthorization=true`.
- Store `Jwt__SigningKey` only in the hosting provider secret storage.
- Replace `LoggingNotificationSender` with real email/SMS/push providers.
- Replace demo HMAC with HSM/KMS/SKZI implementation behind `ICryptoService`.
- Add external alerts from `/health/ready` and `/metrics`.
