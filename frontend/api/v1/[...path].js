const users = [
  user('demo-client', 'client@savranpay.local', 'Client123!', 'Иван Петров', ['Customer'], 'demo-customer'),
  user('demo-support', 'support@savranpay.local', 'Support123!', 'Support Operator', ['SupportOperator']),
  user('demo-aml', 'aml@savranpay.local', 'Aml123!', 'AML Officer', ['AmlOfficer']),
  user('demo-fraud', 'fraud@savranpay.local', 'Fraud123!', 'Fraud Officer', ['FraudOfficer']),
  user('demo-admin', 'admin@savranpay.local', 'Admin123!', 'Administrator', ['Admin']),
  user('demo-audit', 'audit@savranpay.local', 'Audit123!', 'Auditor', ['Auditor']),
]

const sessions = []

const demoAccount = {
  id: 'demo-account-1',
  customerId: 'demo-customer',
  number: '40817810000000000001',
  maskedNumber: '40817810000000000001',
  status: 'Active',
  availableBalance: { minorUnits: 125000000, currency: 'RUB' },
  reservedBalance: { minorUnits: 0, currency: 'RUB' },
}

const recipientDirectory = [
  {
    cardNumber: '2202200000000001',
    name: 'Анна Смирнова',
    accountNumber: '40817810000000000999',
    bankBic: '044525225',
    bankName: 'SavranPay Банк',
  },
  {
    cardNumber: '2202200000000002',
    name: 'Петр Иванов',
    accountNumber: '40817810000000000888',
    bankBic: '044525225',
    bankName: 'SavranPay Банк',
  },
  {
    cardNumber: '2202200000000003',
    name: 'Мария Кузнецова',
    accountNumber: '40817810000000000777',
    bankBic: '044525225',
    bankName: 'SavranPay Банк',
  },
]

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

const ledgerEntries = [
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
]

