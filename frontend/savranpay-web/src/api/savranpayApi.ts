const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()
const isVercelHost = typeof window !== 'undefined' && window.location.hostname.endsWith('.vercel.app')
const API_BASE_URL = configuredApiBaseUrl && configuredApiBaseUrl !== 'auto' && !isVercelHost ? configuredApiBaseUrl : ''
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

export type AdminUserView = {
  id: string
  login: string
  fullName: string
  email: string
  phone: string
  customerId?: string
  isActive: boolean
  createdAt: string
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
  transfer?: TransferView
}

export type RecipientSearchResult = {
  cardNumber: string
  name: string
  accountNumber: string
  bankBic: string
  bankName: string
}

export type UserSessionView = {
  id: string
  ipAddress: string
  userAgent: string
  createdAt: string
  lastSeenAt: string
  revokedAt?: string | null
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
  const result = await rawRequest<LoginResult>('/api/v1/auth/login', {
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

export async function getCurrentUser() {
  return request<AuthUser>('/api/v1/auth/me')
}

export async function getSessions() {
  return request<UserSessionView[]>('/api/v1/auth/sessions')
}

export async function changePassword(currentPassword: string, newPassword: string) {
  return request<void>('/api/v1/auth/change-password', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({ currentPassword, newPassword }),
  })
}

export async function logout() {
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
  if (refreshToken) {
    await rawRequest('/api/v1/auth/logout', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Request-Id': crypto.randomUUID(),
      },
      body: JSON.stringify({ refreshToken }),
    }).catch(() => undefined)
  }

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

export async function searchRecipients(query: string) {
  return request<RecipientSearchResult[]>(`/api/v1/recipients/search?q=${encodeURIComponent(query)}`)
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

export async function cancelTransferRequest(transferId: string) {
  return request<CreateTransferResult>(`/api/v1/transfers/${transferId}/cancel`, {
    method: 'POST',
    headers: {
      'X-Request-Id': crypto.randomUUID(),
    },
  })
}

export async function reportUnauthorizedClaim(
  transferId: string,
  claim: {
    reason?: string
    description?: string
    contactPhone?: string
  } = {},
) {
  return request(`/api/v1/transfers/${transferId}/unauthorized-claim`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({
      reason: claim.reason ?? 'Я не совершал эту операцию',
      description: claim.description ?? 'Операция была замечена после уведомления',
      contactPhone: claim.contactPhone ?? '+79990000000',
    }),
  })
}

export async function getAdminUsers() {
  return request<AdminUserView[]>('/api/v1/admin/users')
}

export async function setAdminUserActive(userId: string, isActive: boolean) {
  return request(`/api/v1/admin/users/${userId}/${isActive ? 'unblock' : 'block'}`, {
    method: 'POST',
    headers: {
      'X-Request-Id': crypto.randomUUID(),
    },
  })
}

export async function addAdminUserRole(userId: string, role: string) {
  return request(`/api/v1/admin/users/${userId}/roles`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({ role }),
  })
}

export async function removeAdminUserRole(userId: string, role: string) {
  return request(`/api/v1/admin/users/${userId}/roles/${encodeURIComponent(role)}`, {
    method: 'DELETE',
    headers: {
      'X-Request-Id': crypto.randomUUID(),
    },
  })
}

export async function recordRiskDecision(kind: 'aml' | 'fraud', transferId: string, decision: string, details: string) {
  return request(`/api/v1/${kind}/transfers/${transferId}/decision`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({ decision, details }),
  })
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response = await rawFetch(path, init)

  if (response.status === 401 && (await refreshTokens())) {
    response = await rawFetch(path, init)
  }

  return parseResponse<T>(response)
}

async function rawRequest<T>(path: string, init?: RequestInit): Promise<T> {
  return parseResponse<T>(await rawFetch(path, init))
}

async function rawFetch(path: string, init?: RequestInit) {
  const headers = new Headers(init?.headers)
  const token = getAccessToken()
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  return fetch(`${API_BASE_URL}${path}`, {
    cache: 'no-store',
    ...init,
    headers,
  })
}

async function refreshTokens() {
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
  if (!refreshToken) {
    return false
  }

  const response = await rawFetch('/api/v1/auth/refresh', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-Id': crypto.randomUUID(),
    },
    body: JSON.stringify({ refreshToken }),
  })

  if (!response.ok) {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    return false
  }

  const result = (await response.json()) as LoginResult
  localStorage.setItem(ACCESS_TOKEN_KEY, result.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, result.refreshToken)
  return true
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(await response.text())
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}
