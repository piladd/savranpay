# Public domain deployment

## Backend

The repository contains `render.yaml` for a Render Blueprint:

1. Push the repository to GitHub.
2. In Render, create a Blueprint from the repository.
3. Render creates:
   - `savranpay-postgres`;
   - `savranpay-backend`;
   - `savranpay-worker`.
4. After deployment, copy the backend URL, for example:

```text
https://savranpay-backend.onrender.com
```

The backend exposes:

- `/health/live`
- `/health/ready`
- `/metrics`
- `/api/v1/auth/login`

## Frontend

The repository contains `netlify.toml` for Netlify:

1. Create a Netlify site from the same repository.
2. Set `VITE_API_BASE_URL` to the public backend URL.
3. Set the site domain to `savranpay.netlify.app` or connect a custom domain.
4. Deploy the site.

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
