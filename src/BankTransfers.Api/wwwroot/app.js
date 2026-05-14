const { createApp } = Vue;

const demoSecret = 'savranpay-demo-secret-change-in-production';

createApp({
  data() {
    return {
      tabs: [
        { id: 'overview', title: 'Обзор', icon: '▦' },
        { id: 'transfer', title: 'Перевод', icon: '→' },
        { id: 'transactions', title: 'Транзакции', icon: '≡' },
        { id: 'risk', title: 'Риски', icon: '◇' },
        { id: 'ledger', title: 'Ledger и аудит', icon: '✓' }
      ],
      activeTab: 'overview',
      dashboard: {
        customer: null,
        accounts: [],
        transfers: [],
        ledger: [],
        riskChecks: [],
        auditEvents: [],
        notifications: [],
        limits: [],
        compliance: []
      },
      form: {
        fromAccountId: '',
        recipient: {
          type: 'Account',
          accountNumber: '40817810000000000002',
          bankBic: '044525225',
          name: 'Иван Петров, накопительный счет'
        },
        amountRub: 1500,
        purpose: 'Перевод собственных средств'
      },
      selectedTransfer: null,
      lastChallenge: '',
      busy: false,
      error: ''
    };
  },
  async mounted() {
    await this.loadDashboard();
  },
  methods: {
    async loadDashboard() {
      this.error = '';
      const response = await fetch('/api/v1/dashboard', { cache: 'no-store' });
      this.dashboard = await response.json();
      if (!this.form.fromAccountId && this.dashboard.accounts.length > 0) {
        this.form.fromAccountId = this.dashboard.accounts[0].id;
      }
      this.selectedTransfer = this.dashboard.transfers.find(item => item.status === 'PendingClientConfirmation') ?? this.selectedTransfer;
    },
    async createTransfer() {
      this.busy = true;
      this.error = '';
      try {
        const request = {
          fromAccountId: this.form.fromAccountId,
          recipient: this.form.recipient,
          amount: {
            minorUnits: Math.round(Number(this.form.amountRub) * 100),
            currency: 'RUB'
          },
          purpose: this.form.purpose
        };

        const response = await fetch('/api/v1/transfers', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Idempotency-Key': crypto.randomUUID()
          },
          body: JSON.stringify(request)
        });

        if (!response.ok) {
          throw new Error(await response.text());
        }

        const result = await response.json();
        await this.loadDashboard();
        this.selectedTransfer = this.dashboard.transfers.find(item => item.id === result.transferId);
        this.activeTab = 'transfer';
      } catch (error) {
        this.error = error.message ?? 'Не удалось создать перевод.';
      } finally {
        this.busy = false;
      }
    },
    selectTransfer(transfer) {
      this.selectedTransfer = transfer;
      this.activeTab = 'transfer';
      this.lastChallenge = '';
    },
    async confirmTransfer(transferId) {
      this.busy = true;
      this.error = '';
      try {
        const challengeResponse = await fetch(`/api/v1/transfers/${transferId}/confirmation-challenge`, { cache: 'no-store' });
        if (!challengeResponse.ok) {
          throw new Error(await challengeResponse.text());
        }

        const challenge = await challengeResponse.json();
        const signature = await this.signPayload(challenge.payload);

        const confirmResponse = await fetch(`/api/v1/transfers/${transferId}/confirm`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            confirmationType: 'TransactionSignature',
            signature,
            nonce: challenge.nonce,
            timestamp: challenge.timestamp
          })
        });

        if (!confirmResponse.ok) {
          throw new Error(await confirmResponse.text());
        }

        this.lastChallenge = JSON.stringify({
          algorithm: challenge.demoAlgorithm,
          payloadHash: challenge.payloadHash,
          nonce: challenge.nonce,
          timestamp: challenge.timestamp,
          note: 'Подписаны сумма, получатель, назначение, nonce и время.'
        }, null, 2);

        await this.loadDashboard();
        this.selectedTransfer = this.dashboard.transfers.find(item => item.id === transferId);
      } catch (error) {
        this.error = error.message ?? 'Не удалось подтвердить перевод.';
      } finally {
        this.busy = false;
      }
    },
    async signPayload(payload) {
      const encoder = new TextEncoder();
      const key = await crypto.subtle.importKey(
        'raw',
        encoder.encode(demoSecret),
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign']
      );
      const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(payload));
      return this.arrayBufferToBase64(signature);
    },
    arrayBufferToBase64(buffer) {
      const bytes = new Uint8Array(buffer);
      let binary = '';
      for (const byte of bytes) {
        binary += String.fromCharCode(byte);
      }
      return btoa(binary);
    },
    money(value) {
      if (!value) return '0,00 RUB';
      return this.minor(value.minorUnits, value.currency);
    },
    minor(minorUnits, currency) {
      return new Intl.NumberFormat('ru-RU', {
        style: 'currency',
        currency: currency || 'RUB'
      }).format((minorUnits || 0) / 100);
    },
    date(value) {
      return new Intl.DateTimeFormat('ru-RU', {
        dateStyle: 'short',
        timeStyle: 'medium'
      }).format(new Date(value));
    },
    statusClass(status) {
      if (status === 'Settled') return 'success';
      if (status === 'Failed' || status === 'Disputed') return 'danger';
      return 'warn';
    }
  }
}).mount('#app');
