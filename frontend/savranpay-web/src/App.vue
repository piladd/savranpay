<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  confirmTransfer,
  createTransfer,
  getAccessToken,
  getAdminUsers,
  getConfirmationChallenge,
  getCurrentUser,
  getDashboard,
  login,
  logout,
  recordRiskDecision,
  reportUnauthorizedClaim,
  setAdminUserActive,
  type AccountView,
  type AdminUserView,
  type AuthUser,
  type DashboardView,
  type TransferView,
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
const selectedTransfer = ref<TransferView | null>(null)
const dashboard = ref<DashboardView>({ ...emptyDashboard })
const adminUsers = ref<AdminUserView[]>([])
const decisionDetails = ref('Проверено вручную, замечания внесены в журнал аудита')

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

const activeCabinet = computed(() => cabinets.find((cabinet) => cabinet.path === currentPath.value) ?? cabinets[0])
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
const riskKind = computed(() => (currentPath.value === '/cabinet/fraud' ? 'fraud' : 'aml'))

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
}

async function loadRoleData() {
  if (currentPath.value === '/cabinet/admin' && getAccessToken() && canUseCurrentCabinet.value) {
    adminUsers.value = await getAdminUsers()
  }
}

async function submitTransfer() {
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

    await loadDashboard()
    selectedTransfer.value = dashboard.value.transfers.find((transfer) => transfer.id === result.transferId) ?? null
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

    await loadDashboard()
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
    await loadDashboard()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Не удалось создать заявление.'
  } finally {
    busy.value = false
  }
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
  navigate('/cabinet/client')
}

function selectTransfer(transfer: TransferView) {
  selectedTransfer.value = transfer
  lastChallenge.value = ''
}

function normalizePath(path: string) {
  return cabinets.some((cabinet) => cabinet.path === path) ? path : '/cabinet/client'
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
  if (status === 'Failed' || status === 'Disputed' || status === 'Blocked') return 'danger'
  return 'warn'
}

function accountLabel(account: AccountView) {
  return `${account.number} · ${money(account.availableBalance)}`
}

