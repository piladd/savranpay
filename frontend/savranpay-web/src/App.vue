<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  cancelTransferRequest,
  changePassword,
  confirmTransfer,
  createTransfer,
  getAccessToken,
  getAdminUsers,
  getConfirmationChallenge,
  getCurrentUser,
  getDashboard,
  getSessions,
  login,
  logout,
  recordRiskDecision,
  reportUnauthorizedClaim,
  searchRecipients,
  setAdminUserActive,
  type AccountView,
  type AdminUserView,
  type AuthUser,
  type DashboardView,
  type RecipientSearchResult,
  type TransferView,
  type UserSessionView,
} from './api/savranpayApi'

const demoSecret = 'savranpay-demo-secret-change-in-production'

const cabinets = [
  { path: '/cabinet/client', title: 'Клиент', role: 'Customer', icon: 'К' },
  { path: '/cabinet/support', title: 'Поддержка', role: 'SupportOperator', icon: 'S' },
  { path: '/cabinet/aml', title: 'AML', role: 'AmlOfficer', icon: 'A' },
  { path: '/cabinet/fraud', title: 'Антифрод', role: 'FraudOfficer', icon: 'F' },
  { path: '/cabinet/admin', title: 'Админ', role: 'Admin', icon: 'M' },
  { path: '/cabinet/audit', title: 'Аудит', role: 'Auditor', icon: 'R' },
] as const

const emptyDashboard: DashboardView = {
  customer: null,
  accounts: [],
  transfers: [],
  ledger: [],
  riskChecks: [],
  auditEvents: [],
  notifications: [],
  limits: [],
  compliance: [],
}

const currentPath = ref(normalizePath(window.location.pathname))
const user = ref<AuthUser | null>(null)
const loginForm = ref({ login: 'client@savranpay.local', password: 'Client123!' })
const busy = ref(false)
const error = ref('')
const lastChallenge = ref('')
const recipientSearch = ref('')
const securityMessage = ref('')
const serverRecipientMatches = ref<RecipientSearchResult[]>([])
const selectedTransfer = ref<TransferView | null>(null)
const dashboard = ref<DashboardView>({ ...emptyDashboard })
const adminUsers = ref<AdminUserView[]>([])
const sessions = ref<UserSessionView[]>([])
const decisionDetails = ref('Проверено вручную, замечания внесены в журнал аудита')
const reservedTransferIds = ref<Set<string>>(new Set())

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

const activeCabinet = computed(() => cabinets.find((cabinet) => cabinet.path === currentPath.value) ?? cabinets[0])
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
  return user.value.roles.includes(activeCabinet.value.role) || user.value.roles.includes('Admin')
})
const pageTitle = computed(() => activeCabinet.value.title)
const pageSubtitle = computed(() => `${activeCabinet.value.role} · ${currentPath.value}`)
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

