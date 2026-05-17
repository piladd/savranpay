<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  addAdminUserRole,
  cancelTransferRequest,
  changePassword,
  confirmTransfer,
  createTransfer,
  getAccessToken,
  getAdminUsers,
  getConfirmationChallenge,
  getCurrentUser,
  getDashboard,
  getAdminTransferTechnicalDetails,
  getSessions,
  login,
  logout,
  recordRiskDecision,
  reportUnauthorizedClaim,
  retryAdminTransfer,
  saveSupportClaim as saveSupportClaimRequest,
  searchRecipients,
  setAdminUserActive,
  removeAdminUserRole,
  type AccountView,
  type AdminUserView,
  type AuthUser,
  type ConfirmationChallenge,
  type DashboardView,
  type RecipientSearchResult,
  type SupportClaimView,
  type TransferView,
  type UserSessionView,
} from './api/savranpayApi'

type SupportClaim = SupportClaimView

const demoSecret = import.meta.env.VITE_DEMO_TRANSFER_SECRET?.trim() ?? ''

const cabinets = [
  { path: '/cabinet/client', title: 'Клиент', role: 'Customer', icon: 'К' },
  { path: '/cabinet/support', title: 'Поддержка', role: 'SupportOperator', icon: 'S' },
  { path: '/cabinet/aml', title: 'AML', role: 'AmlOfficer', icon: 'A' },
  { path: '/cabinet/fraud', title: 'Антифрод', role: 'FraudOfficer', icon: 'F' },
  { path: '/cabinet/admin', title: 'Админ', role: 'Admin', icon: 'M' },
  { path: '/cabinet/audit', title: 'Аудит', role: 'Auditor', icon: 'R' },
] as const

const availableRoles = ['Customer', 'SupportOperator', 'AmlOfficer', 'FraudOfficer', 'Admin', 'Auditor'] as const

const emptyDashboard: DashboardView = {
  customer: null,
  accounts: [],
  transfers: [],
  ledger: [],
  riskChecks: [],
  supportClaims: [],
  auditEvents: [],
  notifications: [],
  limits: [],
  compliance: [],
}

const currentPath = ref(normalizePath(window.location.pathname))
const user = ref<AuthUser | null>(null)
const loginForm = ref({ login: '', password: '' })
const busy = ref(false)
const error = ref('')
const lastChallenge = ref('')
const recipientSearch = ref('')
const supportSearch = ref('')
const reviewSearch = ref('')
const auditSearch = ref('')
const ledgerSearch = ref('')
const securityMessage = ref('')
const serverRecipientMatches = ref<RecipientSearchResult[]>([])
const selectedTransfer = ref<TransferView | null>(null)
const confirmationChallenge = ref<ConfirmationChallenge | null>(null)
const dashboard = ref<DashboardView>({ ...emptyDashboard })
const adminUsers = ref<AdminUserView[]>([])
const sessions = ref<UserSessionView[]>([])
const adminTechnicalDetails = ref('')
const decisionDetails = ref('Проверено вручную, замечания внесены в журнал аудита')
const supportClaimForm = ref({
  status: 'Открыто',
  category: 'Оспаривание операции',
  assignedTo: 'Support' as SupportClaim['assignedTo'],
  comment: '',
  contactComment: '',
})
const supportClaims = ref<SupportClaim[]>([])
const reservedTransferIds = ref<Set<string>>(new Set())
const visibleAccountIds = ref<Set<string>>(new Set())

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

const form = ref({
  fromAccountId: '',
  recipient: {
    type: 'Account',
    accountNumber: '40817810000000000002',
    bankBic: '044525225',
    name: 'Анна Смирнова',
  },
  amountRub: 1500,
  purpose: 'Перевод собственных средств',
})

const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  repeatPassword: '',
})

const activeCabinet = computed(() => cabinets.find((cabinet) => cabinet.path === currentPath.value) ?? null)
const selectedAccount = computed(() => dashboard.value.accounts.find((account) => account.id === form.value.fromAccountId) ?? null)
const recipientMatches = computed(() => {
  const digits = recipientSearch.value.replace(/\D/g, '')
  if (digits.length < 4) return []
  const local = recipientDirectory.filter((item) => item.cardNumber.includes(digits))
  const byCard = new Map([...serverRecipientMatches.value, ...local].map((item) => [item.cardNumber, item]))
  return Array.from(byCard.values()).slice(0, 4)
})
const pendingTransfers = computed(() =>
  dashboard.value.transfers.filter((transfer) => transfer.status === 'PendingClientConfirmation'),
)
const canUseCurrentCabinet = computed(() => {
  if (!user.value) return !getAccessToken()
  if (!activeCabinet.value) return true
  return user.value.roles.includes(activeCabinet.value.role) || user.value.roles.includes('Admin')
})
const pageTitle = computed(() => activeCabinet.value?.title ?? 'SavranPay')
const pageSubtitle = computed(() =>
  activeCabinet.value ? `${activeCabinet.value.role} · ${currentPath.value}` : 'учебный fintech-сервис',
)
const totalBalance = computed(() =>
  dashboard.value.accounts.reduce((sum, account) => sum + account.availableBalance.minorUnits, 0),
)
const transferAmountMinor = computed(() => Math.round(Number(form.value.amountRub || 0) * 100))
const transferFeeMinor = computed(() => 0)
const transferTotalMinor = computed(() => transferAmountMinor.value + transferFeeMinor.value)
const riskKind = computed(() => (currentPath.value === '/cabinet/fraud' ? 'fraud' : 'aml'))
const selectedTransferAccount = computed(() =>
  selectedTransfer.value
    ? dashboard.value.accounts.find((account) => account.id === selectedTransfer.value?.fromAccountId) ?? null
    : null,
)
const selectedTransferChecks = computed(() =>
  selectedTransfer.value
    ? dashboard.value.riskChecks.filter((check) => check.transferId === selectedTransfer.value?.id)
    : [],
)
const activeConfirmationChallenge = computed(() =>
  confirmationChallenge.value?.id === selectedTransfer.value?.id ? confirmationChallenge.value : null,
)
const filteredSupportTransfers = computed(() => {
  const query = normalizeSearch(supportSearch.value)
  if (!query) return dashboard.value.transfers
  return dashboard.value.transfers.filter((transfer) =>
    [
      transfer.id,
      transfer.customerId,
      transfer.recipient.name,
      transfer.recipient.accountNumber,
      transfer.recipient.bankBic,
      transfer.purpose,
      transfer.status,
      dashboard.value.customer?.fullName,
      dashboard.value.customer?.email,
      supportClaimForTransfer(transfer.id)?.status,
      supportClaimForTransfer(transfer.id)?.category,
      supportClaimForTransfer(transfer.id)?.comment,
    ].some((value) => normalizeSearch(value).includes(query)),
  )
})
const disputedTransfers = computed(() => dashboard.value.transfers.filter((transfer) => transfer.status === 'Disputed'))
const openSupportClaims = computed(() => supportClaims.value.filter((claim) => claim.status !== 'Закрыто'))
const filteredAuditEvents = computed(() => {
  const query = normalizeSearch(auditSearch.value)
  if (!query) return dashboard.value.auditEvents
  return dashboard.value.auditEvents.filter((event) =>
    [event.createdAt, event.eventType, event.operationId, event.message, auditRole(event.eventType)].some((value) =>
      normalizeSearch(value).includes(query),
    ),
  )
})
const filteredLedgerEntries = computed(() => {
  const query = normalizeSearch(ledgerSearch.value)
  if (!query) return dashboard.value.ledger
  return dashboard.value.ledger.filter((entry) =>
    [entry.transferId, entry.accountNumber, entry.currency, entry.createdAt].some((value) => normalizeSearch(value).includes(query)),
  )
})
const filteredReviewTransfers = computed(() => {
  const query = normalizeSearch(reviewSearch.value)
  if (!query) return dashboard.value.transfers
  return dashboard.value.transfers.filter((transfer) =>
    [
      transfer.id,
      transfer.customerId,
      transfer.recipient.name,
      transfer.recipient.accountNumber,
      transfer.recipient.bankBic,
      transfer.purpose,
      transfer.status,
      dashboard.value.customer?.fullName,
      riskSummary(transfer),
      fraudDeviceLabel(transfer),
      fraudIpLabel(transfer),
    ].some((value) => normalizeSearch(value).includes(query)),
  )
})
const activeAdminUsers = computed(() => adminUsers.value.filter((item) => item.isActive).length)
const blockedAdminUsers = computed(() => adminUsers.value.filter((item) => !item.isActive).length)
const adminRoles = computed(() =>
  Array.from(new Set(adminUsers.value.flatMap((item) => item.roles))).sort((left, right) => left.localeCompare(right)),
)
const accountsInWork = computed(() => dashboard.value.accounts.filter((account) => account.status === 'Active').length)
const canConfirmSelectedTransfer = computed(() => selectedTransfer.value?.status === 'PendingClientConfirmation')
const canCancelSelectedTransfer = computed(() =>
  ['Draft', 'PendingValidation', 'PendingRiskCheck', 'PendingClientConfirmation'].includes(selectedTransfer.value?.status ?? ''),
)
const canDisputeSelectedTransfer = computed(() =>
  ['Accepted', 'Reserved', 'Processing', 'Settled'].includes(selectedTransfer.value?.status ?? ''),
)
const canRepeatSelectedTransfer = computed(() =>
  ['Settled', 'Failed', 'Cancelled', 'Disputed'].includes(selectedTransfer.value?.status ?? ''),
)

