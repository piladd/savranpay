<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  confirmTransfer,
  createTransfer,
  getConfirmationChallenge,
  getDashboard,
  reportUnauthorizedClaim,
  type AccountView,
  type DashboardView,
  type TransferView,
} from './api/savranpayApi'

const demoSecret = 'savranpay-demo-secret-change-in-production'

const tabs = [
  { id: 'overview', title: 'Обзор', icon: '▦' },
  { id: 'transfer', title: 'Перевод', icon: '→' },
  { id: 'transactions', title: 'Транзакции', icon: '≡' },
  { id: 'risk', title: 'Риски', icon: '◇' },
  { id: 'ledger', title: 'Ledger и аудит', icon: '✓' },
] as const

type TabId = (typeof tabs)[number]['id']

const activeTab = ref<TabId>('overview')
const busy = ref(false)
const error = ref('')
const lastChallenge = ref('')
const selectedTransfer = ref<TransferView | null>(null)

const dashboard = ref<DashboardView>({
  customer: null,
  accounts: [],
  transfers: [],
  ledger: [],
  riskChecks: [],
  auditEvents: [],
  notifications: [],
  limits: [],
  compliance: [],
})

const form = ref({
  fromAccountId: '',
  recipient: {
    type: 'Account',
    accountNumber: '40817810000000000002',
    bankBic: '044525225',
    name: 'Иван Петров, накопительный счет',
  },
  amountRub: 1500,
  purpose: 'Перевод собственных средств',
})

const pendingTransfers = computed(() =>
  dashboard.value.transfers.filter((transfer) => transfer.status === 'PendingClientConfirmation'),
)

onMounted(loadDashboard)

