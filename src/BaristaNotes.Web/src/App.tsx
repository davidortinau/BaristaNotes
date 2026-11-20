import { useEffect, useRef, useState } from 'react'
import './App.css'

interface Run {
  id: string
  status: string
  trigger: string
  startedAt: string
  summary: string | null
  roastersChecked: number
  skipped: number
  picks: number
}

interface Status {
  settings: { autonomy: string; monthlyBudget: number; currency: string }
  spentThisMonth: number
  budgetLeft: number
  latestRun: Run | null
  checkIn: { due: boolean }
  lastSnapshot: { receivedAt: string; beans: number; bags: number; shots: number; guestShots: number } | null
}

interface LedgerEntry {
  id: number
  at: string
  kind: string
  title: string
  detail: string | null
}

interface Proposal {
  id: string
  productTitle: string
  roasterName: string
  forBeanName: string
  price: number
  shipping: number
  currency: string
  status: string
  why: string
}

interface MemoryItem {
  id: string
  statement: string
  evidence: { shotId: number }[]
}

interface Dashboard {
  provider: string
  status: Status
  ledger: LedgerEntry[]
  proposals: Proposal[]
  memory: MemoryItem[]
  syncedAt: Date
}

async function get<T>(path: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(`/api${path}`, { signal })
  if (!response.ok) throw new Error(`${path}: HTTP ${response.status}`)
  return response.json()
}

function money(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(value)
}

