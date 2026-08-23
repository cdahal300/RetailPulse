import {
  AlertTriangle,
  BarChart3,
  Bell,
  ChevronDown,
  CircleDollarSign,
  Clock3,
  CloudOff,
  Download,
  LineChart,
  PackageCheck,
  RefreshCw,
  ShieldCheck,
  Store,
  Wifi,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import './App.css'

type StoreOption = {
  id: string
  name: string
  market: string
}

type SalesSummary = {
  tenantId: string
  storeId: string
  currency: string
  timeZone: string
  from: string
  to: string
  netSalesMinor: number
  orderCount: number
  unitsSold: number
  averageOrderValueMinor: number
  freshness: {
    status: string
    generatedAt: string
    lastSourceEventAt: string
    sourceEventCount: number
    duplicateEventCount: number
    isPartial: boolean
    dataSource: string
  }
  reportSchemaVersion: string
}

type SalesReport = {
  summary: SalesSummary
  hourlySales: Array<{
    hour: string
    netSalesMinor: number
    orderCount: number
    unitsSold: number
  }>
  topProducts: Array<{
    productId: string
    productName: string
    unitsSold: number
    netSalesMinor: number
  }>
}

type DashboardState = 'fresh' | 'cached' | 'offline' | 'loading'

const stores: StoreOption[] = [
  { id: 'store-1', name: 'Bardstown Road', market: 'Louisville' },
  { id: 'store-2', name: 'South End Market', market: 'Louisville' },
]

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
const cacheKeyPrefix = 'retailpulse.analytics.sales'

function App() {
  const [storeId, setStoreId] = useState(stores[0].id)
  const [refreshKey, setRefreshKey] = useState(0)
  const [report, setReport] = useState<SalesReport>(() => fallbackReport(storeId))
  const [dashboardState, setDashboardState] = useState<DashboardState>('loading')
  const [lastError, setLastError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function loadReport() {
      setDashboardState('loading')
      setLastError(null)
      const cached = readCachedReport(storeId)

      try {
        const fresh = await fetchSalesReport(storeId)
        if (cancelled) return
        setReport(fresh)
        setDashboardState('fresh')
        localStorage.setItem(cacheKey(storeId), JSON.stringify(fresh))
      } catch (error) {
        if (cancelled) return
        setReport(cached ?? fallbackReport(storeId))
        setDashboardState(cached ? 'cached' : 'offline')
        setLastError(error instanceof Error ? error.message : 'Analytics API is unavailable')
      }
    }

    void loadReport()
    return () => {
      cancelled = true
    }
  }, [storeId, refreshKey])

  const selectedStore = stores.find((store) => store.id === storeId) ?? stores[0]
  const maxHourlySales = Math.max(...report.hourlySales.map((hour) => hour.netSalesMinor), 1)

  return (
    <main className="portal-shell">
      <aside className="sidebar" aria-label="Primary navigation">
        <div className="brand-block" aria-label="RetailPulse">
          <span className="brand-mark">RP</span>
          <div>
            <strong>RetailPulse</strong>
            <span>Manager PWA</span>
          </div>
        </div>
        <nav className="nav-list" aria-label="Dashboard sections">
          <a className="nav-item active" href="#sales"><BarChart3 size={18} />Sales</a>
          <a className="nav-item" href="#inventory"><PackageCheck size={18} />Inventory</a>
          <a className="nav-item" href="#alerts"><Bell size={18} />Alerts</a>
          <a className="nav-item" href="#sync"><Wifi size={18} />Sync</a>
        </nav>
        <div className="session-card">
          <ShieldCheck size={18} />
          <div>
            <strong>Manager session</strong>
            <span>Store-scoped reports only</span>
          </div>
        </div>
      </aside>

      <section className="workspace" aria-label="Manager dashboard">
        <header className="topbar">
          <div>
            <p className="eyebrow">Operations cockpit</p>
            <h1>{selectedStore.name}</h1>
            <p>{selectedStore.market} · Today · {report.summary.timeZone}</p>
          </div>
          <div className="toolbar" aria-label="Dashboard controls">
            <label className="select-shell">
              <Store size={17} />
              <select value={storeId} onChange={(event) => setStoreId(event.target.value)} aria-label="Select store">
                {stores.map((store) => (
                  <option key={store.id} value={store.id}>{store.name}</option>
                ))}
              </select>
              <ChevronDown size={16} aria-hidden="true" />
            </label>
            <button className="icon-button" type="button" title="Refresh report" onClick={() => setRefreshKey((value) => value + 1)}>
              <RefreshCw size={18} />
            </button>
            <button className="icon-button" type="button" title="Export visible report">
              <Download size={18} />
            </button>
          </div>
        </header>

        <section className="status-strip" aria-label="Data status">
          <StatusPill state={dashboardState} />
          <span>Source: {report.summary.freshness.dataSource}</span>
          <span>Schema: {report.summary.reportSchemaVersion}</span>
          <span>Last event: {formatTime(report.summary.freshness.lastSourceEventAt)}</span>
        </section>

        {lastError ? <p className="inline-alert"><CloudOff size={16} />Using cached or built-in simulated data: {lastError}</p> : null}

        <section className="kpi-grid" aria-label="Sales summary">
          <Metric label="Net sales" value={formatMoney(report.summary.netSalesMinor, report.summary.currency)} trend="+8.4% vs same window" icon={<CircleDollarSign size={20} />} />
          <Metric label="Orders" value={report.summary.orderCount.toString()} trend="Duplicate events excluded" icon={<BarChart3 size={20} />} />
          <Metric label="Units sold" value={report.summary.unitsSold.toString()} trend={`${report.summary.freshness.sourceEventCount} source events`} icon={<PackageCheck size={20} />} />
          <Metric label="Avg order" value={formatMoney(report.summary.averageOrderValueMinor, report.summary.currency)} trend={`${report.summary.freshness.duplicateEventCount} duplicate ignored`} icon={<Clock3 size={20} />} />
        </section>

        <section className="dashboard-grid">
          <article className="panel sales-panel" id="sales">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Hourly sales</p>
                <h2>Revenue by hour</h2>
              </div>
              <LineChart size={20} />
            </div>
            <div className="bar-list">
              {report.hourlySales.map((hour) => (
                <div className="bar-row" key={hour.hour}>
                  <span>{formatHour(hour.hour)}</span>
                  <div className="bar-track"><div style={{ width: `${Math.max(8, (hour.netSalesMinor / maxHourlySales) * 100)}%` }} /></div>
                  <strong>{formatMoney(hour.netSalesMinor, report.summary.currency)}</strong>
                </div>
              ))}
            </div>
          </article>

          <article className="panel" id="inventory">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Top products</p>
                <h2>What moved</h2>
              </div>
              <PackageCheck size={20} />
            </div>
            <div className="product-list">
              {report.topProducts.map((product, index) => (
                <div className="product-row" key={product.productId}>
                  <span>{index + 1}</span>
                  <div>
                    <strong>{product.productName}</strong>
                    <p>{product.unitsSold} {product.unitsSold === 1 ? 'unit' : 'units'}</p>
                  </div>
                  <strong>{formatMoney(product.netSalesMinor, report.summary.currency)}</strong>
                </div>
              ))}
            </div>
          </article>

          <article className="panel action-panel" id="sync">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Readiness</p>
                <h2>Operational checks</h2>
              </div>
              <AlertTriangle size={20} />
            </div>
            <ul className="check-list">
              <li><span />Analytics is using simulated facts until real ingestion lands.</li>
              <li><span />Reports are tenant and store scoped on the server.</li>
              <li><span />No payment card data is present in the report contract.</li>
            </ul>
          </article>
        </section>
      </section>
    </main>
  )
}

