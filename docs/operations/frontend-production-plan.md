# План frontend production

Целевой frontend SavranPay должен быть отдельным приложением:

```text
frontend/savranpay-web
```

Технологии:

```text
Vue 3
TypeScript
Vite
npm
```

Целевые команды:

```powershell
npm install
npm run dev
npm run build
npm run preview
```

После перехода на Vite production CSP должна быть ужесточена:

```text
default-src 'self';
script-src 'self';
style-src 'self';
img-src 'self' data:;
connect-src 'self' https://api.savranpay.ru;
font-src 'self';
object-src 'none';
base-uri 'self';
frame-ancestors 'none';
```

Из production CSP нужно убрать:

```text
'unsafe-eval'
'unsafe-inline'
```
