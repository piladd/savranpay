# Production smoke e2e

Install Playwright before the first run:

```bash
npm install
npx playwright install chromium
```

Run:

```bash
SAVRANPAY_FRONTEND_URL=https://savranpay-5yge.vercel.app \
SAVRANPAY_BACKEND_URL=https://savranpay-production.up.railway.app \
SAVRANPAY_CUSTOMER_PASSWORD='...' \
SAVRANPAY_SUPPORT_PASSWORD='...' \
SAVRANPAY_AML_PASSWORD='...' \
SAVRANPAY_FRAUD_PASSWORD='...' \
SAVRANPAY_ADMIN_PASSWORD='...' \
SAVRANPAY_AUDITOR_PASSWORD='...' \
npx playwright test tests/e2e/production-smoke.spec.ts
```

The suite includes a production API binding check. It fails if browser requests go to
`https://savranpay-5yge.vercel.app/api/v1/*` instead of the configured Railway backend.

Optional confirmation flow:

```bash
SAVRANPAY_DEMO_TRANSFER_SECRET='...' npx playwright test tests/e2e/production-smoke.spec.ts -g confirm
```

Do not run destructive admin actions against shared production seed users.