onMounted(async () => {
  window.addEventListener('popstate', () => {
    currentPath.value = normalizePath(window.location.pathname)
    void loadRoleData()
  })

  if (getAccessToken()) {
    try {
      user.value = await getCurrentUser()
      if (user.value.roles.length === 0) {
        await logout()
        user.value = null
        return
      }
      if (currentPath.value === '/') {
        const cabinet = cabinets.find((item) => user.value?.roles.includes(item.role)) ?? cabinets[0]
        navigate(cabinet.path)
      }
      await loadDashboard()
    } catch {
      await logout()
    }
  }
})

async function submitLogin() {
  busy.value = true
  error.value = ''
  try {
    const result = await login(loginForm.value.login, loginForm.value.password)
    user.value = result.user
    const cabinet = cabinets.find((item) => result.user.roles.includes(item.role)) ?? cabinets[0]
    navigate(cabinet.path)
    await loadDashboard()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось войти.'
  } finally {
    busy.value = false
  }
}

async function loadDashboard() {
  error.value = ''
  dashboard.value = await getDashboard()
  supportClaims.value = dashboard.value.supportClaims ?? []

  if (!form.value.fromAccountId && dashboard.value.accounts.length > 0) {
    form.value.fromAccountId = dashboard.value.accounts[0].id
  }

  selectedTransfer.value =
    dashboard.value.transfers.find((transfer) => transfer.id === transferIdFromPath(window.location.pathname)) ??
    dashboard.value.transfers.find((transfer) => transfer.id === selectedTransfer.value?.id) ??
    pendingTransfers.value[0] ??
    dashboard.value.transfers[0] ??
    null

  await loadRoleData()
  await loadSessions()
}

async function loadRoleData() {
  if (currentPath.value === '/cabinet/admin' && getAccessToken() && canUseCurrentCabinet.value) {
    adminUsers.value = await getAdminUsers()
  }
}

async function loadSessions() {
  if (!getAccessToken()) {
    sessions.value = []
    return
  }

  sessions.value = await getSessions().catch(() => [])
}

async function submitPasswordChange() {
  error.value = ''
  securityMessage.value = ''
  if (passwordForm.value.newPassword.length < 8) {
    error.value = 'Новый пароль должен быть не короче 8 символов.'
    return
  }
  if (passwordForm.value.newPassword !== passwordForm.value.repeatPassword) {
    error.value = 'Пароли не совпадают.'
    return
  }

  busy.value = true
  try {
    await changePassword(passwordForm.value.currentPassword, passwordForm.value.newPassword)
    passwordForm.value = { currentPassword: '', newPassword: '', repeatPassword: '' }
    securityMessage.value = 'Пароль изменен. Старые refresh-сессии отозваны.'
    await logout()
    user.value = null
    dashboard.value = { ...emptyDashboard }
    sessions.value = []
    navigate('/')
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось сменить пароль.'
  } finally {
    busy.value = false
  }
}