async function loadDashboard() {
  error.value = ''
  dashboard.value = await getDashboard()

  if (!form.value.fromAccountId && dashboard.value.accounts.length > 0) {
    form.value.fromAccountId = dashboard.value.accounts[0].id
  }

  selectedTransfer.value =
    dashboard.value.transfers.find((transfer) => transfer.id === selectedTransfer.value?.id) ??
    pendingTransfers.value[0] ??
    selectedTransfer.value
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
    activeTab.value = 'transfer'
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
    selectedTransfer.value = dashboard.value.transfers.find((transfer) => transfer.id === transferId) ?? null
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

function selectTransfer(transfer: TransferView) {
  selectedTransfer.value = transfer
  activeTab.value = 'transfer'
  lastChallenge.value = ''
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
  if (status === 'Failed' || status === 'Disputed') return 'danger'
  return 'warn'
}

function accountLabel(account: AccountView) {
  return `${account.maskedNumber} · ${money(account.availableBalance)}`
}
</script>

<template>
  <aside class="sidebar">
    <div class="brand">
      <div class="brand-mark">SP</div>
      <div>
        <strong>SavranPay</strong>
        <span>Защищенный кабинет</span>
      </div>
    </div>

    <nav>
      <button v-for="tab in tabs" :key="tab.id" :class="{ active: activeTab === tab.id }" @click="activeTab = tab.id">
        <span>{{ tab.icon }}</span>{{ tab.title }}
      </button>
    </nav>

    <div class="security-note">
      <strong>HTTPS + WebCrypto</strong>
      <span>Подтверждение подписывает сумму, получателя, назначение, nonce и время.</span>
    </div>
  </aside>

  <main class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">SavranPay · цифровой сервис переводов</p>
        <h1>Личный кабинет</h1>
      </div>
      <div v-if="dashboard.customer" class="profile">
        <span>{{ dashboard.customer.identificationStatus }}</span>
        <strong>{{ dashboard.customer.fullName }}</strong>
      </div>
    </header>

    <section v-if="error" class="alert">{{ error }}</section>

    <section v-show="activeTab === 'overview'" class="grid overview-grid">
      <article class="panel balance-panel">
        <div class="panel-head">
          <span>Счета</span>
          <button class="ghost" @click="loadDashboard">Обновить</button>
        </div>
        <div class="accounts">
          <div v-for="account in dashboard.accounts" :key="account.id" class="account">
            <span>{{ account.maskedNumber }}</span>
            <strong>{{ money(account.availableBalance) }}</strong>
            <small>{{ account.status }}</small>
          </div>
        </div>
      </article>

      <article class="panel">
        <div class="panel-head"><span>Контроль</span></div>
        <div class="metric-list">
          <div><strong>{{ dashboard.transfers.length }}</strong><span>переводов</span></div>
          <div><strong>{{ dashboard.riskChecks.length }}</strong><span>AML/антифрод</span></div>
          <div><strong>{{ dashboard.auditEvents.length }}</strong><span>событий аудита</span></div>
        </div>
      </article>

      <article class="panel wide">
        <div class="panel-head"><span>Соответствие ТЗ</span></div>
        <div class="chips">
          <span v-for="item in dashboard.compliance" :key="item">{{ item }}</span>
        </div>
      </article>
    </section>

    <section v-show="activeTab === 'transfer'" class="grid transfer-grid">
      <form class="panel transfer-form" @submit.prevent="submitTransfer">
        <div class="panel-head">
          <span>Новый перевод</span>
          <small>Idempotency-Key создается автоматически</small>
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
        <label>БИК банка получателя <input v-model="form.recipient.bankBic" required maxlength="9" /></label>
        <label>Получатель <input v-model="form.recipient.name" required /></label>
        <label>Сумма, RUB <input v-model.number="form.amountRub" type="number" min="1" step="1" required /></label>
        <label>Назначение платежа <input v-model="form.purpose" required /></label>

        <button class="primary" type="submit" :disabled="busy">Создать распоряжение</button>
      </form>

      <article class="panel confirmation">
        <div class="panel-head">
          <span>Криптографическое подтверждение</span>
          <small>HMAC-SHA-256, nonce, timestamp</small>
        </div>

        <template v-if="selectedTransfer">
          <div class="confirm-card">
            <strong>{{ selectedTransfer.recipient.name }}</strong>
            <span>{{ money(selectedTransfer.amount) }} · {{ selectedTransfer.status }}</span>
            <small>{{ selectedTransfer.recipient.accountNumber }}</small>
          </div>
          <button
            class="primary"
            :disabled="busy || selectedTransfer.status !== 'PendingClientConfirmation'"
            @click="signAndConfirm(selectedTransfer.id)"
          >
            Подписать и подтвердить через WebCrypto
          </button>
          <button class="ghost" :disabled="busy" @click="disputeTransfer(selectedTransfer.id)">
            Заявить об операции без согласия
          </button>
          <pre v-if="lastChallenge">{{ lastChallenge }}</pre>
        </template>
        <p v-else class="muted">Создайте перевод или выберите ожидающий подтверждения в журнале.</p>
      </article>
    </section>

    <section v-show="activeTab === 'transactions'" class="panel">
      <div class="panel-head">
        <span>Транзакции</span>
        <small>Жизненный цикл перевода</small>
      </div>
      <div class="table">
        <div class="row header">
          <span>Получатель</span><span>Сумма</span><span>Статус</span><span>Дата</span><span></span>
        </div>
        <div v-for="transfer in dashboard.transfers" :key="transfer.id" class="row">
          <span>{{ transfer.recipient.name }}</span>
          <strong>{{ money(transfer.amount) }}</strong>
          <span class="badge" :class="statusClass(transfer.status)">{{ transfer.status }}</span>
          <span>{{ date(transfer.createdAt) }}</span>
          <button class="ghost" @click="selectTransfer(transfer)">Открыть</button>
        </div>
      </div>
    </section>

    <section v-show="activeTab === 'risk'" class="grid two">
      <article class="panel">
        <div class="panel-head"><span>AML и антифрод</span></div>
        <div class="timeline">
          <div v-for="check in dashboard.riskChecks" :key="check.id">
            <strong>{{ check.checkType }} · {{ check.decision }}</strong>
            <span>{{ check.details }}</span>
            <small>{{ date(check.createdAt) }}</small>
          </div>
        </div>
      </article>
      <article class="panel">
        <div class="panel-head"><span>Лимиты</span></div>
        <div class="limits">
          <div v-for="limit in dashboard.limits" :key="limit.name">
            <span>{{ limit.name }}</span>
            <strong>{{ limit.value }}</strong>
          </div>
        </div>
      </article>
    </section>

    <section v-show="activeTab === 'ledger'" class="grid two">
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
  </main>
</template>
