const users = [
  user('demo-client', 'client@savranpay.local', 'Client123!', 'Иван Петров', ['Customer'], 'demo-customer'),
  user('demo-support', 'support@savranpay.local', 'Support123!', 'Support Operator', ['SupportOperator']),
  user('demo-aml', 'aml@savranpay.local', 'Aml123!', 'AML Officer', ['AmlOfficer']),
  user('demo-fraud', 'fraud@savranpay.local', 'Fraud123!', 'Fraud Officer', ['FraudOfficer']),
  user('demo-admin', 'admin@savranpay.local', 'Admin123!', 'Administrator', ['Admin']),
  user('demo-audit', 'audit@savranpay.local', 'Audit123!', 'Auditor', ['Auditor']),
]

const demoAccount = {
  id: 'demo-account-1',
  customerId: 'demo-customer',
  number: '40817810000000000001',
  maskedNumber: '4081 **** **** 0001',
  status: 'Active',
  availableBalance: { minorUnits: 125000000, currency: 'RUB' },
  reservedBalance: { minorUnits: 0, currency: 'RUB' },
}

const seededTransfers = [
  {
    id: 'demo-transfer-1',
    customerId: 'demo-customer',
    fromAccountId: demoAccount.id,
    recipient: {
      type: 'Account',
      accountNumber: '40817810000000000999',
      bankBic: '044525225',
      name: 'Анна Смирнова',
    },
    amount: { minorUnits: 1850000, currency: 'RUB' },
    purpose: 'Оплата по счету',
    status: 'PendingClientConfirmation',
    createdAt: '2026-05-14T12:30:00.000Z',
    updatedAt: '2026-05-14T12:31:00.000Z',
  },
  {
    id: 'demo-transfer-2',
    customerId: 'demo-customer',
    fromAccountId: demoAccount.id,
    recipient: {
      type: 'Account',
      accountNumber: '40817810000000000888',
      bankBic: '044525225',
      name: 'Петр Иванов',
    },
    amount: { minorUnits: 80000, currency: 'RUB' },
    purpose: 'Возврат долга',
    status: 'Settled',
    createdAt: '2026-05-14T08:20:00.000Z',
    updatedAt: '2026-05-14T08:21:00.000Z',
  },
  {
    id: 'demo-transfer-3',
    customerId: 'demo-customer',
    fromAccountId: demoAccount.id,
    recipient: {
      type: 'Account',
      accountNumber: '40702810000000000321',
      bankBic: '044525225',
      name: 'ООО Ромашка',
    },
    amount: { minorUnits: 500000, currency: 'RUB' },
    purpose: 'Оплата услуг',
    status: 'Disputed',
    createdAt: '2026-05-13T15:40:00.000Z',
    updatedAt: '2026-05-13T16:10:00.000Z',
  },
]

module.exports = async function handler(req, res) {
  setHeaders(res)

  if (req.method === 'OPTIONS') {
    return res.status(204).end()
  }

  const route = normalizeRoute(req.query.path)

  try {
    if (req.method === 'POST' && route === 'auth/login') {
      const body = await readBody(req)
      const found = users.find((item) => item.login === body.login && item.password === body.password && item.isActive)
      if (!found) {
        return res.status(401).json({ message: 'Неверный логин или пароль.' })
      }

      const authUser = publicUser(found)
      return res.status(200).json({
        accessToken: tokenFor(authUser),
        refreshToken: `refresh.${authUser.id}`,
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        refreshTokenExpiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
        user: authUser,
      })
    }

    if (req.method === 'POST' && route === 'auth/refresh') {
      const body = await readBody(req)
      const id = String(body.refreshToken || '').replace('refresh.', '')
      const found = users.find((item) => item.id === id)
      if (!found) {
        return res.status(401).json({ message: 'Refresh token expired.' })
      }

      const authUser = publicUser(found)
      return res.status(200).json({
        accessToken: tokenFor(authUser),
        refreshToken: `refresh.${authUser.id}`,
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        refreshTokenExpiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
        user: authUser,
      })
    }

    if (req.method === 'POST' && route === 'auth/logout') {
      return res.status(204).end()
    }

    if (req.method === 'GET' && route === 'auth/me') {
      return res.status(200).json(currentUser(req))
    }

    if (req.method === 'GET' && route === 'auth/sessions') {
      return res.status(200).json([
        {
          id: 'demo-session',
          ipAddress: 'vercel-edge',
          userAgent: req.headers['user-agent'] || 'browser',
          createdAt: new Date().toISOString(),
          lastSeenAt: new Date().toISOString(),
        },
      ])
    }

    if (req.method === 'GET' && route === 'dashboard') {
      return res.status(200).json(dashboard())
    }

    if (req.method === 'POST' && route === 'transfers') {
      const body = await readBody(req)
      return res.status(200).json({ transferId: `demo-${Date.now()}`, status: 'PendingClientConfirmation', request: body })
    }

    const challengeMatch = route.match(/^transfers\/([^/]+)\/confirmation-challenge$/)
    if (req.method === 'GET' && challengeMatch) {
      const transferId = challengeMatch[1]
      const timestamp = new Date().toISOString()
      const nonce = `nonce-${Date.now()}`
      return res.status(200).json({
        id: `challenge-${transferId}`,
        nonce,
        timestamp,
        payload: `${transferId}:${nonce}:${timestamp}`,
        payloadHash: 'demo-payload-hash',
        demoAlgorithm: 'HMAC-SHA-256',
        demoSharedSecretWarning: 'Demo confirmation only.',
      })
    }

    const confirmMatch = route.match(/^transfers\/([^/]+)\/confirm$/)
    if (req.method === 'POST' && confirmMatch) {
      return res.status(200).json({ transferId: confirmMatch[1], status: 'Settled' })
    }

    const claimMatch = route.match(/^transfers\/([^/]+)\/unauthorized-claim$/)
    if (req.method === 'POST' && claimMatch) {
      return res.status(202).json({ transferId: claimMatch[1], status: 'Disputed' })
    }

    if (req.method === 'GET' && route === 'admin/users') {
      return res.status(200).json(users.map(adminUser))
    }

    const activeMatch = route.match(/^admin\/users\/([^/]+)\/(block|unblock)$/)
    if (req.method === 'POST' && activeMatch) {
      return res.status(204).end()
    }

    const riskMatch = route.match(/^(aml|fraud)\/transfers\/([^/]+)\/decision$/)
    if (req.method === 'POST' && riskMatch) {
      const body = await readBody(req)
      return res.status(202).json({ transferId: riskMatch[2], decision: body.decision || 'ManualReview' })
    }

    return res.status(404).json({ message: `Unknown API route: ${route}` })
  } catch (error) {
    return res.status(500).json({ message: error instanceof Error ? error.message : 'Server error' })
  }
}

