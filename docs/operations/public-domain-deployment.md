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
VITE_DEMO_TRANSFER_SECRET=<training-demo-secret>
```

## Optional Vercel frontend

Vercel can publish only the Vue frontend. The backend must still be deployed on Render first.

Project settings:

```text
Framework Preset: Vite
Root Directory: leave empty
Build Command: cd frontend/savranpay-web && npm run build
Install Command: cd frontend/savranpay-web && npm ci
Output Directory: frontend/savranpay-web/dist
```

Environment variable:

```text
VITE_API_BASE_URL=https://<actual-render-backend-url>
VITE_DEMO_TRANSFER_SECRET=<training-demo-secret>
```

For the current Vercel deployment, set:

```text
VITE_API_BASE_URL=https://savranpay.onrender.com
VITE_DEMO_TRANSFER_SECRET=<same-demo-secret-as-backend-training-stand>
```

If Render gives another service URL, use that exact URL instead. After changing the Vercel environment variable, redeploy the frontend.

## Railway backend and worker

Railway must build this repository with Dockerfiles, not Railpack. Create these services in one Railway project:

- `savranpay` - public backend API service;
- `savranpay-worker` - private background worker service;
- `savranpay-postgres` - Railway PostgreSQL service.

The root `railway.json` is for the backend API service and points Railway to:

```text
src/SavranPay.Api/Dockerfile
```

Create the `savranpay` backend service from the repository root. If Railway still tries Railpack, set this service variable explicitly:

```text
RAILWAY_DOCKERFILE_PATH=src/SavranPay.Api/Dockerfile
```

Backend service `savranpay` variables:

```text
PORT=8080
ASPNETCORE_HTTP_PORTS=8080
DISABLE_HTTPS_REDIRECTION=true
Database__StartupRetrySeconds=90
Jwt__RequireAuthorization=true
Jwt__SigningKey=<generated-secret-at-least-32-bytes>
ConnectionStrings__Postgres=Host=${{savranpay-postgres.PGHOST}};Port=${{savranpay-postgres.PGPORT}};Database=${{savranpay-postgres.PGDATABASE}};Username=${{savranpay-postgres.PGUSER}};Password=${{savranpay-postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true
Cors__AllowedOrigins__0=https://<your-vercel-frontend>.vercel.app
VITE_API_BASE_URL=
VITE_DEMO_TRANSFER_SECRET=<training-demo-secret>
```

The API Dockerfile listens on `0.0.0.0:${PORT:-8080}` and exposes port `8080`. The readiness endpoint stays strict: `/health/ready` returns HTTP 503 when PostgreSQL is unavailable. If the Railway healthcheck reaches 503, check `ConnectionStrings__Postgres` first and confirm that the variable references the `savranpay-postgres` service name exactly.

Expected healthy response:

```json
{
  "status": "ready",
  "storage": "postgresql"
}
```

For `savranpay-worker`, create a second Railway service from the same repository. Railway uses one config file per service, so set the worker service config file path to:

```text
/railway.worker.json
```

If your Railway UI does not expose a config file path field, leave the repo root unchanged and set this service variable instead:

```text
RAILWAY_DOCKERFILE_PATH=src/SavranPay.Workers/Dockerfile
```

Worker service `savranpay-worker` variables:

```text
ConnectionStrings__Postgres=Host=${{savranpay-postgres.PGHOST}};Port=${{savranpay-postgres.PGPORT}};Database=${{savranpay-postgres.PGDATABASE}};Username=${{savranpay-postgres.PGUSER}};Password=${{savranpay-postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true
```

The worker must not have a public domain and must not have a healthcheck path. It has no HTTP listener; it only consumes the PostgreSQL connection and processes outbox messages.

For a Vercel frontend with Railway backend, set Vercel to the public Railway backend URL:

```text
VITE_API_BASE_URL=https://<your-railway-backend>.up.railway.app
VITE_DEMO_TRANSFER_SECRET=<same-training-demo-secret>
```

Then set the backend CORS origin to the exact Vercel domain:

```text
Cors__AllowedOrigins__0=https://<your-vercel-frontend>.vercel.app
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
