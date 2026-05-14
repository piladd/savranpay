const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:5001'
const ACCESS_TOKEN_KEY = 'savranpay.accessToken'
const REFRESH_TOKEN_KEY = 'savranpay.refreshToken'

export type MoneyDto = {
  minorUnits: number
  currency: string
}

export type CustomerView = {
  id: string
  fullName: string
  phone: string
  email: string
  identificationStatus: string
  amlRiskLevel: string
  isBlocked: boolean
}

export type AccountView = {
  id: string
  customerId: string
  number: string
  maskedNumber: string
  status: string
  availableBalance: MoneyDto
  reservedBalance: MoneyDto
}

export type TransferView = {
  id: string
  customerId: string
  fromAccountId: string
  recipient: {
    type: string
    accountNumber: string
    bankBic: string
    name: string
  }
  amount: MoneyDto
  purpose: string
  status: string
  createdAt: string
  updatedAt?: string
}

export type LedgerEntryView = {
  id: string
  transferId: string
  accountNumber: string
  debitMinorUnits: number
  creditMinorUnits: number
  currency: string
  createdAt: string
}

export type RiskCheckView = {
  id: string
  transferId: string
  checkType: string
  decision: string
  details: string
  createdAt: string
}

export type AuditEventView = {
  id: string
  operationId: string
  eventType: string
  message: string
  createdAt: string
}

export type NotificationView = {
  id: string
  transferId: string
  channel: string
  recipient: string
  status: string
  createdAt: string
}

export type DashboardView = {
  customer: CustomerView | null
  accounts: AccountView[]
  transfers: TransferView[]
  ledger: LedgerEntryView[]
  riskChecks: RiskCheckView[]
  auditEvents: AuditEventView[]
  notifications: NotificationView[]
  limits: Array<{ name: string; value: string }>
  compliance: string[]
}

export type AuthUser = {
  id: string
  login: string
  fullName: string
  customerId?: string
  roles: string[]
}

export type LoginResult = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: AuthUser
}

export type CreateTransferRequest = {
  fromAccountId: string
  recipient: TransferView['recipient']
  amount: MoneyDto
  purpose: string
}

export type CreateTransferResult = {
  transferId: string
  status: string
}

export type ConfirmationChallenge = {
  id: string
  nonce: string
  timestamp: string
  payload: string
  payloadHash: string
  demoAlgorithm: 'HMAC-SHA-256'
  demoSharedSecretWarning: string
}

export type ConfirmTransferRequest = {
  confirmationType: 'TransactionSignature'
  signature: string
  nonce: string
  timestamp: string
}

export async function getDashboard() {
  return request<DashboardView>('/api/v1/dashboard')
}

export async function login(loginName: string, password: string) {
  const result = await request<LoginResult>('/api/v1/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({ login: loginName, password }),
  })

  localStorage.setItem(ACCESS_TOKEN_KEY, result.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, result.refreshToken)
  return result
}

export function logout() {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
}

export function getAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_KEY)
}

export async function createTransfer(body: CreateTransferRequest) {
  return request<CreateTransferResult>('/api/v1/transfers', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': crypto.randomUUID(),
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify(body),
  })
}

export async function getConfirmationChallenge(transferId: string) {
  return request<ConfirmationChallenge>(`/api/v1/transfers/${transferId}/confirmation-challenge`)
}

export async function confirmTransfer(transferId: string, body: ConfirmTransferRequest) {
  return request<CreateTransferResult>(`/api/v1/transfers/${transferId}/confirm`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify(body),
  })
}

export async function reportUnauthorizedClaim(transferId: string) {
  return request(`/api/v1/transfers/${transferId}/unauthorized-claim`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({
      reason: 'Я не совершал эту операцию',
      description: 'Операция была замечена после уведомления',
      contactPhone: '+79990000000',
    }),
  })
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  const token = getAccessToken()
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    cache: 'no-store',
    ...init,
    headers,
  })

  if (!response.ok) {
    throw new Error(await response.text())
  }

  return response.json() as Promise<T>
}