function Metric({ label, value, trend, icon }: { label: string; value: string; trend: string; icon: React.ReactNode }) {
  return (
    <article className="metric-card">
      <div className="metric-icon">{icon}</div>
      <span>{label}</span>
      <strong>{value}</strong>
      <p>{trend}</p>
    </article>
  )
}

function StatusPill({ state }: { state: DashboardState }) {
  const label = state === 'fresh' ? 'Live API' : state === 'loading' ? 'Loading' : state === 'cached' ? 'Cached' : 'Simulated fallback'
  return <strong className={`status-pill ${state}`}>{label}</strong>
}

async function fetchSalesReport(storeId: string): Promise<SalesReport> {
  if (!apiBaseUrl) {
    throw new Error('VITE_API_BASE_URL is not configured')
  }

  const issuedAt = new Date().toISOString()
  const expiresAt = new Date(Date.now() + 60 * 60 * 1000).toISOString()
  const url = `${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/tenant-1/stores/${storeId}/reports/sales?from=2026-08-23T00:00:00Z&to=2026-08-24T00:00:00Z&timezone=UTC&currency=USD`
  const response = await fetch(url, {
    headers: {
      'X-RetailPulse-Token-Id': `portal-${storeId}-${Date.now()}`,
      'X-RetailPulse-Subject-Id': 'manager-portal',
      'X-RetailPulse-Tenant-Id': 'tenant-1',
      'X-RetailPulse-Store-Id': storeId,
      'X-RetailPulse-Principal-Type': 'User',
      'X-RetailPulse-Roles': 'Manager',
      'X-RetailPulse-Issued-At': issuedAt,
      'X-RetailPulse-Expires-At': expiresAt,
      'X-Correlation-Id': `portal-${crypto.randomUUID()}`,
    },
  })

  if (!response.ok) {
    throw new Error(`Analytics API returned HTTP ${response.status}`)
  }

  return await response.json() as SalesReport
}

