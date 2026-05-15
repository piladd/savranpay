import { expect, test } from '@playwright/test'
import { createHmac, randomUUID } from 'node:crypto'

const frontendUrl = process.env.SAVRANPAY_FRONTEND_URL ?? 'https://savranpay-5yge.vercel.app'
const backendUrl = process.env.SAVRANPAY_BACKEND_URL ?? 'https://savranpay-production.up.railway.app'

const accounts = {
  customer: {
    role: 'Customer',
    login: process.env.SAVRANPAY_CUSTOMER_LOGIN ?? 'client@savranpay.local',
    password: process.env.SAVRANPAY_CUSTOMER_PASSWORD,
    cabinet: '/cabinet/client',
  },
  support: {
    role: 'SupportOperator',
    login: process.env.SAVRANPAY_SUPPORT_LOGIN ?? 'support@savranpay.local',
    password: process.env.SAVRANPAY_SUPPORT_PASSWORD,
    cabinet: '/cabinet/support',
  },
  aml: {
    role: 'AmlOfficer',
    login: process.env.SAVRANPAY_AML_LOGIN ?? 'aml@savranpay.local',
    password: process.env.SAVRANPAY_AML_PASSWORD,
    cabinet: '/cabinet/aml',
  },
  fraud: {
    role: 'FraudOfficer',
    login: process.env.SAVRANPAY_FRAUD_LOGIN ?? 'fraud@savranpay.local',
    password: process.env.SAVRANPAY_FRAUD_PASSWORD,
    cabinet: '/cabinet/fraud',
  },
  admin: {
    role: 'Admin',
    login: process.env.SAVRANPAY_ADMIN_LOGIN ?? 'admin@savranpay.local',
    password: process.env.SAVRANPAY_ADMIN_PASSWORD,
    cabinet: '/cabinet/admin',
  },
  auditor: {
    role: 'Auditor',
    login: process.env.SAVRANPAY_AUDITOR_LOGIN ?? 'audit@savranpay.local',
    password: process.env.SAVRANPAY_AUDITOR_PASSWORD,
    cabinet: '/cabinet/audit',
  },
} as const

const requiredPassword = (key: keyof typeof accounts) => {
  const password = accounts[key].password
  test.skip(!password, `Set ${accounts[key].role} password in env before running production smoke tests.`)
  return password!
}

async function backendLogin(request: Parameters<typeof test>[1]['request'], key: keyof typeof accounts) {
  const account = accounts[key]
  const response = await request.post(`${backendUrl}/api/v1/auth/login`, {
    data: { login: account.login, password: requiredPassword(key) },
  })
  expect(response.ok()).toBeTruthy()
  return (await response.json()) as { accessToken: string; refreshToken: string }
}

test('backend healthcheck is ready', async ({ request }) => {
  const response = await request.get(`${backendUrl}/health/ready`)
  expect(response.status()).toBe(200)
  expect(await response.json()).toEqual({ status: 'ready', storage: 'postgresql' })
})

test('login, me, refresh and logout work for all roles', async ({ request }) => {
  for (const key of Object.keys(accounts) as Array<keyof typeof accounts>) {
    const login = await backendLogin(request, key)
    const me = await request.get(`${backendUrl}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${login.accessToken}` },
    })
    expect(me.status()).toBe(200)
    expect(await me.json()).toMatchObject({ login: accounts[key].login })

    const refresh = await request.post(`${backendUrl}/api/v1/auth/refresh`, {
      data: { refreshToken: login.refreshToken },
    })
    expect(refresh.status()).toBe(200)

    const logout = await request.post(`${backendUrl}/api/v1/auth/logout`, {
      headers: { Authorization: `Bearer ${login.accessToken}` },
      data: { refreshToken: login.refreshToken },
    })
    expect(logout.ok()).toBeTruthy()
  }
})

test('role navigation exposes only permitted frontend cabinets', async ({ page }) => {
  for (const key of Object.keys(accounts) as Array<keyof typeof accounts>) {
    const account = accounts[key]
    await page.goto(`${frontendUrl}/cabinet/client`)
    await page.getByLabel('Логин').fill(account.login)
    await page.getByLabel('Пароль').fill(requiredPassword(key))
    await page.getByRole('button', { name: 'Войти' }).click()
    await expect(page.getByText(account.role)).toBeVisible()

    await page.goto(`${frontendUrl}${account.cabinet}`)
    await expect(page.getByText(account.role)).toBeVisible()

    if (account.role !== 'Admin') {
      await page.goto(`${frontendUrl}/cabinet/admin`)
      await expect(page.getByText('У текущего пользователя нет роли')).toBeVisible()
    }

    await page.locator('header').getByRole('button', { name: 'Выйти' }).click()
    await page.goto(`${frontendUrl}${account.cabinet}`)
    await expect(page.getByRole('button', { name: 'Войти' })).toBeVisible()
  }
})

test('customer can create a test transfer through backend API', async ({ request }) => {
  const login = await backendLogin(request, 'customer')
  const headers = { Authorization: `Bearer ${login.accessToken}` }
  const dashboard = await (await request.get(`${backendUrl}/api/v1/dashboard`, { headers })).json()
  const fromAccountId = dashboard.accounts[0].id

  const create = await request.post(`${backendUrl}/api/v1/transfers`, {
    headers: {
      ...headers,
      'Idempotency-Key': randomUUID(),
      'X-Request-Id': randomUUID(),
    },
    data: {
      fromAccountId,
      recipient: {
        type: 'Account',
        accountNumber: '40817810000000000999',
        bankBic: '044525225',
        name: 'QA Smoke Recipient',
      },
      amount: { minorUnits: 100, currency: 'RUB' },
      purpose: `QA smoke ${new Date().toISOString()}`,
    },
  })
  expect(create.status()).toBe(202)
})

test('customer transfer can be confirmed when demo signing secret is provided', async ({ request }) => {
  const secret = process.env.SAVRANPAY_DEMO_TRANSFER_SECRET
  test.skip(!secret, 'Set SAVRANPAY_DEMO_TRANSFER_SECRET to run confirmation smoke.')

  const login = await backendLogin(request, 'customer')
  const headers = { Authorization: `Bearer ${login.accessToken}` }
  const dashboard = await (await request.get(`${backendUrl}/api/v1/dashboard`, { headers })).json()
  const create = await request.post(`${backendUrl}/api/v1/transfers`, {
    headers: { ...headers, 'Idempotency-Key': randomUUID(), 'X-Request-Id': randomUUID() },
    data: {
      fromAccountId: dashboard.accounts[0].id,
      recipient: { type: 'Account', accountNumber: '40817810000000000999', bankBic: '044525225', name: 'QA Smoke Recipient' },
      amount: { minorUnits: 100, currency: 'RUB' },
      purpose: `QA smoke confirm ${new Date().toISOString()}`,
    },
  })
  const transfer = await create.json()
  const challenge = await (await request.get(`${backendUrl}/api/v1/transfers/${transfer.transferId}/confirmation-challenge`, { headers })).json()
  const signature = createHmac('sha256', secret).update(challenge.payload).digest('base64')

  const confirm = await request.post(`${backendUrl}/api/v1/transfers/${transfer.transferId}/confirm`, {
    headers: { ...headers, 'X-Request-Id': randomUUID() },
    data: {
      confirmationType: 'TransactionSignature',
      signature,
      nonce: challenge.nonce,
      timestamp: challenge.timestamp,
    },
  })
  expect(confirm.status()).toBe(200)
  expect(await confirm.json()).toMatchObject({ status: 'Settled' })
})