const auditEvents = [
  {
    id: 'audit-1',
    operationId: 'demo-transfer-1',
    eventType: 'TransferCreated',
    message: 'Создано платежное распоряжение',
    createdAt: '2026-05-14T12:30:00.000Z',
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
      const session = createSession(req, found.id)
      return res.status(200).json({
        accessToken: tokenFor(authUser),
        refreshToken: `refresh.${authUser.id}.${session.id}`,
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        refreshTokenExpiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
        user: authUser,
      })
    }

    if (req.method === 'POST' && route === 'auth/refresh') {
      const body = await readBody(req)
      const parsed = parseRefreshToken(body.refreshToken)
      const found = users.find((item) => item.id === parsed.userId)
      const session = sessions.find((item) => item.id === parsed.sessionId && item.userId === parsed.userId && !item.revokedAt)
      if (!found || !session) {
        return res.status(401).json({ message: 'Refresh token expired.' })
      }

      session.lastSeenAt = new Date().toISOString()
      const authUser = publicUser(found)
      return res.status(200).json({
        accessToken: tokenFor(authUser),
        refreshToken: `refresh.${authUser.id}.${session.id}`,
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        refreshTokenExpiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
        user: authUser,
      })
    }

    if (req.method === 'POST' && route === 'auth/logout') {
      const body = await readBody(req)
      const parsed = parseRefreshToken(body.refreshToken)
      const session = sessions.find((item) => item.id === parsed.sessionId && item.userId === parsed.userId)
      if (session) {
        session.revokedAt = new Date().toISOString()
        auditEvents.unshift(auditEvent('Logout', parsed.userId, `Сессия ${session.id} завершена`))
      }
      return res.status(204).end()
    }

    if (req.method === 'GET' && route === 'auth/me') {
      return res.status(200).json(currentUser(req))
    }

    if (req.method === 'GET' && route === 'auth/sessions') {
      const authUser = currentUser(req)
      return res.status(200).json(
        sessions
          .filter((item) => item.userId === authUser.id)
          .map(({ userId, ...item }) => item),
      )
    }

    if (req.method === 'POST' && route === 'auth/change-password') {
      const authUser = currentUser(req)
      const body = await readBody(req)
      const found = users.find((item) => item.id === authUser.id)
      if (!found || found.password !== body.currentPassword) {
        return res.status(400).json({ message: 'Current password is invalid.' })
      }
      if (!body.newPassword || String(body.newPassword).length < 8) {
        return res.status(400).json({ message: 'New password is too short.' })
      }

      found.password = String(body.newPassword)
      for (const session of sessions.filter((item) => item.userId === found.id && !item.revokedAt)) {
        session.revokedAt = new Date().toISOString()
      }
      auditEvents.unshift(auditEvent('PasswordChanged', found.id, `Пользователь ${found.login} сменил пароль`))
      return res.status(204).end()
    }

    if (req.method === 'GET' && route === 'dashboard') {
      return res.status(200).json(dashboard())
    }

    if (req.method === 'GET' && route === 'recipients/search') {
      const query = String(req.query.q || '').replace(/\D/g, '')
      const matches = query.length < 4 ? [] : recipientDirectory.filter((item) => item.cardNumber.includes(query))
      return res.status(200).json(matches.slice(0, 5))
    }

    if (req.method === 'POST' && route === 'transfers') {
      const body = await readBody(req)
      const amount = Number(body.amount?.minorUnits || 0)
      if (!body.fromAccountId || amount <= 0) {
        return res.status(400).json({ message: 'Invalid transfer request.' })
      }
      if (body.fromAccountId !== demoAccount.id) {
        return res.status(404).json({ message: 'Source account not found.' })
      }
      if (amount > demoAccount.availableBalance.minorUnits) {
        return res.status(409).json({ message: 'Insufficient funds.' })
      }

      const now = new Date().toISOString()
      const transfer = {
        id: `demo-${Date.now()}`,
        customerId: 'demo-customer',
        fromAccountId: body.fromAccountId,
        recipient: body.recipient,
        amount: body.amount,
        purpose: body.purpose,
        status: 'PendingClientConfirmation',
        createdAt: now,
        updatedAt: now,
      }
      demoAccount.availableBalance.minorUnits -= amount
      demoAccount.reservedBalance.minorUnits += amount
      seededTransfers.unshift(transfer)
      auditEvents.unshift(auditEvent('TransferCreated', transfer.id, `Создан перевод ${transfer.recipient.name} на ${formatRub(amount)}`))
      return res.status(200).json({ transferId: transfer.id, status: transfer.status, transfer })
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
      const transfer = seededTransfers.find((item) => item.id === confirmMatch[1])
      if (!transfer) {
        return res.status(404).json({ message: 'Transfer not found.' })
      }

      if (transfer.status === 'PendingClientConfirmation') {
        demoAccount.reservedBalance.minorUnits = Math.max(0, demoAccount.reservedBalance.minorUnits - transfer.amount.minorUnits)
        transfer.status = 'Settled'
        transfer.updatedAt = new Date().toISOString()
        ledgerEntries.unshift(
          ledgerEntry(transfer, demoAccount.number, transfer.amount.minorUnits, 0),
          ledgerEntry(transfer, transfer.recipient.accountNumber, 0, transfer.amount.minorUnits),
        )
        auditEvents.unshift(auditEvent('TransferConfirmed', transfer.id, `Перевод ${transfer.id} подписан и исполнен`))
      }

      return res.status(200).json({ transferId: transfer.id, status: transfer.status, transfer })
    }

    const cancelMatch = route.match(/^transfers\/([^/]+)\/cancel$/)
    if (req.method === 'POST' && cancelMatch) {
      const transfer = seededTransfers.find((item) => item.id === cancelMatch[1])
      if (!transfer) {
        return res.status(404).json({ message: 'Transfer not found.' })
      }

      if (transfer.status === 'PendingClientConfirmation') {
        demoAccount.availableBalance.minorUnits += transfer.amount.minorUnits
        demoAccount.reservedBalance.minorUnits = Math.max(0, demoAccount.reservedBalance.minorUnits - transfer.amount.minorUnits)
        transfer.status = 'Cancelled'
        transfer.updatedAt = new Date().toISOString()
        auditEvents.unshift(auditEvent('TransferCancelled', transfer.id, `Перевод ${transfer.id} отменен клиентом`))
      }

      return res.status(200).json({ transferId: transfer.id, status: transfer.status, transfer })
    }

    const claimMatch = route.match(/^transfers\/([^/]+)\/unauthorized-claim$/)
    if (req.method === 'POST' && claimMatch) {
      const transfer = seededTransfers.find((item) => item.id === claimMatch[1])
      if (transfer) {
        transfer.status = 'Disputed'
        transfer.updatedAt = new Date().toISOString()
        auditEvents.unshift(auditEvent('TransferDisputed', transfer.id, `Клиент оспорил перевод ${transfer.id}`))
      }
      return res.status(202).json({ transferId: claimMatch[1], status: transfer?.status || 'Disputed', transfer })
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

function createSession(req, userId) {
  const now = new Date().toISOString()
  const session = {
    id: `session-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    userId,
    ipAddress: req.headers['x-forwarded-for'] || req.socket?.remoteAddress || 'vercel-edge',
    userAgent: req.headers['user-agent'] || 'browser',
    createdAt: now,
    lastSeenAt: now,
    revokedAt: null,
  }
  sessions.unshift(session)
  auditEvents.unshift(auditEvent('Login', userId, `Создана сессия ${session.id}`))
  return session
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
    ledger: ledgerEntries,
    riskChecks,
    auditEvents,
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

function ledgerEntry(transfer, accountNumber, debitMinorUnits, creditMinorUnits) {
  return {
    id: `ledger-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    transferId: transfer.id,
    accountNumber,
    debitMinorUnits,
    creditMinorUnits,
    currency: transfer.amount.currency || 'RUB',
    createdAt: new Date().toISOString(),
  }
}

function auditEvent(eventType, operationId, message) {
  return {
    id: `audit-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    operationId,
    eventType,
    message,
    createdAt: new Date().toISOString(),
  }
}

function formatRub(minorUnits) {
  return new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB' }).format(minorUnits / 100)
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

function parseRefreshToken(refreshToken) {
  const parts = String(refreshToken || '').split('.')
  return {
    userId: parts[1] || '',
    sessionId: parts.slice(2).join('.') || '',
  }
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