async function submitTransfer() {
  const validationError = validateTransferForm()
  if (validationError) {
    error.value = validationError
    return
  }

  busy.value = true
  error.value = ''
  try {
    const result = await createTransfer({
      fromAccountId: form.value.fromAccountId,
      recipient: form.value.recipient,
      amount: {
        minorUnits: Math.round(Number(form.value.amountRub) * 100),
        currency: 'RUB',
      },
      purpose: form.value.purpose,
    })

    const transfer: TransferView = result.transfer ?? {
      id: result.transferId,
      customerId: dashboard.value.customer?.id ?? user.value?.customerId ?? 'demo-customer',
      fromAccountId: form.value.fromAccountId,
      recipient: { ...form.value.recipient },
      amount: {
        minorUnits: transferAmountMinor.value,
        currency: selectedAccount.value?.availableBalance.currency ?? 'RUB',
      },
      purpose: form.value.purpose,
      status: result.status || 'PendingClientConfirmation',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
    dashboard.value.transfers = [transfer, ...dashboard.value.transfers]
    selectedTransfer.value = transfer
    reservedTransferIds.value = new Set(reservedTransferIds.value).add(transfer.id)
    adjustAccountBalance(transfer.fromAccountId, -transfer.amount.minorUnits, transfer.amount.minorUnits)
    await prepareConfirmationChallenge(transfer.id)
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось создать перевод.'
  } finally {
    busy.value = false
  }
}

async function prepareConfirmationChallenge(transferId: string) {
  const transfer = dashboard.value.transfers.find((item) => item.id === transferId)
  if (!transfer || transfer.status !== 'PendingClientConfirmation') {
    confirmationChallenge.value = null
    lastChallenge.value = ''
    return null
  }

  if (confirmationChallenge.value?.id === transferId) {
    return confirmationChallenge.value
  }

  const challenge = await getConfirmationChallenge(transferId)
  confirmationChallenge.value = challenge
  lastChallenge.value = JSON.stringify(
    {
      algorithm: challenge.demoAlgorithm,
      payloadHash: challenge.payloadHash,
      nonce: challenge.nonce,
      timestamp: challenge.timestamp,
    },
    null,
    2,
  )
  return challenge
}

async function signAndConfirm(transferId: string) {
  busy.value = true
  error.value = ''
  try {
    const challenge = await prepareConfirmationChallenge(transferId)
    if (!challenge) {
      throw new Error('Перевод недоступен для подтверждения.')
    }
    const signature = await signPayload(challenge.payload)

    await confirmTransfer(transferId, {
      confirmationType: 'TransactionSignature',
      signature,
      nonce: challenge.nonce,
      timestamp: challenge.timestamp,
    })

    lastChallenge.value = JSON.stringify(
      {
        algorithm: challenge.demoAlgorithm,
        payloadHash: challenge.payloadHash,
        nonce: challenge.nonce,
        timestamp: challenge.timestamp,
      },
      null,
      2,
    )

    const transfer = dashboard.value.transfers.find((item) => item.id === transferId)
    if (transfer && reservedTransferIds.value.has(transferId)) {
      adjustAccountBalance(transfer.fromAccountId, 0, -transfer.amount.minorUnits)
      const next = new Set(reservedTransferIds.value)
      next.delete(transferId)
      reservedTransferIds.value = next
    }
    markTransferStatus(transferId, 'Settled')
    confirmationChallenge.value = null
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось подтвердить перевод.'
  } finally {
    busy.value = false
  }
}

async function disputeTransfer(transferId: string) {
  busy.value = true
  error.value = ''
  try {
    await reportUnauthorizedClaim(transferId, {
      reason: supportClaimForm.value.category,
      description: supportClaimForm.value.comment || 'Клиент оспорил операцию из кабинета SavranPay.',
      contactPhone: dashboard.value.customer?.phone || '+79990000000',
    })
    markTransferStatus(transferId, 'Disputed')
    upsertSupportClaim(transferId, {
      status: 'Открыто',
      category: supportClaimForm.value.category,
      comment: supportClaimForm.value.comment || 'Клиент оспорил операцию.',
      contactComment: supportClaimForm.value.contactComment,
      assignedTo: 'Support',
    })
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось создать заявление.'
  } finally {
    busy.value = false
  }
}

function supportClaimForTransfer(transferId: string) {
  return supportClaims.value.find((claim) => claim.transferId === transferId) ?? null
}

function upsertSupportClaim(
  transferId: string,
  patch: Partial<Pick<SupportClaim, 'status' | 'category' | 'comment' | 'contactComment' | 'assignedTo'>>,
) {
  const now = new Date().toISOString()
  const existing = supportClaimForTransfer(transferId)

  if (existing) {
    supportClaims.value = supportClaims.value.map((claim) =>
      claim.transferId === transferId
        ? {
            ...claim,
            ...patch,
            updatedAt: now,
          }
        : claim,
    )
    return
  }

  supportClaims.value = [
    {
      id: crypto.randomUUID(),
      transferId,
      status: patch.status ?? 'Открыто',
      category: patch.category ?? 'Оспаривание операции',
      comment: patch.comment ?? '',
      contactComment: patch.contactComment ?? '',
      assignedTo: patch.assignedTo ?? 'Support',
      createdAt: now,
      updatedAt: now,
      customerId: dashboard.value.customer?.id ?? '',
      comments: [],
    },
    ...supportClaims.value,
  ]
}

async function saveSupportClaim(transferId: string) {
  busy.value = true
  error.value = ''
  try {
    const persistedClaim = await saveSupportClaimRequest(transferId, {
      status: supportClaimForm.value.status,
      category: supportClaimForm.value.category,
      comment: supportClaimForm.value.comment,
      contactComment: supportClaimForm.value.contactComment,
      assignedTo: supportClaimForm.value.assignedTo,
    })
    supportClaims.value = [persistedClaim, ...supportClaims.value.filter((item) => item.id !== persistedClaim.id)]
    securityMessage.value = 'Support claim saved.'
    await loadDashboard()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Could not save support claim.'
    return
  } finally {
    busy.value = false
  }
  securityMessage.value = 'Обращение поддержки обновлено.'
}

function transferToSupportClaimForm(transferId: string) {
  const claim = supportClaimForTransfer(transferId)
  supportClaimForm.value = {
    status: claim?.status ?? 'Открыто',
    category: claim?.category ?? 'Оспаривание операции',
    assignedTo: claim?.assignedTo ?? 'Support',
    comment: claim?.comment ?? '',
    contactComment: claim?.contactComment ?? '',
  }
}

async function cancelTransfer(transferId: string) {
  busy.value = true
  error.value = ''
  const transfer = dashboard.value.transfers.find((item) => item.id === transferId)
  try {
    await cancelTransferRequest(transferId)
    if (transfer && reservedTransferIds.value.has(transferId)) {
      adjustAccountBalance(transfer.fromAccountId, transfer.amount.minorUnits, -transfer.amount.minorUnits)
      const next = new Set(reservedTransferIds.value)
      next.delete(transferId)
      reservedTransferIds.value = next
    }
    markTransferStatus(transferId, 'Cancelled')
    confirmationChallenge.value = null
    lastChallenge.value = ''
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось отменить перевод.'
  } finally {
    busy.value = false
  }
}

function applyRecipient(recipient: RecipientSearchResult) {
  recipientSearch.value = recipient.cardNumber
  form.value.recipient.type = 'Card'
  form.value.recipient.name = recipient.name
  form.value.recipient.accountNumber = recipient.accountNumber
  form.value.recipient.bankBic = recipient.bankBic
}

async function applyRecipientBySearch() {
  const digits = recipientSearch.value.replace(/\D/g, '')
  if (digits.length >= 4) {
    serverRecipientMatches.value = await searchRecipients(digits).catch(() => [])
  } else {
    serverRecipientMatches.value = []
  }
  const found = recipientDirectory.find((item) => item.cardNumber === digits)
  const serverFound = serverRecipientMatches.value.find((item) => item.cardNumber === digits)
  if (serverFound || found) applyRecipient(serverFound ?? found!)
}

function repeatTransfer(transfer: TransferView) {
  form.value.fromAccountId = transfer.fromAccountId
  form.value.recipient = { ...transfer.recipient }
  form.value.amountRub = transfer.amount.minorUnits / 100
  form.value.purpose = transfer.purpose
  currentPath.value = '/cabinet/client'
  window.history.pushState({}, '', currentPath.value)
}

async function submitRiskDecision(transfer: TransferView, decision: 'ManualReview' | 'Allow' | 'Block') {
  busy.value = true
  error.value = ''
  try {
    await recordRiskDecision(riskKind.value, transfer.id, decision, decisionDetails.value)
    await loadDashboard()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось записать решение.'
  } finally {
    busy.value = false
  }
}

async function retrySelectedTransfer(transfer: TransferView) {
  busy.value = true
  error.value = ''
  try {
    await retryAdminTransfer(transfer.id, decisionDetails.value || 'Manual retry from admin cabinet')
    securityMessage.value = 'Запрос на повтор обработки операции записан в audit.'
    await loadDashboard()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Could not retry transfer.'
  } finally {
    busy.value = false
  }
}

async function loadTechnicalDetails(transfer: TransferView) {
  busy.value = true
  error.value = ''
  try {
    const details = await getAdminTransferTechnicalDetails(transfer.id)
    adminTechnicalDetails.value = JSON.stringify(
      {
        riskChecks: details.riskChecks.length,
        auditEvents: details.auditEvents.length,
        ledger: details.ledger.length,
        supportClaims: details.supportClaims.length,
      },
      null,
      2,
    )
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Could not load technical details.'
  } finally {
    busy.value = false
  }
}

async function toggleUser(item: AdminUserView) {
  busy.value = true
  error.value = ''
  try {
    await setAdminUserActive(item.id, !item.isActive)
    await loadRoleData()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось изменить пользователя.'
  } finally {
    busy.value = false
  }
}

async function toggleAdminRole(item: AdminUserView, role: string) {
  busy.value = true
  error.value = ''
  try {
    if (item.roles.includes(role)) {
      await removeAdminUserRole(item.id, role)
    } else {
      await addAdminUserRole(item.id, role)
    }
    await loadRoleData()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось изменить роль пользователя.'
  } finally {
    busy.value = false
  }
}

async function signPayload(payload: string) {
  if (!demoSecret) {
    throw new Error('Demo transfer signing is disabled: set VITE_DEMO_TRANSFER_SECRET for the training stand.')
  }

  const encoder = new TextEncoder()
  const key = await crypto.subtle.importKey(
    'raw',
    encoder.encode(demoSecret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  )
  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(payload))
  return arrayBufferToBase64(signature)
}

function navigate(path: string) {
  currentPath.value = normalizePath(path)
  window.history.pushState({}, '', currentPath.value)
  void loadRoleData()
}

async function signOut() {
  await logout()
  user.value = null
  dashboard.value = { ...emptyDashboard }
  adminUsers.value = []
  sessions.value = []
  navigate('/')
}

function selectTransfer(transfer: TransferView) {
  selectedTransfer.value = transfer
  lastChallenge.value = ''
  confirmationChallenge.value = null
  if (currentPath.value === '/cabinet/client') {
    window.history.pushState({}, '', `/cabinet/client/transfers/${transfer.id}`)
  } else if (currentPath.value === '/cabinet/support') {
    window.history.pushState({}, '', `/cabinet/support/transfers/${transfer.id}`)
  } else if (currentPath.value === '/cabinet/aml') {
    window.history.pushState({}, '', `/cabinet/aml/reviews/${transfer.id}`)
  } else if (currentPath.value === '/cabinet/fraud') {
    window.history.pushState({}, '', `/cabinet/fraud/reviews/${transfer.id}`)
  } else if (currentPath.value === '/cabinet/admin') {
    window.history.pushState({}, '', `/cabinet/admin/operations/${transfer.id}`)
  } else if (currentPath.value === '/cabinet/audit') {
    window.history.pushState({}, '', `/cabinet/audit/events/${transfer.id}`)
  }
  if (currentPath.value === '/cabinet/support') {
    transferToSupportClaimForm(transfer.id)
  }
  if (transfer.status === 'PendingClientConfirmation') {
    void prepareConfirmationChallenge(transfer.id).catch(() => undefined)
  }
}

function normalizePath(path: string) {
  if (path === '/') return '/'
  const direct = cabinets.find((cabinet) => cabinet.path === path)
  if (direct) return direct.path
  if (path.startsWith('/cabinet/client/transfers/')) return '/cabinet/client'
  if (path.startsWith('/cabinet/support/transfers/')) return '/cabinet/support'
  if (path.startsWith('/cabinet/aml/reviews/')) return '/cabinet/aml'
  if (path.startsWith('/cabinet/fraud/reviews/')) return '/cabinet/fraud'
  if (path.startsWith('/cabinet/audit/events/')) return '/cabinet/audit'
  if (path.startsWith('/cabinet/admin/operations/')) return '/cabinet/admin'
  return '/cabinet/client'
}

function transferIdFromPath(path: string) {
  return path.match(/\/cabinet\/(?:client\/transfers|support\/transfers|aml\/reviews|fraud\/reviews|audit\/events|admin\/operations)\/([^/]+)/)?.[1] ?? null
}

function arrayBufferToBase64(buffer: ArrayBuffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary)
}

function money(value?: { minorUnits: number; currency: string } | null) {
  if (!value) return '0,00 RUB'
  return minor(value.minorUnits, value.currency)
}

function minor(minorUnits: number, currency: string) {
  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: currency || 'RUB',
  }).format((minorUnits || 0) / 100)
}

function date(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    dateStyle: 'short',
    timeStyle: 'medium',
  }).format(new Date(value))
}

function statusClass(status: string) {
  if (status === 'Settled') return 'success'
  if (status === 'Confirmed' || status === 'Accepted' || status === 'Reserved' || status === 'Processing' || status === 'PendingClientConfirmation') return 'info'
  if (status === 'Failed' || status === 'Blocked') return 'danger'
  if (status === 'Disputed') return 'disputed'
  if (status === 'Cancelled') return 'neutral'
  return 'warn'
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Черновик',
    Created: 'Создан',
    PendingValidation: 'Проверяется',
    PendingRiskCheck: 'Проверка AML/Fraud',
    PendingClientConfirmation: 'Ожидает подтверждения',
    Confirmed: 'Подтвержден',
    Accepted: 'Подтвержден',
    Reserved: 'Зарезервирован',
    Processing: 'Исполняется',
    Settled: 'Исполнен',
    Failed: 'Ошибка',
    Disputed: 'Оспорен',
    Blocked: 'Заблокирован',
    ManualReview: 'Ручная проверка',
    Cancelled: 'Отменен',
    Reversed: 'Возвращен',
  }
  return labels[status] ?? status
}

function accountStatusLabel(status: string) {
  const labels: Record<string, string> = { Active: 'Активен', Closed: 'Закрыт', Blocked: 'Заблокирован' }
  return labels[status] ?? status
}

function accountLabel(account: AccountView) {
  return `${account.maskedNumber || maskAccount(account.number)} · ${money(account.availableBalance)}`
}

function accountNumberLabel(account: AccountView) {
  return isAccountNumberVisible(account.id) ? account.number : account.maskedNumber || maskAccount(account.number)
}

function isAccountNumberVisible(accountId: string) {
  return visibleAccountIds.value.has(accountId)
}

function toggleAccountNumber(accountId: string) {
  const next = new Set(visibleAccountIds.value)
  if (next.has(accountId)) {
    next.delete(accountId)
  } else {
    next.add(accountId)
  }
  visibleAccountIds.value = next
}

function recipientDetails(transfer: TransferView) {
  return `${maskAccount(transfer.recipient.accountNumber)} · БИК ${transfer.recipient.bankBic}`
}

function normalizeSearch(value?: string | number | null) {
  return String(value ?? '').trim().toLocaleLowerCase('ru-RU')
}

