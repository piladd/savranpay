type TransferStatus =
  | 'Draft'
  | 'PendingValidation'
  | 'PendingRiskCheck'
  | 'PendingClientConfirmation'
  | 'Accepted'
  | 'Reserved'
  | 'Processing'
  | 'Settled'
  | 'Failed'
  | 'Cancelled'
  | 'Reversed'
  | 'Disputed';

type MoneyDto = {
  minorUnits: number;
  currency: string;
};

type TransferView = {
  id: string;
  recipient: {
    accountNumber: string;
    bankBic: string;
    name: string;
  };
  amount: MoneyDto;
  purpose: string;
  status: TransferStatus;
  createdAt: string;
};

type ConfirmationChallenge = {
  nonce: string;
  timestamp: string;
  payload: string;
  payloadHash: string;
  demoAlgorithm: 'HMAC-SHA-256';
};

// Typed frontend contract for SavranPay.
// The current embedded demo uses app.js; the production frontend is intended to move to Vue 3 + TypeScript + Vite.