function time(value: string | Date) {
  return new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function App() {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)
  const [refreshing, setRefreshing] = useState(true)
  const lastSignature = useRef('')
  const lastError = useRef<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined

    async function sync() {
      try {
        const [storage, status, proposals, memory] = await Promise.all([
          get<{ provider: string }>('/demo/storage', controller.signal),
          get<Status>('/agent/status', controller.signal),
          get<Proposal[]>('/proposals', controller.signal),
          get<{ items: MemoryItem[] }>('/memory', controller.signal),
        ])
        const ledger = status.latestRun
          ? await get<LedgerEntry[]>(`/runs/${status.latestRun.id}/ledger`, controller.signal)
          : []
        if (controller.signal.aborted) return

        const signature = JSON.stringify({ storage, status, proposals, memory, ledger })
        if (signature !== lastSignature.current) {
          console.info('[BaristaNotes] Activity updated', JSON.stringify({
            storage: storage.provider,
            run: status.latestRun?.id,
            status: status.latestRun?.status,
            ledgerEntries: ledger.length,
            picks: proposals.length,
            sharedShots: status.lastSnapshot?.shots ?? 0,
          }))
          lastSignature.current = signature
        }
        if (lastError.current) console.info('[BaristaNotes] API connection restored')
        lastError.current = null
        setData({ provider: storage.provider, status, proposals, memory: memory.items, ledger, syncedAt: new Date() })
        setError(null)
      } catch (cause) {
        if (controller.signal.aborted) return
        const message = cause instanceof Error ? cause.message : String(cause)
        if (message !== lastError.current) console.error('[BaristaNotes] Refresh failed', message)
        lastError.current = message
        setError(message)
      } finally {
        if (!controller.signal.aborted) {
          setRefreshing(false)
          timer = setTimeout(sync, 2000)
        }
      }
    }

    void sync()
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [refresh])

  const status = data?.status
  const run = status?.latestRun
  const snapshot = status?.lastSnapshot
  const currency = status?.settings.currency ?? 'EUR'

  return (
    <main>
      <header className="page-header">
        <div>
          <p className="eyebrow">BaristaNotes / Live companion</p>
          <h1>A little coffee intelligence.</h1>
          <p>Make a change in the app. Watch the same agent activity arrive here.</p>
        </div>
        <div className="connection">
          <span className={`badge ${error ? 'error' : data ? 'live' : ''}`}>
            {error ? 'Reconnecting' : data ? 'Live / every 2 seconds' : 'Connecting'}
          </span>
          <span className="badge" data-testid="storage">
            {data ? data.provider === 'postgres' ? 'PostgreSQL' : 'Local SQLite' : 'Storage pending'}
          </span>
        </div>
      </header>

      {error && (
        <div className="error-banner" role="alert">
          <strong>Could not refresh activity.</strong> {error}. Retrying automatically.
          {data && ` Showing the last successful update from ${time(data.syncedAt)}.`}
        </div>
      )}

      <section className="stats" aria-label="Agent overview">
        <article className="stat">
          <p className="eyebrow">Monthly budget left</p>
          <strong>{status ? money(status.budgetLeft, currency) : '--'}</strong>
          <p>{status ? `${money(status.spentThisMonth, currency)} spent of ${money(status.settings.monthlyBudget, currency)}` : 'Waiting for the API'}</p>
        </article>
        <article className="stat">
          <p className="eyebrow">Shared journal</p>
          <strong>{snapshot ? `${snapshot.shots} drinks` : status ? 'Not shared yet' : '--'}</strong>
          <p>{snapshot ? `${snapshot.beans} beans / ${snapshot.bags} bags / ${snapshot.guestShots} guest drinks` : 'Share your journal from the app'}</p>
        </article>
        <article className="stat">
          <p className="eyebrow">Autonomy</p>
          <strong>{status ? ({ Watch: 'Watch', PrepareCart: 'Prepare carts', ReorderFavorites: 'Reorder favorites' }[status.settings.autonomy] ?? status.settings.autonomy) : '--'}</strong>
          <p>{status ? status.checkIn.due ? 'A cupboard check-in is due' : 'Cupboard check-in is up to date' : 'Waiting for settings'}</p>
        </article>
      </section>

      <div className="dashboard-grid">
        <section className="panel activity">
          <div className="section-header">
            <div>
              <p className="eyebrow">The latest run</p>
              <h2>Agent activity</h2>
            </div>
            {run && <span className={`badge ${run.status === 'Failed' ? 'error' : run.status === 'Running' ? 'live' : ''}`}>{run.status}</span>}
          </div>
          {run ? (
            <>
              <p className="run-summary">{run.summary ?? 'The agent is working. Steps appear here as they happen.'}</p>
              <p className="meta">{time(run.startedAt)} / {run.trigger} / {run.roastersChecked} roasters / {run.skipped} skipped / {run.picks} picks</p>
              <ol className="ledger">
                {data?.ledger.map(entry => (
                  <li key={entry.id}>
                    <span className={`ledger-dot ${entry.kind === 'Failed' ? 'failed' : ''}`} />
                    <div>
                      <p className="meta">{time(entry.at)} / {entry.kind}</p>
                      <h3>{entry.title}</h3>
                      {entry.detail && <p>{entry.detail}</p>}
                    </div>
                  </li>
                ))}
              </ol>
              {data?.ledger.length === 0 && <p className="empty">Waiting for the first step.</p>}
            </>
          ) : (
            <div className="empty">
              <h3>{data ? 'Ready when you are.' : 'Connecting to the agent...'}</h3>
              <p>Use Run now in the app or Run agent in the Aspire dashboard. No sample activity is generated here.</p>
            </div>
          )}
        </section>

        <aside>
          <section className="panel">
            <div className="section-header"><div><p className="eyebrow">From the shared API</p><h2>Coffee picks</h2></div><span className="count">{data?.proposals.length ?? 0}</span></div>
            {data?.proposals.map(proposal => (
              <article className="pick" key={proposal.id}>
                <span className="badge">{proposal.status}</span>
                <h3>{proposal.productTitle}</h3>
                <p className="meta">{proposal.roasterName} / for {proposal.forBeanName}</p>
                <p>{proposal.why}</p>
                <strong>{money(proposal.price + proposal.shipping, proposal.currency)} <small>including shipping</small></strong>
              </article>
            ))}
            {!data?.proposals.length && <p className="empty">Picks will appear after the agent finds coffee for a finished bag.</p>}
          </section>
          <section className="panel">
            <p className="eyebrow">What it learned</p>
            <h2>Memory</h2>
            <ul className="memory">
              {data?.memory.map(item => (
                <li key={item.id}><p>{item.statement}</p><span className="meta">{item.evidence.length} cited drinks</span></li>
              ))}
            </ul>
            {!data?.memory.length && <p className="empty">No learned preferences yet. Manage memory in the app.</p>}
          </section>
        </aside>
      </div>

      <footer>
        <p>{data ? `Last synced ${time(data.syncedAt)}` : 'Waiting for the first update'} / Journal stays on-device; only shared data reaches the agent.</p>
        <button type="button" disabled={refreshing} onClick={() => {
          console.info('[BaristaNotes] Manual refresh requested')
          setRefreshing(true)
          setRefresh(value => value + 1)
        }}>{refreshing ? 'Refreshing...' : 'Refresh now'}</button>
      </footer>
    </main>
  )
}

export default App