function maskAccount(accountNumber?: string) {
  if (!accountNumber) return '—'
  if (accountNumber.length <= 8) return accountNumber
  return `${accountNumber.slice(0, 4)} **** **** ${accountNumber.slice(-4)}`
}

function riskSummary(transfer: TransferView) {
  const checks = dashboard.value.riskChecks.filter((check) => check.transferId === transfer.id)
  if (checks.length === 0) return 'Проверяется'
  return checks.map((check) => `${check.checkType}: ${check.decision}`).join(' · ')
}

function riskDecision(transfer: TransferView, checkType: 'AML' | 'Fraud') {
  return dashboard.value.riskChecks.find((check) => check.transferId === transfer.id && check.checkType === checkType)?.decision ?? 'Ожидает'
}

function fraudRiskLevel(transfer: TransferView) {
  const decision = riskDecision(transfer, 'Fraud')
  if (decision === 'Block' || decision === 'Blocked' || decision === 'CriticalRisk') return 'Critical'
  if (decision === 'ManualReview' || decision === 'MediumRisk') return 'Medium'
  return 'Low'
}

function fraudDeviceLabel(transfer: TransferView) {
  return transfer.amount.minorUnits > 10000000 || transfer.status === 'PendingClientConfirmation' ? 'Новое устройство' : 'Известное устройство'
}

function fraudIpLabel(transfer: TransferView) {
  const tail = Math.abs(hashCode(transfer.id)) % 220
  return `192.0.2.${tail + 10}`
}

function hashCode(value: string) {
  return value.split('').reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) | 0, 0)
}

function riskFactors(transfer: TransferView) {
  const factors = [
    `Сумма: ${money(transfer.amount)}`,
    `Получатель: ${transfer.recipient.name}`,
    `БИК: ${transfer.recipient.bankBic}`,
    `Назначение: ${transfer.purpose}`,
  ]
  if (transfer.amount.minorUnits > 10000000) factors.push('Крупная сумма для дополнительного контроля')
  if (transfer.status === 'PendingClientConfirmation') factors.push('Операция ожидает клиентского подтверждения')
  if (transfer.status === 'Disputed') factors.push('Есть спор клиента по операции')
  return factors
}

function transferById(transferId: string) {
  return dashboard.value.transfers.find((transfer) => transfer.id === transferId) ?? null
}

function ledgerTransferLabel(transferId: string) {
  const transfer = transferById(transferId)
  if (!transfer) return transferId
  return `${transfer.recipient.name} · ${money(transfer.amount)}`
}

function auditRole(eventType: string) {
  if (eventType.includes('AML')) return 'AML'
  if (eventType.includes('Fraud')) return 'Fraud'
  if (eventType.includes('Admin') || eventType.includes('User')) return 'Admin'
  if (eventType.includes('UnauthorizedClaim')) return 'Support'
  if (eventType.includes('Transfer')) return 'Customer'
  return 'System'
}

function auditObjectLabel(operationId: string) {
  return transferById(operationId) ? `Перевод ${operationId}` : operationId
}

function csvCell(value?: string | number | null) {
  return `"${String(value ?? '').replace(/"/g, '""')}"`
}

