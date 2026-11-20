# BaristaNotes live companion

Read-only React / TypeScript / Vite dashboard for the same agent API used by
the MAUI app. It refreshes every two seconds, displaying the selected storage
provider, shared journal counts, budget, latest run ledger, coffee picks, and
learned memory. Failed refreshes keep the last successful data visible with
an error banner and retry automatically.

Start through the AppHost:

```bash
aspire start --apphost src/BaristaNotes.AppHost/BaristaNotes.AppHost.csproj
```

Open the `web` endpoint from the Aspire dashboard. To capture browser console
and network activity, use **Open tracked browser** on `web-browser-logs`.
Updates, manual refreshes, connection failures, and recovery emit console
messages there. The dashboard never writes journal or agent data.

The Vite `/api` proxy reads `services__api__http__0` supplied by Aspire.
For standalone frontend development, set `API_HTTP` to a running API endpoint:

```bash
API_HTTP=http://localhost:5048 npm run dev --prefix src/BaristaNotes.Web
npm run build --prefix src/BaristaNotes.Web
npm run lint --prefix src/BaristaNotes.Web
```

See `specs/005-reorder-agent/demo-runbook.md` for storage switching and the
Mac-to-web demo.