function fallbackReport(storeId: string): SalesReport {
  const isSecondStore = storeId === 'store-2'
  const netSalesMinor = isSecondStore ? 1000 : 5750
  const orderCount = isSecondStore ? 1 : 3
  const unitsSold = isSecondStore ? 1 : 6

  return {
    summary: {
      tenantId: 'tenant-1',
      storeId,
      currency: 'USD',
      timeZone: 'UTC',
      from: '2026-08-23T00:00:00+00:00',
      to: '2026-08-24T00:00:00+00:00',
      netSalesMinor,
      orderCount,
      unitsSold,
      averageOrderValueMinor: Math.floor(netSalesMinor / orderCount),
      freshness: {
        status: 'simulated',
        generatedAt: new Date().toISOString(),
        lastSourceEventAt: isSecondStore ? '2026-08-23T14:40:00+00:00' : '2026-08-23T15:10:00+00:00',
        sourceEventCount: isSecondStore ? 1 : 4,
        duplicateEventCount: isSecondStore ? 0 : 1,
        isPartial: false,
        dataSource: apiBaseUrl ? 'cached-or-fallback' : 'local-simulated-fallback',
      },
      reportSchemaVersion: 'sales-report.v1',
    },
    hourlySales: isSecondStore
      ? [{ hour: '2026-08-23T14:00:00+00:00', netSalesMinor: 1000, orderCount: 1, unitsSold: 1 }]
      : [
          { hour: '2026-08-23T14:00:00+00:00', netSalesMinor: 3200, orderCount: 2, unitsSold: 3 },
          { hour: '2026-08-23T15:00:00+00:00', netSalesMinor: 2550, orderCount: 1, unitsSold: 3 },
        ],
    topProducts: isSecondStore
      ? [{ productId: 'coffee', productName: 'Coffee', unitsSold: 1, netSalesMinor: 1000 }]
      : [
          { productId: 'sandwich', productName: 'Sandwich', unitsSold: 3, netSalesMinor: 2550 },
          { productId: 'coffee', productName: 'Coffee', unitsSold: 2, netSalesMinor: 2000 },
          { productId: 'tea', productName: 'Tea', unitsSold: 1, netSalesMinor: 1200 },
        ],
  }
}

function readCachedReport(storeId: string): SalesReport | null {
  const cached = localStorage.getItem(cacheKey(storeId))
  if (!cached) return null

  try {
    return JSON.parse(cached) as SalesReport
  } catch {
    localStorage.removeItem(cacheKey(storeId))
    return null
  }
}

function cacheKey(storeId: string) {
  return `${cacheKeyPrefix}.${storeId}`
}

function formatMoney(minorUnits: number, currency: string) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(minorUnits / 100)
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(value))
}

function formatHour(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(value))
}

export default App
