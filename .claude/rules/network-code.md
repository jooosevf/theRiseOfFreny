---
paths:
  - "Assets/Scripts/Online/**/*.cs"
  - "firebase/**"
---

# Network Code Rules

> **No Freny:** o online é Firebase por REST e a API na Vercel (`freny-online`), sem
> partida em tempo real. Predição, rollback, host migration e replicação não se
> aplicam. Valem as regras de servidor autoritativo e de validação: o cliente nunca
> decide a pontuação, e quem valida é `firestore.rules` e a API.

- Server is AUTHORITATIVE for all gameplay-critical state — never trust the client
- All network messages must be versioned for forward/backward compatibility
- Client predicts locally, reconciles with server — implement rollback for mispredictions
- Handle disconnection, reconnection, and host migration gracefully
- Rate-limit all network logging to prevent log flooding
- All networked values must specify replication strategy: reliable/unreliable, frequency, interpolation
- Bandwidth budget: define and track per-message-type bandwidth usage
- Security: validate all incoming packet sizes and field ranges