function user(id, login, password, fullName, roles, customerId) {
  return {
    id,
    login,
    password,
    fullName,
    customerId,
    roles,
    email: login,
    phone: '+7 900 000-00-00',
    isActive: true,
    createdAt: '2026-05-14T12:00:00.000Z',
  }
}

function dashboard() {
  const riskChecks = seededTransfers.flatMap((transfer) => [
    riskCheck(`${transfer.id}-aml`, transfer.id, 'AML', 'Allowed', 'Автоматическая AML-проверка пройдена'),
    riskCheck(`${transfer.id}-fraud`, transfer.id, 'Fraud', 'LowRisk', 'Антифрод проверка устройства и получателя'),
  ])

  return {
    customer: {
      id: 'demo-customer',
      fullName: 'Иван Петров',
      phone: '+7 900 000-00-00',
      email: 'ivan.petrov@example.test',
      identificationStatus: 'Verified',
      amlRiskLevel: 'Низкий',
      isBlocked: false,
    },
    accounts: [demoAccount],
    transfers: seededTransfers,
    ledger: [
      {
        id: 'ledger-1',
        transferId: 'demo-transfer-2',
        accountNumber: demoAccount.number,
        debitMinorUnits: 80000,
        creditMinorUnits: 0,
        currency: 'RUB',
        createdAt: '2026-05-14T08:21:00.000Z',
      },
      {
        id: 'ledger-2',
        transferId: 'demo-transfer-2',
        accountNumber: '40817810000000000888',
        debitMinorUnits: 0,
        creditMinorUnits: 80000,
        currency: 'RUB',
        createdAt: '2026-05-14T08:21:00.000Z',
      },
    ],
    riskChecks,
    auditEvents: [
      {
        id: 'audit-1',
        operationId: 'demo-transfer-1',
        eventType: 'TransferCreated',
        message: 'Создано платежное распоряжение',
        createdAt: '2026-05-14T12:30:00.000Z',
      },
    ],
    notifications: [
      {
        id: 'notification-1',
        transferId: 'demo-transfer-1',
        channel: 'Email',
        recipient: 'client@savranpay.local',
        status: 'Sent',
        createdAt: '2026-05-14T12:30:00.000Z',
      },
    ],
    limits: [
      { name: 'Одна операция', value: '600 000 RUB' },
      { name: 'Новое устройство', value: 'Step-up confirmation' },
      { name: 'Новый получатель', value: 'Дополнительная антифрод-проверка' },
    ],
    compliance: [
      'JWT + refresh token + роли Customer/SupportOperator/AmlOfficer/FraudOfficer/Admin/Auditor',
      'Vercel serverless API для публичной демонстрации',
      'Production C# backend остаётся в репозитории для контейнерного деплоя',
    ],
  }
}

function riskCheck(id, transferId, checkType, decision, details) {
  return { id, transferId, checkType, decision, details, createdAt: '2026-05-14T12:30:00.000Z' }
}

function adminUser(item) {
  return {
    id: item.id,
    login: item.login,
    fullName: item.fullName,
    email: item.email,
    phone: item.phone,
    customerId: item.customerId,
    isActive: item.isActive,
    createdAt: item.createdAt,
    roles: item.roles,
  }
}

function publicUser(item) {
  return {
    id: item.id,
    login: item.login,
    fullName: item.fullName,
    customerId: item.customerId,
    roles: item.roles,
  }
}

function tokenFor(authUser) {
  return `demo.${Buffer.from(JSON.stringify(authUser)).toString('base64url')}`
}

function currentUser(req) {
  const header = req.headers.authorization || ''
  const raw = header.startsWith('Bearer demo.') ? header.slice('Bearer demo.'.length) : ''
  if (!raw) {
    throw new Error('Unauthorized')
  }

  return JSON.parse(Buffer.from(raw, 'base64url').toString('utf8'))
}

function normalizeRoute(path) {
  return Array.isArray(path) ? path.join('/') : String(path || '')
}

function setHeaders(res) {
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization, Idempotency-Key, X-Request-Id')
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
  res.setHeader('Cache-Control', 'no-store')
}

async function readBody(req) {
  if (req.body && typeof req.body === 'object') {
    return req.body
  }

  const chunks = []
  for await (const chunk of req) {
    chunks.push(chunk)
  }
  const raw = Buffer.concat(chunks).toString('utf8')
  return raw ? JSON.parse(raw) : {}
}