onMounted(async () => {
  window.addEventListener('popstate', () => {
    currentPath.value = normalizePath(window.location.pathname)
    void loadRoleData()
  })

  if (getAccessToken()) {
    try {
      user.value = await getCurrentUser()
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

  if (!form.value.fromAccountId && dashboard.value.accounts.length > 0) {
    form.value.fromAccountId = dashboard.value.accounts[0].id
  }

  selectedTransfer.value =
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
    navigate('/cabinet/client')
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
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось создать перевод.'
  } finally {
    busy.value = false
  }
}

async function signAndConfirm(transferId: string) {
  busy.value = true
  error.value = ''
  try {
    const challenge = await getConfirmationChallenge(transferId)
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
    await reportUnauthorizedClaim(transferId)
    markTransferStatus(transferId, 'Disputed')
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось создать заявление.'
  } finally {
    busy.value = false
  }
}

async function cancelTransfer(transferId: string) {
  busy.value = true
  error.value = ''
  const transfer = dashboard.value.transfers.find((item) => item.id === transferId)
  try {
    await cancelTransferRequest(transferId).catch(() => undefined)
    if (transfer && reservedTransferIds.value.has(transferId)) {
      adjustAccountBalance(transfer.fromAccountId, transfer.amount.minorUnits, -transfer.amount.minorUnits)
      const next = new Set(reservedTransferIds.value)
      next.delete(transferId)
      reservedTransferIds.value = next
    }
    markTransferStatus(transferId, 'Cancelled')
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

async function signPayload(payload: string) {
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
  navigate('/cabinet/client')
}

function selectTransfer(transfer: TransferView) {
  selectedTransfer.value = transfer
  lastChallenge.value = ''
  if (currentPath.value === '/cabinet/client') {
    window.history.pushState({}, '', `/cabinet/client/transfers/${transfer.id}`)
  }
}

function normalizePath(path: string) {
  const direct = cabinets.find((cabinet) => cabinet.path === path)
  if (direct) return direct.path
  if (path.startsWith('/cabinet/client/transfers/')) return '/cabinet/client'
  if (path.startsWith('/cabinet/support/transfers/')) return '/cabinet/support'
  if (path.startsWith('/cabinet/aml/reviews/')) return '/cabinet/aml'
  if (path.startsWith('/cabinet/fraud/reviews/')) return '/cabinet/fraud'
  return '/cabinet/client'
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
  if (status === 'Confirmed' || status === 'Processing' || status === 'PendingClientConfirmation') return 'info'
  if (status === 'Failed' || status === 'Blocked') return 'danger'
  if (status === 'Disputed') return 'disputed'
  if (status === 'Cancelled') return 'neutral'
  return 'warn'
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Created: 'Создан',
    PendingValidation: 'Проверяется',
    PendingClientConfirmation: 'Ожидает подтверждения',
    Confirmed: 'Подтвержден',
    Processing: 'Исполняется',
    Settled: 'Исполнен',
    Failed: 'Ошибка',
    Disputed: 'Оспорен',
    Blocked: 'Заблокирован',
    ManualReview: 'Ручная проверка',
    Cancelled: 'Отменен',
  }
  return labels[status] ?? status
}

function accountStatusLabel(status: string) {
  const labels: Record<string, string> = { Active: 'Активен', Closed: 'Закрыт', Blocked: 'Заблокирован' }
  return labels[status] ?? status
}

function accountLabel(account: AccountView) {
  return `${account.number} · ${money(account.availableBalance)}`
}

function recipientDetails(transfer: TransferView) {
  return `${transfer.recipient.accountNumber} · БИК ${transfer.recipient.bankBic}`
}

function riskSummary(transfer: TransferView) {
  const checks = dashboard.value.riskChecks.filter((check) => check.transferId === transfer.id)
  if (checks.length === 0) return 'Проверяется'
  return checks.map((check) => `${check.checkType}: ${check.decision}`).join(' · ')
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
  if (['Settled', 'Confirmed', 'Processing'].includes(transfer.status)) {
    items.push({ label: 'Клиент подтвердил перевод', at: transfer.updatedAt ?? transfer.createdAt })
    items.push({ label: 'Перевод исполнен', at: transfer.updatedAt ?? transfer.createdAt })
  }
  if (transfer.status === 'Disputed') items.push({ label: 'Клиент оспорил операцию', at: transfer.updatedAt ?? transfer.createdAt })
  if (transfer.status === 'Cancelled') items.push({ label: 'Перевод отменен до подтверждения', at: transfer.updatedAt ?? transfer.createdAt })
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
          <span>Вход</span>
          <small>client@savranpay.local / Client123!</small>
        </div>
        <label>Логин <input v-model="loginForm.login" autocomplete="username" required /></label>
        <label>Пароль <input v-model="loginForm.password" autocomplete="current-password" type="password" required /></label>
        <button class="primary" type="submit" :disabled="busy">Войти</button>
      </form>

      <article class="panel role-card">
        <div class="panel-head"><span>Контуры доступа</span></div>
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
              <strong class="account-number">{{ account.number }}</strong>
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
                <div class="detail-line"><b>Счет получателя</b><span>{{ selectedTransfer.recipient.accountNumber }}</span></div>
                <div class="detail-line"><b>БИК</b><span>{{ selectedTransfer.recipient.bankBic }}</span></div>
                <div class="detail-line"><b>Счет списания</b><span>{{ selectedTransferAccount?.number }}</span></div>
                <div class="detail-line"><b>Сумма</b><span>{{ money(selectedTransfer.amount) }}</span></div>
                <div class="detail-line"><b>Комиссия</b><span>{{ minor(0, selectedTransfer.amount.currency) }}</span></div>
                <div class="detail-line"><b>Итого</b><span>{{ money(selectedTransfer.amount) }}</span></div>
                <div class="detail-line"><b>Назначение</b><span>{{ selectedTransfer.purpose }}</span></div>
                <div class="detail-line"><b>Статус</b><span class="badge" :class="statusClass(selectedTransfer.status)">{{ statusLabel(selectedTransfer.status) }}</span></div>
              </div>
              <div class="actions">
                <button
                  class="primary"
                  :disabled="busy || selectedTransfer.status !== 'PendingClientConfirmation'"
                  @click="signAndConfirm(selectedTransfer.id)"
                >
                  Подписать и подтвердить
                </button>
                <button class="ghost" :disabled="busy || selectedTransfer.status !== 'PendingClientConfirmation'" @click="cancelTransfer(selectedTransfer.id)">
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
              <span>{{ transfer.recipient.accountNumber }}</span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
              <span>{{ transfer.purpose }}</span>
              <span class="row-actions">
                <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
                <button v-if="transfer.status === 'PendingClientConfirmation'" class="ghost" @click="selectTransfer(transfer)">Подтвердить</button>
                <button v-if="transfer.status === 'Settled' || transfer.status === 'Processing'" class="ghost" @click="disputeTransfer(transfer.id)">Оспорить</button>
                <button v-if="transfer.status === 'Settled' || transfer.status === 'Failed' || transfer.status === 'Cancelled'" class="ghost" @click="repeatTransfer(transfer)">Повторить</button>
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
                <span>Статус</span><b>{{ statusLabel(selectedTransfer.status) }}</b>
                <span>Сумма</span><b>{{ money(selectedTransfer.amount) }}</b>
                <span>Назначение</span><b>{{ selectedTransfer.purpose }}</b>
              </div>
            </section>
            <section>
              <h3>Отправитель</h3>
              <div class="readonly-grid">
                <span>ФИО клиента</span><b>{{ dashboard.customer?.fullName }}</b>
                <span>Customer ID</span><b>{{ selectedTransfer.customerId }}</b>
                <span>Счет списания</span><b>{{ selectedTransferAccount?.number }}</b>
              </div>
            </section>
            <section>
              <h3>Получатель</h3>
              <div class="readonly-grid">
                <span>Тип</span><b>{{ selectedTransfer.recipient.type }}</b>
                <span>Имя</span><b>{{ selectedTransfer.recipient.name }}</b>
                <span>Счет</span><b>{{ selectedTransfer.recipient.accountNumber }}</b>
                <span>БИК</span><b>{{ selectedTransfer.recipient.bankBic }}</b>
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
          </div>
        </article>
      </section>

      <section v-if="currentPath === '/cabinet/support'" class="panel">
        <div class="panel-head"><span>Поддержка: операции и спорные переводы</span><small>Поддержка не меняет сумму, получателя и ledger</small></div>
        <div class="support-tools">
          <label>Поиск клиента <input placeholder="ФИО, email или Customer ID" /></label>
          <label>Комментарий поддержки <input placeholder="Комментарий к обращению" /></label>
        </div>
        <div class="table support-table">
          <div class="row header"><span>Дата</span><span>Клиент</span><span>Получатель</span><span>Сумма</span><span>Статус</span><span>Риск</span><span>Спор</span><span>Действие</span></div>
          <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
            <span>{{ date(transfer.createdAt) }}</span>
            <span><strong>{{ dashboard.customer?.fullName }}</strong><small>{{ dashboard.customer?.email }}</small></span>
            <span class="recipient-cell">
              <strong>{{ transfer.recipient.name }}</strong>
              <small>{{ recipientDetails(transfer) }}</small>
            </span>
            <strong>{{ money(transfer.amount) }}</strong>
            <span class="badge" :class="statusClass(transfer.status)">{{ statusLabel(transfer.status) }}</span>
            <span>{{ riskSummary(transfer) }}</span>
            <span>{{ transfer.status === 'Disputed' ? 'Да' : 'Нет' }}</span>
            <span class="row-actions"><button class="ghost" @click="selectTransfer(transfer)">Открыть</button></span>
          </div>
        </div>
      </section>

      <section v-if="currentPath === '/cabinet/aml' || currentPath === '/cabinet/fraud'" class="grid two">
        <article class="panel">
          <div class="panel-head">
            <span>{{ currentPath === '/cabinet/aml' ? 'Очередь AML-проверок' : 'Очередь подозрительных операций' }}</span>
            <small>{{ currentPath === '/cabinet/aml' ? 'Комплаенс и риск-факторы' : 'Устройство, IP и поведение' }}</small>
          </div>
          <div class="table review-table">
            <div class="row header"><span>Дата</span><span>Клиент</span><span>Сумма</span><span>Получатель</span><span>Риск</span><span>Решение</span><span></span></div>
            <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
              <span>{{ date(transfer.createdAt) }}</span>
              <span>{{ dashboard.customer?.fullName }}</span>
              <strong>{{ money(transfer.amount) }}</strong>
              <span><strong>{{ transfer.recipient.name }}</strong><small>{{ recipientDetails(transfer) }}</small></span>
              <span>{{ currentPath === '/cabinet/aml' ? dashboard.customer?.amlRiskLevel : 'Low' }}</span>
              <span>{{ riskSummary(transfer) }}</span>
              <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
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

      <section v-if="currentPath === '/cabinet/admin'" class="grid overview-grid">
        <article class="panel wide">
          <div class="panel-head">
            <span>Пользователи и роли</span>
            <button class="ghost" @click="loadRoleData">Обновить</button>
          </div>
          <div class="table users-table">
            <div class="row header"><span>Пользователь</span><span>Роли</span><span>Статус</span><span>CustomerId</span><span>Создан</span><span>Действие</span></div>
            <div v-for="item in adminUsers" :key="item.id" class="row">
              <span><strong>{{ item.fullName }}</strong><small>{{ item.login }}</small></span>
              <span>{{ item.roles.join(', ') }}</span>
              <span class="badge" :class="item.isActive ? 'success' : 'danger'">{{ item.isActive ? 'Активен' : 'Заблокирован' }}</span>
              <span>{{ item.customerId || '—' }}</span>
              <span>{{ date(item.createdAt) }}</span>
              <button class="ghost" :disabled="busy" @click="toggleUser(item)">
                {{ item.isActive ? 'Блокировать' : 'Разблокировать' }}
              </button>
            </div>
          </div>
        </article>
      </section>

      <section v-if="currentPath === '/cabinet/audit'" class="grid two">
        <article class="panel">
          <div class="panel-head"><span>Ledger</span></div>
          <div class="table compact">
            <div class="row header"><span>Счет</span><span>Дебет</span><span>Кредит</span></div>
            <div v-for="entry in dashboard.ledger" :key="entry.id" class="row">
              <span>{{ entry.accountNumber }}</span>
              <span>{{ minor(entry.debitMinorUnits, entry.currency) }}</span>
              <span>{{ minor(entry.creditMinorUnits, entry.currency) }}</span>
            </div>
          </div>
        </article>
        <article class="panel">
          <div class="panel-head"><span>Журнал аудита</span><small>Только просмотр, поиск и экспорт</small></div>
          <div class="timeline audit-timeline">
            <div v-for="event in dashboard.auditEvents" :key="event.id">
              <strong>{{ event.eventType }}</strong>
              <span>{{ event.message }}</span>
              <span>Объект: {{ event.operationId }}</span>
              <small>{{ date(event.createdAt) }}</small>
            </div>
          </div>
        </article>
      </section>
    </template>
  </main>
</template>