function exportAuditEvents() {
  const header = ['Дата', 'Пользователь', 'Роль', 'Тип события', 'Объект', 'Сообщение', 'IP/User-Agent']
  const rows = filteredAuditEvents.value.map((event) => [
    date(event.createdAt),
    'system',
    auditRole(event.eventType),
    event.eventType,
    auditObjectLabel(event.operationId),
    event.message,
    '',
  ])
  const csv = [header, ...rows].map((row) => row.map(csvCell).join(';')).join('\n')
  const blob = new Blob([`\ufeff${csv}`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `savranpay-audit-${new Date().toISOString().slice(0, 10)}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

function validateTransferForm() {
  if (!form.value.fromAccountId) return 'Выберите счет списания.'
  if (transferAmountMinor.value <= 0) return 'Сумма должна быть больше 0.'
  if (selectedAccount.value && transferTotalMinor.value > selectedAccount.value.availableBalance.minorUnits) {
    return 'Сумма превышает доступный баланс.'
  }
  if (!form.value.recipient.accountNumber.trim()) return 'Заполните счет получателя.'
  if (form.value.recipient.accountNumber.trim().length < 12) return 'Счет получателя слишком короткий.'
  if (!form.value.recipient.bankBic.trim()) return 'Заполните БИК банка получателя.'
  if (!form.value.recipient.name.trim()) return 'Заполните имя получателя.'
  if (!form.value.purpose.trim()) return 'Заполните назначение платежа.'
  if (/[<>]/.test(form.value.purpose)) return 'Назначение платежа содержит запрещенные символы.'
  if (transferAmountMinor.value > 60000000) return 'Сумма превышает лимит одной операции 600 000 ₽.'
  return ''
}

function markTransferStatus(transferId: string, status: string) {
  dashboard.value.transfers = dashboard.value.transfers.map((transfer) =>
    transfer.id === transferId ? { ...transfer, status, updatedAt: new Date().toISOString() } : transfer,
  )
  selectedTransfer.value = dashboard.value.transfers.find((transfer) => transfer.id === transferId) ?? selectedTransfer.value
}

function adjustAccountBalance(accountId: string, availableDelta: number, reservedDelta: number) {
  dashboard.value.accounts = dashboard.value.accounts.map((account) => {
    if (account.id !== accountId) return account
    return {
      ...account,
      availableBalance: {
        ...account.availableBalance,
        minorUnits: Math.max(0, account.availableBalance.minorUnits + availableDelta),
      },
      reservedBalance: {
        ...account.reservedBalance,
        minorUnits: Math.max(0, account.reservedBalance.minorUnits + reservedDelta),
      },
    }
  })
}

function transferTimeline(transfer: TransferView) {
  const items = [
    { label: 'Перевод создан', at: transfer.createdAt },
    { label: 'Проверка баланса пройдена', at: transfer.createdAt },
    { label: 'AML-проверка пройдена', at: transfer.createdAt },
    { label: 'Антифрод-проверка пройдена', at: transfer.createdAt },
  ]
  if (transfer.status === 'PendingClientConfirmation') {
    items.push({ label: 'Ожидает подтверждения клиента', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (['Accepted', 'Reserved', 'Settled', 'Confirmed', 'Processing'].includes(transfer.status)) {
    items.push({ label: 'Клиент подтвердил перевод', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (transfer.status === 'Reserved') {
    items.push({ label: 'Сумма зарезервирована на счете', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (transfer.status === 'Processing') {
    items.push({ label: 'Перевод исполняется', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (transfer.status === 'Settled') {
    items.push({ label: 'Перевод исполнен', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (transfer.status === 'Disputed') items.push({ label: 'Клиент оспорил операцию', at: transfer.updatedAt ?? transfer.createdAt })
  if (transfer.status === 'Cancelled') items.push({ label: 'Перевод отменен до подтверждения', at: transfer.updatedAt ?? transfer.createdAt })
  if (transfer.status === 'Failed') items.push({ label: 'Ошибка исполнения или проверки', at: transfer.updatedAt ?? transfer.createdAt })
  return items
}
</script>

<template>
  <aside class="sidebar">
    <div class="brand">
      <div class="brand-mark">SP</div>
      <div class="brand-copy">
        <strong>SavranPay</strong>
        <span>Ролевые кабинеты</span>
      </div>
    </div>

    <nav>
      <button
        v-for="cabinet in cabinets"
        :key="cabinet.path"
        :class="{ active: currentPath === cabinet.path }"
        @click="navigate(cabinet.path)"
      >
        <span class="nav-icon">{{ cabinet.icon }}</span>
        <span>{{ cabinet.title }}</span>
      </button>
    </nav>

    <div class="security-note">
      <strong>JWT + refresh token</strong>
      <span>Доступ выдается по логину, паролю и роли пользователя.</span>
    </div>
  </aside>

  <main class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">SavranPay</p>
        <h1>{{ pageTitle }}</h1>
        <span class="muted">{{ pageSubtitle }}</span>
      </div>
      <div v-if="user || dashboard.customer" class="profile">
        <span>{{ user?.roles.join(', ') || dashboard.customer?.identificationStatus }}</span>
        <strong>{{ user?.fullName || dashboard.customer?.fullName }}</strong>
        <button v-if="user" class="ghost" @click="signOut">Выйти</button>
      </div>
    </header>

    <section v-if="error" class="alert">{{ error }}</section>
    <section v-if="securityMessage" class="alert success-alert">{{ securityMessage }}</section>

    <section v-if="!getAccessToken()" class="auth-layout">
      <form class="panel login-card" @submit.prevent="submitLogin">
        <div class="panel-head">
          <div>
            <span>Вход в SavranPay</span>
            <small>Демо-кабинеты подключены к Railway API</small>
          </div>
        </div>
        <p class="login-copy">Учебный банковский интерфейс для переводов, риск-проверок, поддержки и аудита.</p>
        <label>Логин <input v-model="loginForm.login" autocomplete="username" placeholder="email пользователя" required /></label>
        <label>Пароль <input v-model="loginForm.password" autocomplete="current-password" placeholder="Введите пароль" type="password" required /></label>
        <button class="primary" type="submit" :disabled="busy">{{ busy ? 'Входим...' : 'Войти' }}</button>
      </form>

      <article class="panel role-card">
        <div class="panel-head">
          <div>
            <span>Тестовые роли</span>
            <small>Пароли не отображаются в production UI</small>
          </div>
        </div>
        <div class="chips">
          <span v-for="cabinet in cabinets" :key="cabinet.role">{{ cabinet.role }}</span>
        </div>
      </article>
    </section>

    <section v-else-if="!canUseCurrentCabinet" class="alert">
      У текущего пользователя нет роли для этого кабинета.
    </section>

    <template v-else>
      <section class="stats-strip">
        <div>
          <span>Доступный остаток</span>
          <strong>{{ minor(totalBalance, 'RUB') }}</strong>
        </div>
        <div>
          <span>Переводы</span>
          <strong>{{ dashboard.transfers.length }}</strong>
        </div>
        <div>
          <span>Risk checks</span>
          <strong>{{ dashboard.riskChecks.length }}</strong>
        </div>
        <div>
          <span>Аудит</span>
          <strong>{{ dashboard.auditEvents.length }}</strong>
        </div>
      </section>

      <section v-if="currentPath === '/cabinet/client'" class="client-layout">
        <article class="panel client-profile">
          <div>
            <span class="section-label">Профиль клиента</span>
            <h2>{{ dashboard.customer?.fullName || user?.fullName }}</h2>
            <p>{{ dashboard.customer?.email || user?.login }}</p>
            <p>{{ dashboard.customer?.phone }}</p>
          </div>
          <div class="profile-badges">
            <span class="badge success">{{ dashboard.customer?.identificationStatus || 'Verified' }}</span>
            <span class="badge warn">AML-риск: {{ dashboard.customer?.amlRiskLevel || 'Низкий' }}</span>
            <span class="badge info">Роль: {{ user?.roles[0] || 'Customer' }}</span>
            <button v-if="user" class="ghost" @click="signOut">Выйти</button>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>Счета</span><small>Только просмотр. Счет можно выбрать в форме перевода.</small></div>
          <div class="accounts">
            <div v-for="account in dashboard.accounts" :key="account.id" class="account">
              <span class="readonly-label">Счет</span>
              <div class="account-title">
                <strong class="account-number">{{ accountNumberLabel(account) }}</strong>
                <button
                  class="icon-button"
                  type="button"
                  :aria-label="isAccountNumberVisible(account.id) ? 'Скрыть номер счета' : 'Показать номер счета'"
                  :title="isAccountNumberVisible(account.id) ? 'Скрыть номер счета' : 'Показать номер счета'"
                  @click="toggleAccountNumber(account.id)"
                >
                  <svg class="eye-icon" viewBox="0 0 24 24" aria-hidden="true">
                    <path
                      v-if="!isAccountNumberVisible(account.id)"
                      d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z"
                    />
                    <circle v-if="!isAccountNumberVisible(account.id)" cx="12" cy="12" r="3" />
                    <path
                      v-if="isAccountNumberVisible(account.id)"
                      d="M3 3l18 18M10.6 6.2A10.6 10.6 0 0 1 12 6c6.5 0 10 6 10 6a18.8 18.8 0 0 1-3.1 3.7M6.7 6.9C3.7 8.7 2 12 2 12s3.5 6 10 6c1.5 0 2.8-.3 4-.8M9.9 9.9A3 3 0 0 0 14.1 14.1"
                    />
                  </svg>
                </button>
              </div>
              <div class="readonly-grid">
                <span>Валюта</span><b>{{ account.availableBalance.currency }}</b>
                <span>Доступно</span><b>{{ money(account.availableBalance) }}</b>
                <span>Зарезервировано</span><b>{{ money(account.reservedBalance) }}</b>
                <span>Статус</span><b>{{ accountStatusLabel(account.status) }}</b>
              </div>
            </div>
          </div>
        </article>

        <article class="panel security-panel">
          <div class="panel-head">
            <span>Безопасность и сессии</span>
            <button class="ghost" type="button" @click="loadSessions">Обновить</button>
          </div>
          <div class="security-grid">
            <form class="password-form" @submit.prevent="submitPasswordChange">
              <label>Текущий пароль <input v-model="passwordForm.currentPassword" autocomplete="current-password" type="password" required /></label>
              <label>Новый пароль <input v-model="passwordForm.newPassword" autocomplete="new-password" type="password" required /></label>
              <label>Повтор нового пароля <input v-model="passwordForm.repeatPassword" autocomplete="new-password" type="password" required /></label>
              <button class="primary" type="submit" :disabled="busy">Сменить пароль</button>
            </form>
            <div class="session-list">
              <div v-for="session in sessions" :key="session.id" class="session-card">
                <strong>{{ session.revokedAt ? 'Завершена' : 'Активна' }}</strong>
                <span>{{ session.ipAddress }}</span>
                <small>{{ session.userAgent }}</small>
                <small>Создана: {{ date(session.createdAt) }}</small>
                <small>Последняя активность: {{ date(session.lastSeenAt) }}</small>
              </div>
              <p v-if="sessions.length === 0" class="muted">Активные сессии появятся после входа.</p>
            </div>
          </div>
        </article>

        <section class="grid transfer-grid">
          <form class="panel transfer-form" @submit.prevent="submitTransfer">
            <div class="panel-head">
              <span>Новый перевод</span>
              <small>Создается распоряжение, затем проверка и подтверждение</small>
            </div>

            <label>
              Счет списания
              <select v-model="form.fromAccountId" required>
                <option v-for="account in dashboard.accounts" :key="account.id" :value="account.id">
                  {{ accountLabel(account) }}
                </option>
              </select>
            </label>

            <div class="readonly-grid form-summary">
              <span>Доступный баланс</span><b>{{ money(selectedAccount?.availableBalance) }}</b>
              <span>Валюта</span><b>{{ selectedAccount?.availableBalance.currency || 'RUB' }}</b>
            </div>

            <label>
              Тип получателя
              <select v-model="form.recipient.type">
                <option value="Account">Счет</option>
                <option value="Card">Карта</option>
                <option value="Sbp">СБП</option>
              </select>
            </label>
            <label class="recipient-search">
              Поиск по номеру карты
              <input
                v-model="recipientSearch"
                inputmode="numeric"
                maxlength="19"
                placeholder="Например 2202200000000001"
                @input="applyRecipientBySearch"
              />
              <div v-if="recipientMatches.length" class="recipient-suggestions">
                <button
                  v-for="recipient in recipientMatches"
                  :key="recipient.cardNumber"
                  class="recipient-suggestion"
                  type="button"
                  @click="applyRecipient(recipient)"
                >
                  <strong>{{ recipient.name }}</strong>
                  <span>{{ recipient.cardNumber }} · {{ recipient.bankName }}</span>
                </button>
              </div>
            </label>
            <label>Получатель <input v-model="form.recipient.name" required /></label>
            <label>Счет получателя <input v-model="form.recipient.accountNumber" required maxlength="20" /></label>
            <label>БИК банка <input v-model="form.recipient.bankBic" required maxlength="9" /></label>
            <label>Сумма, RUB <input v-model.number="form.amountRub" type="number" min="1" step="1" required /></label>

            <div class="readonly-grid form-summary">
              <span>Комиссия</span><b>{{ minor(transferFeeMinor, 'RUB') }}</b>
              <span>Итого к списанию</span><b>{{ minor(transferTotalMinor, 'RUB') }}</b>
            </div>

            <label>Назначение платежа <textarea v-model="form.purpose" required rows="3" /></label>

            <button class="primary" type="submit" :disabled="busy">Создать перевод</button>
          </form>

          <article class="panel confirmation">
            <div class="panel-head">
              <span>Подтверждение перевода</span>
              <small>Все поля ниже только для чтения</small>
            </div>

            <template v-if="selectedTransfer">
              <div class="confirm-card">
                <div class="detail-line"><b>Получатель</b><span>{{ selectedTransfer.recipient.name }}</span></div>
                <div class="detail-line"><b>Счет получателя</b><span>{{ maskAccount(selectedTransfer.recipient.accountNumber) }}</span></div>
                <div class="detail-line"><b>БИК</b><span>{{ selectedTransfer.recipient.bankBic }}</span></div>
                <div class="detail-line"><b>Счет списания</b><span>{{ selectedTransferAccount?.maskedNumber || maskAccount(selectedTransferAccount?.number) }}</span></div>
                <div class="detail-line"><b>Сумма</b><span>{{ money(selectedTransfer.amount) }}</span></div>
                <div class="detail-line"><b>Комиссия</b><span>{{ minor(0, selectedTransfer.amount.currency) }}</span></div>
                <div class="detail-line"><b>Итого</b><span>{{ money(selectedTransfer.amount) }}</span></div>
                <div class="detail-line"><b>Назначение</b><span>{{ selectedTransfer.purpose }}</span></div>
                <div class="detail-line"><b>Статус</b><span class="badge" :class="statusClass(selectedTransfer.status)">{{ statusLabel(selectedTransfer.status) }}</span></div>
                <div v-if="activeConfirmationChallenge" class="detail-line"><b>Payload hash</b><span>{{ activeConfirmationChallenge.payloadHash }}</span></div>
                <div v-if="activeConfirmationChallenge" class="detail-line"><b>Nonce</b><span>{{ activeConfirmationChallenge.nonce }}</span></div>
                <div v-if="activeConfirmationChallenge" class="detail-line"><b>Timestamp</b><span>{{ date(activeConfirmationChallenge.timestamp) }}</span></div>
              </div>
              <div class="actions">
                <button
                  class="ghost"
                  type="button"
                  :disabled="busy || !canConfirmSelectedTransfer"
                  @click="prepareConfirmationChallenge(selectedTransfer.id)"
                >
                  Обновить данные подписи
                </button>
                <button
                  class="primary"
                  :disabled="busy || !canConfirmSelectedTransfer || !activeConfirmationChallenge"
                  @click="signAndConfirm(selectedTransfer.id)"
                >
                  Подписать и подтвердить
                </button>
                <button class="ghost" :disabled="busy || !canCancelSelectedTransfer" @click="cancelTransfer(selectedTransfer.id)">
                  Отмена
                </button>
              </div>
              <pre v-if="lastChallenge">{{ lastChallenge }}</pre>
            </template>
            <p v-else class="muted">Создайте перевод или откройте перевод из истории.</p>
          </article>
        </section>

        <article class="panel">
          <div class="panel-head"><span>История переводов</span><small>Список только для просмотра и перехода в карточку</small></div>
          <div class="table transfers-table">
            <div class="row header"><span>Дата</span><span>Получатель</span><span>Счет</span><span>Сумма</span><span>Статус</span><span>Назначение</span><span>Действие</span></div>
            <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
              <span>{{ date(transfer.createdAt) }}</span>
              <span>{{ transfer.recipient.name }}</span>
              <span>{{ maskAccount(transfer.recipient.accountNumber) }}</span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
              <span>{{ transfer.purpose }}</span>
              <span class="row-actions">
                <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
                <button v-if="transfer.status === 'PendingClientConfirmation'" class="ghost" @click="selectTransfer(transfer)">Подтвердить</button>
                <button v-if="['Accepted', 'Reserved', 'Processing', 'Settled'].includes(transfer.status)" class="ghost" @click="disputeTransfer(transfer.id)">Оспорить</button>
                <button v-if="['Settled', 'Failed', 'Cancelled', 'Disputed'].includes(transfer.status)" class="ghost" @click="repeatTransfer(transfer)">Повторить</button>
                <button v-if="['Draft', 'PendingValidation', 'PendingRiskCheck', 'PendingClientConfirmation'].includes(transfer.status)" class="ghost" @click="cancelTransfer(transfer.id)">Отменить</button>
              </span>
            </div>
          </div>
        </article>

        <article v-if="selectedTransfer" class="panel transfer-details">
          <div class="panel-head"><span>Карточка перевода</span><small>/cabinet/client/transfers/{{ selectedTransfer.id }}</small></div>
          <div class="details-grid">
            <section>
              <h3>Основная информация</h3>
              <div class="readonly-grid">
                <span>ID перевода</span><b>{{ selectedTransfer.id }}</b>
                <span>Дата создания</span><b>{{ date(selectedTransfer.createdAt) }}</b>
                <span>Дата обновления</span><b>{{ date(selectedTransfer.updatedAt || selectedTransfer.createdAt) }}</b>
                <span>Статус</span><b><span class="badge" :class="statusClass(selectedTransfer.status)">{{ statusLabel(selectedTransfer.status) }}</span></b>
                <span>Клиент</span><b>{{ dashboard.customer?.fullName || selectedTransfer.customerId }}</b>
                <span>Сумма</span><b>{{ money(selectedTransfer.amount) }}</b>
                <span>Валюта</span><b>{{ selectedTransfer.amount.currency }}</b>
                <span>Назначение</span><b>{{ selectedTransfer.purpose }}</b>
              </div>
            </section>
            <section>
              <h3>Отправитель</h3>
              <div class="readonly-grid">
                <span>ФИО клиента</span><b>{{ dashboard.customer?.fullName }}</b>
                <span>Customer ID</span><b>{{ selectedTransfer.customerId }}</b>
                <span>Счет списания</span><b>{{ selectedTransferAccount?.maskedNumber || maskAccount(selectedTransferAccount?.number) }}</b>
                <span>Доступно сейчас</span><b>{{ money(selectedTransferAccount?.availableBalance) }}</b>
                <span>Зарезервировано</span><b>{{ money(selectedTransferAccount?.reservedBalance) }}</b>
              </div>
            </section>
            <section>
              <h3>Получатель</h3>
              <div class="readonly-grid">
                <span>Тип</span><b>{{ selectedTransfer.recipient.type }}</b>
                <span>Имя</span><b>{{ selectedTransfer.recipient.name }}</b>
                <span>Счет</span><b>{{ maskAccount(selectedTransfer.recipient.accountNumber) }}</b>
                <span>БИК</span><b>{{ selectedTransfer.recipient.bankBic }}</b>
                <span>Банк получателя</span><b>SavranPay Банк</b>
              </div>
            </section>
            <section>
              <h3>Проверки</h3>
              <div class="timeline compact-timeline">
                <div v-for="check in selectedTransferChecks" :key="check.id">
                  <strong>{{ check.checkType }} · {{ check.decision }}</strong>
                  <span>{{ check.details }}</span>
                </div>
                <div v-if="selectedTransferChecks.length === 0">
                  <strong>Проверка безопасности</strong>
                  <span>Проверка запущена и ожидает решения.</span>
                </div>
              </div>
            </section>
            <section class="wide-detail">
              <h3>История статусов</h3>
              <div class="timeline status-timeline">
                <div v-for="item in transferTimeline(selectedTransfer)" :key="item.label">
                  <strong>{{ date(item.at) }}</strong>
                  <span>{{ item.label }}</span>
                </div>
              </div>
            </section>
            <section class="wide-detail">
              <h3>Действия</h3>
              <div class="actions">
                <button class="primary" :disabled="busy || !canConfirmSelectedTransfer" @click="signAndConfirm(selectedTransfer.id)">
                  Подтвердить
                </button>
                <button class="ghost" :disabled="busy || !canCancelSelectedTransfer" @click="cancelTransfer(selectedTransfer.id)">Отменить</button>
                <button class="ghost" :disabled="busy || !canDisputeSelectedTransfer" @click="disputeTransfer(selectedTransfer.id)">Оспорить</button>
                <button class="ghost" :disabled="busy || !canRepeatSelectedTransfer" @click="repeatTransfer(selectedTransfer)">Повторить как новый</button>
                <button class="ghost" :disabled="selectedTransfer.status !== 'Settled'">Скачать чек</button>
              </div>
            </section>
          </div>
        </article>
      </section>

      <section v-if="currentPath === '/cabinet/support'" class="panel">
        <div class="panel-head"><span>Поддержка: операции и спорные переводы</span><small>Поддержка не меняет сумму, получателя и ledger</small></div>
        <div class="stats-strip support-summary">
          <div>
            <span>Открытые обращения</span>
            <strong>{{ openSupportClaims.length }}</strong>
          </div>
          <div>
            <span>Спорные переводы</span>
            <strong>{{ disputedTransfers.length }}</strong>
          </div>
          <div>
            <span>Передано AML/Fraud</span>
            <strong>{{ supportClaims.filter((claim) => claim.assignedTo === 'AML' || claim.assignedTo === 'Fraud').length }}</strong>
          </div>
          <div>
            <span>Все операции</span>
            <strong>{{ dashboard.transfers.length }}</strong>
          </div>
        </div>
        <div class="support-tools">
          <label>Поиск клиента или операции <input v-model="supportSearch" placeholder="ФИО, email, Customer ID, получатель или ID перевода" /></label>
          <label>Категория проблемы
            <select v-model="supportClaimForm.category">
              <option>Оспаривание операции</option>
              <option>Ошибка исполнения</option>
              <option>Подозрение на мошенничество</option>
              <option>Запрос документов</option>
            </select>
          </label>
        </div>
        <div class="table support-table">
          <div class="row header"><span>Дата</span><span>Клиент</span><span>Получатель</span><span>Сумма</span><span>Статус</span><span>Риск</span><span>Обращение</span><span>Действие</span></div>
          <div v-for="transfer in filteredSupportTransfers" :key="transfer.id" class="row">
            <span>{{ date(transfer.createdAt) }}</span>
            <span><strong>{{ dashboard.customer?.fullName }}</strong><small>{{ dashboard.customer?.email }}</small></span>
            <span class="recipient-cell">
              <strong>{{ transfer.recipient.name }}</strong>
              <small>{{ recipientDetails(transfer) }}</small>
            </span>
            <strong>{{ money(transfer.amount) }}</strong>
            <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
            <span>{{ riskSummary(transfer) }}</span>
            <span>
              <strong>{{ supportClaimForTransfer(transfer.id)?.status ?? (transfer.status === 'Disputed' ? 'Открыто' : 'Нет') }}</strong>
              <small>{{ supportClaimForTransfer(transfer.id)?.assignedTo ?? 'Support' }}</small>
            </span>
            <span class="row-actions"><button class="ghost" @click="selectTransfer(transfer)">Открыть</button></span>
          </div>
          <div v-if="filteredSupportTransfers.length === 0" class="row table-empty">
            <span>Нет операций по выбранному поиску.</span>
          </div>
        </div>
      </section>

      <section v-if="currentPath === '/cabinet/aml' || currentPath === '/cabinet/fraud'" class="grid two">
        <article class="panel">
          <div class="panel-head">
            <span>{{ currentPath === '/cabinet/aml' ? 'Очередь AML-проверок' : 'Очередь подозрительных операций' }}</span>
            <small>{{ currentPath === '/cabinet/aml' ? 'Комплаенс и риск-факторы' : 'Устройство, IP и поведение' }}</small>
          </div>
          <label class="table-filter">
            Поиск по очереди
            <input v-model="reviewSearch" placeholder="Клиент, получатель, назначение, риск или ID перевода" />
          </label>
          <div v-if="currentPath === '/cabinet/aml'" class="table aml-review-table">
            <div class="row header"><span>Дата</span><span>Клиент</span><span>Сумма</span><span>Получатель</span><span>Назначение</span><span>AML-риск</span><span>Решение</span><span>Действие</span></div>
            <div v-for="transfer in filteredReviewTransfers" :key="transfer.id" class="row">
              <span>{{ date(transfer.createdAt) }}</span>
              <span>{{ dashboard.customer?.fullName }}</span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span><strong>{{ transfer.recipient.name }}</strong><small>{{ recipientDetails(transfer) }}</small></span>
              <span>{{ transfer.purpose }}</span>
              <span>{{ dashboard.customer?.amlRiskLevel || 'Низкий' }}</span>
              <span>{{ riskDecision(transfer, 'AML') }}</span>
              <span class="row-actions"><button class="ghost" @click="selectTransfer(transfer)">Открыть</button></span>
            </div>
            <div v-if="filteredReviewTransfers.length === 0" class="row table-empty">
              <span>Нет AML-проверок по выбранному поиску.</span>
            </div>
          </div>
          <div v-else class="table fraud-review-table">
            <div class="row header"><span>Дата</span><span>Клиент</span><span>Сумма</span><span>Получатель</span><span>Устройство</span><span>IP</span><span>Fraud-риск</span><span>Статус</span><span>Действие</span></div>
            <div v-for="transfer in filteredReviewTransfers" :key="transfer.id" class="row">
              <span>{{ date(transfer.createdAt) }}</span>
              <span>{{ dashboard.customer?.fullName }}</span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span><strong>{{ transfer.recipient.name }}</strong><small>{{ recipientDetails(transfer) }}</small></span>
              <span>{{ fraudDeviceLabel(transfer) }}</span>
              <span>{{ fraudIpLabel(transfer) }}</span>
              <span>{{ fraudRiskLevel(transfer) }}</span>
              <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
              <span class="row-actions"><button class="ghost" @click="selectTransfer(transfer)">Открыть</button></span>
            </div>
            <div v-if="filteredReviewTransfers.length === 0" class="row table-empty">
              <span>Нет антифрод-проверок по выбранному поиску.</span>
            </div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>{{ currentPath === '/cabinet/aml' ? 'Решение AML' : 'Решение антифрода' }}</span></div>
          <label>Комментарий <input v-model="decisionDetails" /></label>
          <div class="decision-list">
            <div v-for="transfer in dashboard.transfers.slice(0, 4)" :key="transfer.id" class="decision-card">
              <strong>{{ transfer.recipient.name }}</strong>
              <small>{{ recipientDetails(transfer) }}</small>
              <span>{{ money(transfer.amount) }} · {{ statusLabel(transfer.status) }}</span>
              <div class="actions">
                <button class="ghost" :disabled="busy" @click="submitRiskDecision(transfer, 'Allow')">Разрешить</button>
                <button class="ghost" :disabled="busy" @click="submitRiskDecision(transfer, 'ManualReview')">
                  {{ currentPath === '/cabinet/aml' ? 'Ручная проверка' : 'Step-up' }}
                </button>
                <button class="ghost danger-action" :disabled="busy" @click="submitRiskDecision(transfer, 'Block')">Заблокировать</button>
                <button v-if="currentPath === '/cabinet/aml'" class="ghost" :disabled="busy">Запросить документы</button>
                <button v-if="currentPath === '/cabinet/fraud'" class="ghost" :disabled="busy">Передать в поддержку</button>
              </div>
            </div>
          </div>
        </article>
      </section>

      <article v-if="selectedTransfer && currentPath !== '/cabinet/client'" class="panel transfer-details">
        <div class="panel-head">
          <span>Карточка перевода</span>
          <small>{{ currentPath === '/cabinet/support' ? '/cabinet/support/transfers/' : currentPath === '/cabinet/aml' ? '/cabinet/aml/reviews/' : currentPath === '/cabinet/fraud' ? '/cabinet/fraud/reviews/' : currentPath === '/cabinet/audit' ? '/cabinet/audit/events/' : '/cabinet/admin/operations/' }}{{ selectedTransfer.id }}</small>
        </div>
        <div class="details-grid">
          <section>
            <h3>Основная информация</h3>
            <div class="readonly-grid">
              <span>ID перевода</span><b>{{ selectedTransfer.id }}</b>
              <span>Дата создания</span><b>{{ date(selectedTransfer.createdAt) }}</b>
              <span>Дата обновления</span><b>{{ date(selectedTransfer.updatedAt || selectedTransfer.createdAt) }}</b>
              <span>Статус</span><b><span class="badge" :class="statusClass(selectedTransfer.status)">{{ statusLabel(selectedTransfer.status) }}</span></b>
              <span>Клиент</span><b>{{ dashboard.customer?.fullName || selectedTransfer.customerId }}</b>
              <span>Сумма</span><b>{{ money(selectedTransfer.amount) }}</b>
              <span>Назначение</span><b>{{ selectedTransfer.purpose }}</b>
            </div>
          </section>
          <section>
            <h3>Получатель</h3>
            <div class="readonly-grid">
              <span>Тип</span><b>{{ selectedTransfer.recipient.type }}</b>
              <span>Имя</span><b>{{ selectedTransfer.recipient.name }}</b>
              <span>Счет</span><b>{{ maskAccount(selectedTransfer.recipient.accountNumber) }}</b>
              <span>БИК</span><b>{{ selectedTransfer.recipient.bankBic }}</b>
              <span>Банк</span><b>SavranPay Банк</b>
            </div>
          </section>
          <section>
            <h3>Отправитель</h3>
            <div class="readonly-grid">
              <span>Customer ID</span><b>{{ selectedTransfer.customerId }}</b>
              <span>Счет списания</span><b>{{ selectedTransferAccount?.maskedNumber || maskAccount(selectedTransferAccount?.number) }}</b>
              <span>Доступно сейчас</span><b>{{ money(selectedTransferAccount?.availableBalance) }}</b>
              <span>Зарезервировано</span><b>{{ money(selectedTransferAccount?.reservedBalance) }}</b>
            </div>
          </section>
          <section>
            <h3>Проверки</h3>
            <div class="timeline compact-timeline">
              <div v-for="check in selectedTransferChecks" :key="check.id">
                <strong>{{ check.checkType }} · {{ check.decision }}</strong>
                <span>{{ check.details }}</span>
                <small>{{ date(check.createdAt) }}</small>
              </div>
              <div v-if="selectedTransferChecks.length === 0">
                <strong>Проверка безопасности</strong>
                <span>Решения AML/Fraud еще не записаны.</span>
              </div>
            </div>
          </section>
          <section v-if="currentPath === '/cabinet/aml' || currentPath === '/cabinet/fraud'">
            <h3>Риск-факторы</h3>
            <div class="timeline compact-timeline">
              <div v-for="factor in riskFactors(selectedTransfer)" :key="factor">
                <strong>{{ currentPath === '/cabinet/aml' ? 'AML' : 'Fraud' }}</strong>
                <span>{{ factor }}</span>
              </div>
            </div>
          </section>
          <section v-if="currentPath === '/cabinet/fraud'">
            <h3>Устройство / IP / поведение</h3>
            <div class="readonly-grid">
              <span>Устройство</span><b>{{ fraudDeviceLabel(selectedTransfer) }}</b>
              <span>IP</span><b>{{ fraudIpLabel(selectedTransfer) }}</b>
              <span>Fraud-риск</span><b>{{ fraudRiskLevel(selectedTransfer) }}</b>
              <span>Поведение</span><b>{{ selectedTransfer.status === 'PendingClientConfirmation' ? 'Требуется step-up' : 'Без критичных отклонений' }}</b>
            </div>
          </section>
          <section v-if="currentPath === '/cabinet/fraud'" class="wide-detail">
            <h3>История операций клиента</h3>
            <div class="table compact fraud-history-table">
              <div class="row header"><span>Дата</span><span>Получатель</span><span>Сумма</span><span>Статус</span></div>
              <div v-for="transfer in dashboard.transfers.slice(0, 5)" :key="transfer.id" class="row">
                <span>{{ date(transfer.createdAt) }}</span>
                <span>{{ transfer.recipient.name }}</span>
                <strong>{{ money(transfer.amount) }}</strong>
                <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
              </div>
            </div>
          </section>
          <section class="wide-detail">
            <h3>Действия</h3>
            <div v-if="currentPath === '/cabinet/support'" class="support-tools service-actions">
              <label>Статус обращения
                <select v-model="supportClaimForm.status">
                  <option>Открыто</option>
                  <option>В работе</option>
                  <option>Ожидает клиента</option>
                  <option>Передано в AML</option>
                  <option>Передано в Fraud</option>
                  <option>Передано администратору</option>
                  <option>Закрыто</option>
                </select>
              </label>
              <label>Категория
                <select v-model="supportClaimForm.category">
                  <option>Оспаривание операции</option>
                  <option>Ошибка исполнения</option>
                  <option>Подозрение на мошенничество</option>
                  <option>Запрос документов</option>
                </select>
              </label>
              <label>Передать в
                <select v-model="supportClaimForm.assignedTo">
                  <option value="Support">Поддержка</option>
                  <option value="AML">AML</option>
                  <option value="Fraud">Fraud</option>
                  <option value="Admin">Admin</option>
                </select>
              </label>
              <label>Комментарий поддержки <input v-model="supportClaimForm.comment" placeholder="Комментарий к спору или обращению" /></label>
              <label>Контактный комментарий клиента <input v-model="supportClaimForm.contactComment" placeholder="Например: клиент просит перезвонить после 18:00" /></label>
              <div class="actions">
                <button class="ghost" type="button" @click="saveSupportClaim(selectedTransfer.id)">Сохранить обращение</button>
                <button class="ghost" type="button" @click="disputeTransfer(selectedTransfer.id)">Создать спор</button>
              </div>
            </div>
            <div v-if="currentPath === '/cabinet/aml' || currentPath === '/cabinet/fraud'" class="actions">
              <button class="ghost" :disabled="busy" @click="submitRiskDecision(selectedTransfer, 'Allow')">Разрешить</button>
              <button class="ghost" :disabled="busy" @click="submitRiskDecision(selectedTransfer, 'ManualReview')">
                {{ currentPath === '/cabinet/aml' ? 'Ручная проверка' : 'Step-up' }}
              </button>
              <button class="ghost danger-action" :disabled="busy" @click="submitRiskDecision(selectedTransfer, 'Block')">Заблокировать</button>
              <button v-if="currentPath === '/cabinet/aml'" class="ghost" :disabled="busy">Запросить документы</button>
              <button v-if="currentPath === '/cabinet/fraud'" class="ghost" :disabled="busy">Передать в поддержку</button>
            </div>
            <div v-if="currentPath === '/cabinet/admin'" class="actions">
              <button class="ghost" :disabled="busy" @click="retrySelectedTransfer(selectedTransfer)">Retry processing</button>
              <button class="ghost" :disabled="busy" @click="loadTechnicalDetails(selectedTransfer)">Technical details</button>
              <button class="ghost" disabled>Повторить обработку</button>
              <button class="ghost" disabled>Разблокировать операцию</button>
              <button class="ghost danger-action" disabled>Заблокировать пользователя</button>
            </div>
            <p v-if="currentPath === '/cabinet/audit'" class="muted">Аудитор может только просматривать карточку, историю, ledger и audit events.</p>
          </section>
          <section class="wide-detail">
            <h3>История статусов</h3>
            <div class="timeline status-timeline">
              <div v-for="item in transferTimeline(selectedTransfer)" :key="item.label">
                <strong>{{ date(item.at) }}</strong>
                <span>{{ item.label }}</span>
              </div>
            </div>
          </section>
        </div>
      </article>

      <section v-if="currentPath === '/cabinet/admin'" class="grid overview-grid">
        <article class="panel">
          <div class="panel-head"><span>Системная сводка</span><small>Управление без прямого изменения ledger</small></div>
          <div class="metric-list">
            <div><span>Активные пользователи</span><strong>{{ activeAdminUsers }}</strong></div>
            <div><span>Заблокированные пользователи</span><strong>{{ blockedAdminUsers }}</strong></div>
            <div><span>Активные счета</span><strong>{{ accountsInWork }}</strong></div>
            <div><span>Операции в журнале</span><strong>{{ dashboard.transfers.length }}</strong></div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>Роли и доступ</span><small>Назначение ролей выполняется отдельными admin-действиями</small></div>
          <div class="chips role-chips">
            <span v-for="role in adminRoles" :key="role">{{ role }}</span>
            <span v-if="adminRoles.length === 0">Роли появятся после загрузки пользователей</span>
          </div>
        </article>

        <article class="panel wide">
          <div class="panel-head">
            <span>Пользователи и роли</span>
            <button class="ghost" @click="loadRoleData">Обновить</button>
          </div>
          <div class="table users-table">
            <div class="row header"><span>Пользователь</span><span>Роли</span><span>Управление ролями</span><span>Статус</span><span>CustomerId</span><span>Создан</span><span>Действие</span></div>
            <div v-for="item in adminUsers" :key="item.id" class="row">
              <span><strong>{{ item.fullName }}</strong><small>{{ item.login }}</small></span>
              <span>{{ item.roles.join(', ') }}</span>
              <span class="role-toggle-list">
                <button
                  v-for="role in availableRoles"
                  :key="role"
                  class="role-toggle"
                  :class="{ active: item.roles.includes(role) }"
                  type="button"
                  :disabled="busy"
                  @click="toggleAdminRole(item, role)"
                >
                  {{ role }}
                </button>
              </span>
              <span class="badge" :class="item.isActive ? 'success' : 'danger'">{{ item.isActive ? 'Активен' : 'Заблокирован' }}</span>
              <span>{{ item.customerId || '—' }}</span>
              <span>{{ date(item.createdAt) }}</span>
              <button class="ghost" :disabled="busy" @click="toggleUser(item)">
                {{ item.isActive ? 'Блокировать' : 'Разблокировать' }}
              </button>
            </div>
          </div>
        </article>

        <article class="panel wide">
          <div class="panel-head"><span>Счета</span><small>Только просмотр: номер, валюта, баланс, статус</small></div>
          <div class="table admin-accounts-table">
            <div class="row header"><span>Счет</span><span>CustomerId</span><span>Валюта</span><span>Доступно</span><span>Зарезервировано</span><span>Статус</span></div>
            <div v-for="account in dashboard.accounts" :key="account.id" class="row">
              <span>{{ account.maskedNumber || maskAccount(account.number) }}</span>
              <span>{{ account.customerId }}</span>
              <span>{{ account.availableBalance.currency }}</span>
              <strong>{{ money(account.availableBalance) }}</strong>
              <span>{{ money(account.reservedBalance) }}</span>
              <span class="badge" :class="account.status === 'Active' ? 'success' : 'danger'">{{ accountStatusLabel(account.status) }}</span>
            </div>
          </div>
        </article>

        <article class="panel wide">
          <div class="panel-head"><span>Операции</span><small>Администратор может смотреть техническую обработку, сумма не редактируется</small></div>
          <div class="table admin-operations-table">
            <div class="row header"><span>Дата</span><span>ID</span><span>Получатель</span><span>Сумма</span><span>Статус</span><span>Риск</span><span>Действие</span></div>
            <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
              <span>{{ date(transfer.createdAt) }}</span>
              <span>{{ transfer.id }}</span>
              <span><strong>{{ transfer.recipient.name }}</strong><small>{{ recipientDetails(transfer) }}</small></span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
              <span>{{ riskSummary(transfer) }}</span>
              <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
            </div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>Системные лимиты</span><small>Просмотр текущих правил</small></div>
          <div class="limits">
            <div v-for="limit in dashboard.limits" :key="limit.name">
              <strong>{{ limit.name }}</strong>
              <span>{{ limit.value }}</span>
            </div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>Health / metrics</span><small>Краткий статус контура</small></div>
          <div class="readonly-grid">
            <span>API</span><b>Готов к работе</b>
            <span>Auth</span><b>JWT + refresh token</b>
            <span>Audit events</span><b>{{ dashboard.auditEvents.length }}</b>
            <span>Risk checks</span><b>{{ dashboard.riskChecks.length }}</b>
          </div>
        </article>
      </section>

      <section v-if="currentPath === '/cabinet/audit'" class="grid two">
        <article class="panel">
          <div class="panel-head"><span>Ledger</span><small>Только просмотр, фильтр и сверка проводок</small></div>
          <label class="table-filter">Поиск по ledger <input v-model="ledgerSearch" placeholder="ID перевода, счет или валюта" /></label>
          <div class="table ledger-table">
            <div class="row header"><span>Дата</span><span>Перевод</span><span>Счет</span><span>Дебет</span><span>Кредит</span></div>
            <div v-for="entry in filteredLedgerEntries" :key="entry.id" class="row">
              <span>{{ date(entry.createdAt) }}</span>
              <span><strong>{{ ledgerTransferLabel(entry.transferId) }}</strong><small>{{ entry.transferId }}</small></span>
              <span>{{ maskAccount(entry.accountNumber) }}</span>
              <span>{{ minor(entry.debitMinorUnits, entry.currency) }}</span>
              <span>{{ minor(entry.creditMinorUnits, entry.currency) }}</span>
            </div>
            <div v-if="filteredLedgerEntries.length === 0" class="row table-empty">
              <span>Нет ledger-записей по выбранному поиску.</span>
            </div>
          </div>
        </article>
        <article class="panel wide">
          <div class="panel-head">
            <span>Журнал аудита</span>
            <button class="ghost" type="button" :disabled="filteredAuditEvents.length === 0" @click="exportAuditEvents">Экспорт CSV</button>
          </div>
          <label class="table-filter">Поиск по событиям <input v-model="auditSearch" placeholder="Тип события, объект, роль или сообщение" /></label>
          <div class="table audit-events-table">
            <div class="row header"><span>Дата</span><span>Пользователь</span><span>Роль</span><span>Тип события</span><span>Объект</span><span>Сообщение</span><span>IP/User-Agent</span><span>Действие</span></div>
            <div v-for="event in filteredAuditEvents" :key="event.id" class="row">
              <span>{{ date(event.createdAt) }}</span>
              <span>system</span>
              <span>{{ auditRole(event.eventType) }}</span>
              <strong>{{ event.eventType }}</strong>
              <span>{{ auditObjectLabel(event.operationId) }}</span>
              <span>{{ event.message }}</span>
              <span>—</span>
              <span class="row-actions">
                <button v-if="transferById(event.operationId)" class="ghost" @click="selectTransfer(transferById(event.operationId)!)">Открыть</button>
                <button v-else class="ghost" disabled>Открыть</button>
              </span>
            </div>
            <div v-if="filteredAuditEvents.length === 0" class="row table-empty">
              <span>Нет audit events по выбранному поиску.</span>
            </div>
          </div>
        </article>
      </section>
    </template>
  </main>
</template>