function recipientDetails(transfer: TransferView) {
  return `${transfer.recipient.accountNumber} · БИК ${transfer.recipient.bankBic}`
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

      <section v-if="currentPath === '/cabinet/client'" class="grid transfer-grid">
        <form class="panel transfer-form" @submit.prevent="submitTransfer">
          <div class="panel-head">
            <span>Новый перевод</span>
            <button class="ghost" type="button" @click="loadDashboard">Обновить</button>
          </div>

          <label>
            Счет списания
            <select v-model="form.fromAccountId" required>
              <option v-for="account in dashboard.accounts" :key="account.id" :value="account.id">
                {{ accountLabel(account) }}
              </option>
            </select>
          </label>

          <label>Счет получателя <input v-model="form.recipient.accountNumber" required maxlength="20" /></label>
          <label>БИК банка <input v-model="form.recipient.bankBic" required maxlength="9" /></label>
          <label>Получатель <input v-model="form.recipient.name" required /></label>
          <label>Сумма, RUB <input v-model.number="form.amountRub" type="number" min="1" step="1" required /></label>
          <label>Назначение платежа <input v-model="form.purpose" required /></label>

          <button class="primary" type="submit" :disabled="busy">Создать распоряжение</button>
        </form>

        <article class="panel confirmation">
          <div class="panel-head">
            <span>Подтверждение</span>
            <small>WebCrypto demo, HSM/KMS в production</small>
          </div>

          <template v-if="selectedTransfer">
            <div class="confirm-card">
              <div class="detail-line">
                <b>Получатель</b>
                <span>{{ selectedTransfer.recipient.name }}</span>
              </div>
              <div class="detail-line">
                <b>Счет</b>
                <span>{{ selectedTransfer.recipient.accountNumber }}</span>
              </div>
              <div class="detail-line">
                <b>БИК</b>
                <span>{{ selectedTransfer.recipient.bankBic }}</span>
              </div>
              <div class="detail-line">
                <b>Сумма</b>
                <span>{{ money(selectedTransfer.amount) }}</span>
              </div>
              <span class="badge" :class="statusClass(selectedTransfer.status)">{{ selectedTransfer.status }}</span>
            </div>
            <button
              class="primary"
              :disabled="busy || selectedTransfer.status !== 'PendingClientConfirmation'"
              @click="signAndConfirm(selectedTransfer.id)"
            >
              Подписать и подтвердить
            </button>
            <button class="ghost" :disabled="busy" @click="disputeTransfer(selectedTransfer.id)">
              Заявить об операции без согласия
            </button>
            <pre v-if="lastChallenge">{{ lastChallenge }}</pre>
          </template>
          <p v-else class="muted">Создайте перевод или выберите ожидающий подтверждения.</p>
        </article>

        <article class="panel wide">
          <div class="panel-head"><span>Мои счета</span></div>
          <div class="accounts">
            <div v-for="account in dashboard.accounts" :key="account.id" class="account">
              <span class="account-number">{{ account.number }}</span>
              <strong>{{ money(account.availableBalance) }}</strong>
              <small>{{ account.status }}</small>
            </div>
          </div>
        </article>
      </section>

      <section v-if="currentPath === '/cabinet/support'" class="panel">
        <div class="panel-head"><span>Обращения и операции</span></div>
        <div class="table">
          <div class="row header"><span>Получатель</span><span>Сумма</span><span>Статус</span><span>Дата</span><span></span></div>
          <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
            <span class="recipient-cell">
              <strong>{{ transfer.recipient.name }}</strong>
              <small>{{ recipientDetails(transfer) }}</small>
            </span>
            <strong>{{ money(transfer.amount) }}</strong>
            <span class="badge" :class="statusClass(transfer.status)">{{ transfer.status }}</span>
            <span>{{ date(transfer.createdAt) }}</span>
            <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
          </div>
        </div>
      </section>

      <section v-if="currentPath === '/cabinet/aml' || currentPath === '/cabinet/fraud'" class="grid two">
        <article class="panel">
          <div class="panel-head"><span>Risk checks</span></div>
          <div class="timeline">
            <div v-for="check in dashboard.riskChecks" :key="check.id">
              <strong>{{ check.checkType }} · {{ check.decision }}</strong>
              <span>{{ check.details }}</span>
              <small>{{ date(check.createdAt) }}</small>
            </div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-head"><span>Ручное решение</span></div>
          <label>Комментарий <input v-model="decisionDetails" /></label>
          <div class="decision-list">
            <div v-for="transfer in dashboard.transfers.slice(0, 4)" :key="transfer.id" class="decision-card">
              <strong>{{ transfer.recipient.name }}</strong>
              <small>{{ recipientDetails(transfer) }}</small>
              <span>{{ money(transfer.amount) }} · {{ transfer.status }}</span>
              <div class="actions">
                <button class="ghost" :disabled="busy" @click="submitRiskDecision(transfer, 'Allow')">Allow</button>
                <button class="ghost" :disabled="busy" @click="submitRiskDecision(transfer, 'ManualReview')">Review</button>
                <button class="ghost danger-action" :disabled="busy" @click="submitRiskDecision(transfer, 'Block')">Block</button>
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
            <div class="row header"><span>Пользователь</span><span>Роли</span><span>Статус</span><span>Создан</span><span></span></div>
            <div v-for="item in adminUsers" :key="item.id" class="row">
              <span><strong>{{ item.fullName }}</strong><small>{{ item.login }}</small></span>
              <span>{{ item.roles.join(', ') }}</span>
              <span class="badge" :class="item.isActive ? 'success' : 'danger'">{{ item.isActive ? 'Активен' : 'Заблокирован' }}</span>
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
          <div class="panel-head"><span>Аудит</span></div>
          <div class="timeline">
            <div v-for="event in dashboard.auditEvents" :key="event.id">
              <strong>{{ event.eventType }}</strong>
              <span>{{ event.message }}</span>
              <small>{{ date(event.createdAt) }}</small>
            </div>
          </div>
        </article>
      </section>
    </template>
  </main>
</template>
