import {
  AlertTriangle,
  ArrowUpRight,
  Activity,
  BarChart3,
  Bell,
  ClipboardList,
  ChevronDown,
  CircleDollarSign,
  Clock3,
  CloudOff,
  Download,
  LineChart,
  LayoutDashboard,
  PackageCheck,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  Store,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { entraConfigured, getPortalSession, signIn, signOut, type PortalSession } from './auth'
import './App.css'

type StoreOption = {
  id: string
  name: string
  market: string
}

type DemoScenario = {
  scenario: string
  tenantId: string
  role: string
  stores: StoreOption[]
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

type SyncHealth = {
  pendingCount: number
  oldestPendingAt: string | null
  lastSuccessAt: string | null
  retryCount: number
  conflictCount: number
  deadLetterCount: number
}

type QueuedInventoryCommand = {
  tenantId: string
  storeId: string
  productId: string
  quantityDelta: number
  reason: string
  commandId: string
  expectedVersion: number
}

type OperationalAlert = {
  alertId: string
  severity: string
  category: string
  title: string
  detail: string
  occurredAt: string
}

type NotificationPreferences = {
  lowStockEnabled: boolean
  syncFailureEnabled: boolean
}

type NotificationStatus = 'unsupported' | 'default' | 'denied' | 'granted'

type StoreSettings = {
  displayName: string
  timeZone: string
  currency: string
  inventoryAdjustmentsEnabled: boolean
  version: number
}

type InsightResult = {
  insightId: string
  insightType: string
  status: string
  summary: string
  sourceReferences: string[]
  promptVersion: string
  modelDeployment: string
  validationStatus: string
  generatedAt: string
}

const fallbackStores: StoreOption[] = [
  { id: 'store-1', name: 'Bardstown Road', market: 'Louisville' },
  { id: 'store-2', name: 'South End Market', market: 'Louisville' },
]

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
const demoMode = import.meta.env.VITE_DEMO_MODE === 'true'
const cacheKeyPrefix = 'retailpulse.analytics.sales'
const commandQueueKey = 'retailpulse.manager.commands.v1'
const storageScopeKey = 'retailpulse.portal.storage-scope'
const cacheMaxAgeMs = 24 * 60 * 60 * 1000
const displayTimeZone = 'America/New_York'
const demoScenarioKey = import.meta.env.VITE_DEMO_SCENARIO ?? 'owner-three-stores'

function App() {
  const [availableStores, setAvailableStores] = useState<StoreOption[]>(fallbackStores)
  const [storeId, setStoreId] = useState(fallbackStores[0].id)
  const [scenarioTenantId, setScenarioTenantId] = useState('tenant-1')
  const [scenarioRole, setScenarioRole] = useState('Manager')
  const [scenarioLoading, setScenarioLoading] = useState(demoMode && Boolean(apiBaseUrl))
  const [refreshKey, setRefreshKey] = useState(0)
  const [report, setReport] = useState<SalesReport>(() => fallbackReport(storeId))
  const [dashboardState, setDashboardState] = useState<DashboardState>('loading')
  const [lastError, setLastError] = useState<string | null>(null)
  const [session, setSession] = useState<PortalSession | null>(null)
  const [authLoading, setAuthLoading] = useState(!demoMode && entraConfigured)
  const [authError, setAuthError] = useState<string | null>(null)
  const [adjustmentStatus, setAdjustmentStatus] = useState<string | null>(null)
  const [adjustmentSubmitting, setAdjustmentSubmitting] = useState(false)
  const [syncHealth, setSyncHealth] = useState<SyncHealth | null>(null)
  const [syncHealthError, setSyncHealthError] = useState<string | null>(null)
  const [pendingCommandCount, setPendingCommandCount] = useState(0)
  const [alerts, setAlerts] = useState<OperationalAlert[]>([])
  const [alertsError, setAlertsError] = useState<string | null>(null)
  const [notificationPreferences, setNotificationPreferences] = useState<NotificationPreferences>({ lowStockEnabled: true, syncFailureEnabled: true })
  const [preferencesSaving, setPreferencesSaving] = useState(false)
  const [notificationStatus, setNotificationStatus] = useState<NotificationStatus>(() => getNotificationStatus())
  const [notificationMessage, setNotificationMessage] = useState<string | null>(null)
  const [storeSettings, setStoreSettings] = useState<StoreSettings | null>(null)
  const [settingsError, setSettingsError] = useState<string | null>(null)
  const [settingsSaving, setSettingsSaving] = useState(false)
  const [insight, setInsight] = useState<InsightResult | null>(null)
  const [insightError, setInsightError] = useState<string | null>(null)
  const [currentTime, setCurrentTime] = useState(() => new Date())
  const tenantId = demoMode ? scenarioTenantId : session?.tenantId ?? 'tenant-1'

  useEffect(() => {
    if (!demoMode || !apiBaseUrl) {
      setScenarioLoading(false)
      return
    }

    void fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/dev/scenarios/${demoScenarioKey}`)
      .then(async (response) => {
        if (!response.ok) throw new Error(`Scenario manifest returned HTTP ${response.status}`)
        return await response.json() as DemoScenario
      })
      .then((scenario) => {
        setScenarioTenantId(scenario.tenantId)
        setScenarioRole(scenario.role)
        setAvailableStores(scenario.stores)
        setStoreId(scenario.stores[0]?.id ?? fallbackStores[0].id)
      })
      .catch((error: unknown) => setAuthError(error instanceof Error ? error.message : 'Development scenario is unavailable'))
      .finally(() => setScenarioLoading(false))
  }, [])

  useEffect(() => {
    const timer = window.setInterval(() => setCurrentTime(new Date()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (demoMode || !entraConfigured) {
      setAuthLoading(false)
      return
    }

    void withTimeout(getPortalSession(), 5000)
      .then(setSession)
      .catch((error: unknown) => setAuthError(error instanceof Error ? error.message : 'Unable to restore secure session'))
      .finally(() => setAuthLoading(false))
  }, [])

  useEffect(() => {
    let cancelled = false

    async function loadReport() {
      setDashboardState('loading')
      setLastError(null)
      if (scenarioLoading) return
      if (!demoMode && entraConfigured && !session) return
      const scope = getStorageScope(session)
      const cached = readCachedReport(storeId, scope)

      try {
        if (!demoMode && entraConfigured && session && (!session.tenantId || !session.roles.length)) {
          throw new Error('Your Entra token does not include the required tenant and role claims for manager access.')
        }

        const fresh = await fetchSalesReport(storeId, session?.accessToken, tenantId)
        if (cancelled) return
        setReport(fresh)
        setDashboardState('fresh')
        localStorage.setItem(cacheKey(storeId, scope), JSON.stringify({ cachedAt: Date.now(), report: fresh }))
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
  }, [session, storeId, tenantId, refreshKey, scenarioLoading])

  const isOwner = demoMode ? scenarioRole.toLowerCase() === 'owner' : session?.roles.some((role) => role.toLowerCase() === 'owner') ?? false

  useEffect(() => {
    let cancelled = false
    setStoreSettings(null)
    setSettingsError(null)
    if (scenarioLoading || !isOwner || !session?.accessToken) return
    void fetchStoreSettings(storeId, session.accessToken, tenantId)
      .then((settings) => {
        if (!cancelled) setStoreSettings(settings)
      })
      .catch((error: unknown) => {
        if (!cancelled) setSettingsError(error instanceof Error ? error.message : 'Store settings are unavailable')
      })
    return () => {
      cancelled = true
    }
  }, [isOwner, session, storeId, tenantId, refreshKey, scenarioLoading])

  useEffect(() => {
    let cancelled = false
    setInsight(null)
    setInsightError(null)
    if (scenarioLoading || (!demoMode && entraConfigured && !session)) return
    void requestInsight(storeId, session?.accessToken, tenantId)
      .then((result) => {
        if (!cancelled) setInsight(result)
      })
      .catch((error: unknown) => {
        if (!cancelled) setInsightError(error instanceof Error ? error.message : 'Insights are unavailable')
      })
    return () => {
      cancelled = true
    }
  }, [session, storeId, tenantId, refreshKey, scenarioLoading])

  useEffect(() => {
    let cancelled = false
    setAlertsError(null)
    if (scenarioLoading || (!demoMode && entraConfigured && !session)) return
    void Promise.all([fetchAlerts(storeId, session?.accessToken, tenantId), fetchNotificationPreferences(storeId, session?.accessToken, tenantId)])
      .then(([nextAlerts, preferences]) => {
        if (!cancelled) {
          setAlerts(nextAlerts)
          setNotificationPreferences(preferences)
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) setAlertsError(error instanceof Error ? error.message : 'Alerts are unavailable')
      })
    return () => {
      cancelled = true
    }
  }, [session, storeId, tenantId, refreshKey, scenarioLoading])

  useEffect(() => {
    const flush = () => {
      if (session?.accessToken) void flushQueuedCommands(storeId, session.accessToken, getStorageScope(session), setPendingCommandCount, setAdjustmentStatus)
    }
    setPendingCommandCount(readQueuedCommands(getStorageScope(session)).length)
    window.addEventListener('online', flush)
    flush()
    return () => window.removeEventListener('online', flush)
  }, [session, storeId])

  useEffect(() => {
    if (!session) return
    removeLegacyPortalStorage()
    const scope = getStorageScope(session)
    const previousScope = localStorage.getItem(storageScopeKey)
    if (previousScope && previousScope !== scope) clearPortalStorage(previousScope)
    localStorage.setItem(storageScopeKey, scope)
  }, [session])

  useEffect(() => {
    let cancelled = false
    setSyncHealth(null)
    setSyncHealthError(null)
    if (scenarioLoading || (!demoMode && entraConfigured && !session)) return

    void fetchSyncHealth(storeId, session?.accessToken, tenantId)
      .then((health) => {
        if (!cancelled) setSyncHealth(health)
      })
      .catch((error: unknown) => {
        if (!cancelled) setSyncHealthError(error instanceof Error ? error.message : 'Sync health is unavailable')
      })

    return () => {
      cancelled = true
    }
  }, [session, storeId, tenantId, refreshKey, scenarioLoading])

  if (scenarioLoading) {
    return (
      <main className="auth-gate">
        <div className="auth-gate-mark">RP</div>
        <p className="eyebrow">Development scenario</p>
        <h1>Loading {demoScenarioKey}</h1>
        <p>Resolving the seeded tenant and authorized store scope.</p>
      </main>
    )
  }

  if (!demoMode && entraConfigured && !session) {
    return (
      <main className="auth-gate">
        <div className="auth-gate-mark">RP</div>
        <p className="eyebrow">RetailPulse Manager</p>
        <h1>{authLoading ? 'Checking your session' : 'Sign in to your workspace'}</h1>
        <p>{authLoading ? 'Restoring your secure Entra ID session.' : 'Your identity and store permissions are required before reports can be shown.'}</p>
        {authError ? <p className="inline-alert"><CloudOff size={16} />{authError}</p> : null}
        <button className="session-button auth-gate-button" type="button" disabled={authLoading} onClick={() => void handleSignIn(setAuthError, setAuthLoading)}>
          {authLoading ? 'Checking session...' : 'Sign in with Entra ID'}
        </button>
      </main>
    )
  }

  const selectedStore = availableStores.find((store) => store.id === storeId) ?? availableStores[0] ?? fallbackStores[0]
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
          <a className="nav-item active" href="#overview"><LayoutDashboard size={18} />Overview</a>
          <a className="nav-item" href="#inventory"><PackageCheck size={18} />Inventory</a>
          <a className="nav-item" href="#alerts"><Bell size={18} />Alerts{alerts.length > 0 ? <span className="nav-count">{alerts.length}</span> : null}</a>
          <a className="nav-item" href="#sync"><Activity size={18} />Sync</a>
        </nav>
        <div className="session-card">
          <ShieldCheck size={18} />
          <div>
            <strong>{demoMode ? 'Demo manager session' : session ? (session.account.name ?? 'Secure manager session') : 'Secure manager session'}</strong>
            <span>{demoMode ? 'Synthetic identity for local testing' : session ? 'Entra ID authenticated' : 'Identity provider session required'}</span>
          </div>
        </div>
        {!demoMode && entraConfigured ? (
          <button className="session-button" type="button" disabled={authLoading} onClick={() => void handleSignIn(setAuthError, setAuthLoading)}>
            {authLoading ? 'Checking session...' : session ? 'Refresh session' : 'Sign in with Entra ID'}
          </button>
        ) : null}
        {session ? <button className="sign-out-button" type="button" onClick={() => void handleSignOut(setSession, setAuthError, getStorageScope(session))}>Sign out</button> : null}
      </aside>

      <section className="workspace" aria-label="Manager dashboard">
        <header className="topbar" id="overview">
          <div>
            <p className="eyebrow">Manager workspace</p>
            <h1>Good morning</h1>
            <p>{selectedStore.market} · Eastern Time · {formatDate(currentTime.toISOString())} · {formatClock(currentTime.toISOString())}</p>
          </div>
          <div className="toolbar" aria-label="Dashboard controls">
            <label className="select-shell">
              <Store size={17} />
              <select value={storeId} onChange={(event) => setStoreId(event.target.value)} aria-label="Select store">
                {availableStores.map((store) => (
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

        <section className="pulse-hero" aria-labelledby="store-pulse-title">
          <div className="pulse-copy">
            <div className="pulse-kicker"><span className="pulse-dot" />Store pulse</div>
            <h2 id="store-pulse-title">{selectedStore.name}</h2>
            <p>See what needs attention before the next trading window.</p>
          </div>
          <div className="pulse-action">
            <span className="pulse-action-label">Next safe action</span>
            <strong>{alerts[0]?.title ?? (syncHealth?.pendingCount ? 'Review sync queue' : 'Review today\'s sales')}</strong>
            <a href={alerts[0] ? '#alerts' : '#sales'}>Open workspace <ArrowUpRight size={15} /></a>
          </div>
          <Sparkles className="pulse-spark" size={30} aria-hidden="true" />
        </section>

        <section className="status-strip" aria-label="Data status">
          <StatusPill state={dashboardState} />
          <span>Source: {report.summary.freshness.dataSource}</span>
          <span>Schema: {report.summary.reportSchemaVersion}</span>
          <span>Last event: {formatTime(report.summary.freshness.lastSourceEventAt)}</span>
        </section>

        {authError ? <p className="inline-alert"><CloudOff size={16} />{authError}</p> : null}
        {lastError ? <p className="inline-alert"><CloudOff size={16} />Using cached or built-in simulated data: {lastError}</p> : null}

        <section className="priority-layout" aria-label="Priority overview">
          <article className="priority-panel">
            <div className="panel-heading compact-heading">
              <div>
                <p className="eyebrow">Priority queue</p>
                <h2>Today's focus</h2>
              </div>
              <ClipboardList size={20} />
            </div>
            {alerts.length > 0 ? <div className="priority-list">{alerts.slice(0, 2).map((alert) => <a className="priority-item" href="#alerts" key={alert.alertId}><span className={`priority-severity ${alert.severity.toLowerCase()}`} /><span><strong>{alert.title}</strong><small>{alert.detail}</small></span><ArrowUpRight size={16} /></a>)}</div> : <div className="priority-empty"><span className="priority-check"><ShieldCheck size={16} /></span><span><strong>No urgent issues</strong><small>{syncHealth?.pendingCount ? `${syncHealth.pendingCount} item${syncHealth.pendingCount === 1 ? '' : 's'} still syncing.` : 'Your store is ready for the next trading window.'}</small></span></div>}
          </article>
          <article className="priority-panel readiness-card">
            <div className="panel-heading compact-heading">
              <div>
                <p className="eyebrow">Trust signal</p>
                <h2>Data confidence</h2>
              </div>
              <ShieldCheck size={20} />
            </div>
            <div className="confidence-value"><span className={`confidence-dot ${dashboardState}`} />{dashboardState === 'fresh' ? 'Live and current' : dashboardState === 'cached' ? 'Cached read' : dashboardState === 'offline' ? 'Offline mode' : 'Checking data'}</div>
            <p className="confidence-detail">Source: {report.summary.freshness.dataSource} · Last event {formatTime(report.summary.freshness.lastSourceEventAt)}</p>
          </article>
        </section>

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
            <form className="inventory-form" onSubmit={(event) => void submitInventoryAdjustment(event, storeId, session?.accessToken, getStorageScope(session), setAdjustmentStatus, setAdjustmentSubmitting, setPendingCommandCount)}>
              <label>
                Product ID
                <input name="productId" defaultValue="coffee" required />
              </label>
              <label>
                Quantity change
                <input name="quantityDelta" type="number" defaultValue="1" required />
              </label>
              <label>
                Reason
                <input name="reason" defaultValue="Cycle count" required />
              </label>
              <button className="session-button" type="submit" disabled={adjustmentSubmitting || !session}>
                {adjustmentSubmitting ? 'Submitting...' : 'Adjust inventory'}
              </button>
              {adjustmentStatus ? <p className="command-status">{adjustmentStatus}</p> : null}
            </form>
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
              <li><span className={syncHealth && (syncHealth.pendingCount > 0 || pendingCommandCount > 0) ? 'warning-dot' : ''} />{pendingCommandCount > 0 ? `${pendingCommandCount} manager command${pendingCommandCount === 1 ? '' : 's'} pending locally.` : syncHealth ? syncHealth.pendingCount === 0 ? 'Sync queue is clear.' : `${syncHealth.pendingCount} item${syncHealth.pendingCount === 1 ? '' : 's'} pending synchronization.` : syncHealthError ?? 'Checking sync health...'}</li>
              <li><span className={syncHealth && (syncHealth.retryCount > 0 || syncHealth.conflictCount > 0 || syncHealth.deadLetterCount > 0) ? 'warning-dot' : ''} />{syncHealth ? `${syncHealth.retryCount} retries · ${syncHealth.conflictCount} conflicts · ${syncHealth.deadLetterCount} dead letters.` : 'Tenant and store scope is enforced by the server.'}</li>
              <li><span />Last successful sync: {syncHealth?.lastSuccessAt ? formatTime(syncHealth.lastSuccessAt) : 'No completed sync recorded.'}</li>
            </ul>
          </article>

          <article className="panel" id="alerts">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Alerts</p>
                <h2>Needs attention</h2>
              </div>
              <Bell size={20} />
            </div>
            {alertsError ? <p className="inline-alert"><CloudOff size={16} />{alertsError}</p> : alerts.length === 0 ? <p className="empty-state">No active alerts for this store.</p> : <div className="alert-list">{alerts.map((alert) => <div className="alert-row" key={alert.alertId}><strong>{alert.title}</strong><p>{alert.detail}</p><span>{formatTime(alert.occurredAt)}</span></div>)}</div>}
            <div className="preference-list">
              <label><input type="checkbox" checked={notificationPreferences.lowStockEnabled} disabled={preferencesSaving || !session} onChange={(event) => void saveNotificationPreferences(storeId, session?.accessToken, { ...notificationPreferences, lowStockEnabled: event.target.checked }, setNotificationPreferences, setPreferencesSaving)} /> Low-stock notifications</label>
              <label><input type="checkbox" checked={notificationPreferences.syncFailureEnabled} disabled={preferencesSaving || !session} onChange={(event) => void saveNotificationPreferences(storeId, session?.accessToken, { ...notificationPreferences, syncFailureEnabled: event.target.checked }, setNotificationPreferences, setPreferencesSaving)} /> Sync-failure notifications</label>
              <div className="notification-actions">
                <span>Browser notifications: {notificationStatus === 'granted' ? 'Enabled' : notificationStatus === 'denied' ? 'Blocked' : notificationStatus === 'unsupported' ? 'Unavailable' : 'Not enabled'}</span>
                {notificationStatus !== 'granted' && notificationStatus !== 'unsupported' ? <button className="text-button" type="button" disabled={!session} onClick={() => void enableNotifications(storeId, session?.accessToken, setNotificationStatus, setNotificationMessage)}>Enable browser notifications</button> : null}
                {notificationStatus === 'granted' ? <button className="text-button" type="button" onClick={() => void sendTestNotification(setNotificationMessage)}>Send test alert</button> : null}
                {notificationMessage ? <span role="status">{notificationMessage}</span> : null}
              </div>
            </div>
          </article>

          {isOwner ? <article className="panel" id="settings">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Owner settings</p>
                <h2>Store configuration</h2>
              </div>
              <ShieldCheck size={20} />
            </div>
            {settingsError ? <p className="inline-alert"><CloudOff size={16} />{settingsError}</p> : storeSettings ? <form className="settings-form" onSubmit={(event) => void saveStoreSettings(event, storeId, session?.accessToken, storeSettings, setStoreSettings, setSettingsError, setSettingsSaving)}>
              <label>Display name<input name="displayName" defaultValue={storeSettings.displayName} required /></label>
              <label>Time zone<input name="timeZone" defaultValue={storeSettings.timeZone} required /></label>
              <label>Currency<input name="currency" defaultValue={storeSettings.currency} maxLength={3} required /></label>
              <label><input name="inventoryAdjustmentsEnabled" type="checkbox" defaultChecked={storeSettings.inventoryAdjustmentsEnabled} /> Manager inventory adjustments enabled</label>
              <button className="session-button" type="submit" disabled={settingsSaving}>{settingsSaving ? 'Saving...' : 'Save store settings'}</button>
            </form> : <p className="empty-state">Loading store settings...</p>}
          </article> : null}

          <article className="panel" id="insights">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Insights</p>
                <h2>What to investigate</h2>
              </div>
              <LineChart size={20} />
            </div>
            {insightError ? <p className="inline-alert"><CloudOff size={16} />{insightError}</p> : insight ? <div className="insight-content"><p>{insight.summary}</p><span>{insight.validationStatus} · {insight.modelDeployment} · {insight.promptVersion}</span><span>Sources: {insight.sourceReferences.join(', ')}</span></div> : <p className="empty-state">Generating advisory insight...</p>}
          </article>
        </section>
      </section>
    </main>
  )
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error('Secure session check timed out')), timeoutMs)
    promise.then(resolve, reject).finally(() => window.clearTimeout(timeout))
  })
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

async function fetchSalesReport(storeId: string, accessToken: string | undefined, tenantId: string): Promise<SalesReport> {
  if (!apiBaseUrl || (!demoMode && !accessToken)) {
    throw new Error('Live identity session is not configured')
  }

  if (!demoMode && accessToken) {
    const token = accessToken.split('.')[1]
    if (token) {
      try {
        const padded = token.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(token.length / 4) * 4, '=')
        const claims = JSON.parse(atob(padded))
        const roleList = Array.isArray(claims.roles) ? claims.roles : typeof claims.role === 'string' ? claims.role.split(',') : []
        const groupList = Array.isArray(claims.groups) ? claims.groups : typeof claims.group === 'string' ? claims.group.split(',') : []
        const hasStoreClaim = !!(claims.store_id || claims.storeId)
        const hasGroupMembership = groupList.length > 0
        if (!hasStoreClaim && !hasGroupMembership && roleList.length === 0) {
          throw new Error('Your Entra token is missing the required manager authorization claims.')
        }
      } catch {
        throw new Error('Your Entra token is malformed or missing the required RetailPulse claims.')
      }
    }
  }

  const issuedAt = new Date().toISOString()
  const expiresAt = new Date(Date.now() + 60 * 60 * 1000).toISOString()
  const url = `${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/reports/sales?from=2026-08-23T00:00:00Z&to=2026-08-24T00:00:00Z&timezone=America%2FNew_York&currency=USD`
  const headers: Record<string, string> = demoMode ? {
      'X-RetailPulse-Token-Id': `portal-${storeId}-${Date.now()}`,
      'X-RetailPulse-Subject-Id': 'manager-portal',
      'X-RetailPulse-Tenant-Id': tenantId,
      'X-RetailPulse-Store-Id': storeId,
      'X-RetailPulse-Principal-Type': 'User',
      'X-RetailPulse-Roles': 'Manager',
      'X-RetailPulse-Issued-At': issuedAt,
      'X-RetailPulse-Expires-At': expiresAt,
      'X-Correlation-Id': `portal-${crypto.randomUUID()}`,
  } : { Authorization: `Bearer ${accessToken}`, 'X-Correlation-Id': `portal-${crypto.randomUUID()}` }
  const response = await fetch(url, { headers })

  if (!response.ok) {
    throw new Error(`Analytics API returned HTTP ${response.status}`)
  }

  return await response.json() as SalesReport
}

async function fetchSyncHealth(storeId: string, accessToken: string | undefined, tenantId: string): Promise<SyncHealth> {
  if (!apiBaseUrl || (!demoMode && !accessToken)) {
    throw new Error('Live identity session is not configured')
  }

  const url = `${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/sync-health`
  const headers: Record<string, string> = demoMode ? {
    'X-RetailPulse-Token-Id': `portal-sync-${storeId}-${Date.now()}`,
    'X-RetailPulse-Subject-Id': 'manager-portal',
    'X-RetailPulse-Tenant-Id': tenantId,
    'X-RetailPulse-Store-Id': storeId,
    'X-RetailPulse-Principal-Type': 'User',
    'X-RetailPulse-Roles': 'Manager',
    'X-RetailPulse-Issued-At': new Date().toISOString(),
    'X-RetailPulse-Expires-At': new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  } : { Authorization: `Bearer ${accessToken}` }
  const response = await fetch(url, { headers })
  if (!response.ok) throw new Error(`Sync health API returned HTTP ${response.status}`)
  return await response.json() as SyncHealth
}

async function fetchAlerts(storeId: string, accessToken: string | undefined, tenantId: string): Promise<OperationalAlert[]> {
  if (!apiBaseUrl || (!demoMode && !accessToken)) throw new Error('Live identity session is not configured')
  const headers: Record<string, string> = demoMode ? {
    'X-RetailPulse-Token-Id': `portal-alerts-${Date.now()}`,
    'X-RetailPulse-Subject-Id': 'manager-portal',
    'X-RetailPulse-Tenant-Id': tenantId,
    'X-RetailPulse-Store-Id': storeId,
    'X-RetailPulse-Principal-Type': 'User',
    'X-RetailPulse-Roles': 'Manager',
    'X-RetailPulse-Issued-At': new Date().toISOString(),
    'X-RetailPulse-Expires-At': new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  } : { Authorization: `Bearer ${accessToken}` }
  const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/alerts`, { headers })
  if (!response.ok) throw new Error(`Alerts API returned HTTP ${response.status}`)
  return await response.json() as OperationalAlert[]
}

async function fetchNotificationPreferences(storeId: string, accessToken: string | undefined, tenantId: string): Promise<NotificationPreferences> {
  if (!apiBaseUrl || (!demoMode && !accessToken)) throw new Error('Live identity session is not configured')
  if (demoMode) return { lowStockEnabled: true, syncFailureEnabled: true }
  const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/notification-preferences`, { headers: demoMode ? {} : { Authorization: `Bearer ${accessToken}` } })
  if (!response.ok) throw new Error(`Notification preferences API returned HTTP ${response.status}`)
  return await response.json() as NotificationPreferences
}

async function saveNotificationPreferences(storeId: string, accessToken: string | undefined, preferences: NotificationPreferences, setPreferences: (value: NotificationPreferences) => void, setSaving: (value: boolean) => void) {
  if (!accessToken) return
  setSaving(true)
  try {
    const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/tenant-1/stores/${storeId}/notification-preferences`, { method: 'PUT', headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' }, body: JSON.stringify(preferences) })
    if (!response.ok) throw new Error(`Notification preferences API returned HTTP ${response.status}`)
    setPreferences(preferences)
  } finally {
    setSaving(false)
  }
}

async function fetchStoreSettings(storeId: string, accessToken: string, tenantId: string): Promise<StoreSettings> {
  const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/settings`, { headers: { Authorization: `Bearer ${accessToken}` } })
  if (!response.ok) throw new Error(`Store settings API returned HTTP ${response.status}`)
  return await response.json() as StoreSettings
}

async function requestInsight(storeId: string, accessToken: string | undefined, tenantId: string): Promise<InsightResult> {
  if (!apiBaseUrl || (!demoMode && !accessToken)) throw new Error('Live identity session is not configured')
  const headers: Record<string, string> = demoMode ? {
    'X-RetailPulse-Token-Id': `portal-insights-${Date.now()}`,
    'X-RetailPulse-Subject-Id': 'manager-portal',
    'X-RetailPulse-Tenant-Id': tenantId,
    'X-RetailPulse-Store-Id': storeId,
    'X-RetailPulse-Principal-Type': 'User',
    'X-RetailPulse-Roles': 'Manager',
    'X-RetailPulse-Issued-At': new Date().toISOString(),
    'X-RetailPulse-Expires-At': new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    'Content-Type': 'application/json'
  } : { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' }

  const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${tenantId}/stores/${storeId}/insights`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ insightType: 'sales-summary', requestId: crypto.randomUUID(), sourceVersion: 'sales-report.v1' }),
  })
  if (!response.ok) throw new Error(`Insights API returned HTTP ${response.status}`)
  return await response.json() as InsightResult
}

async function saveStoreSettings(event: React.FormEvent<HTMLFormElement>, storeId: string, accessToken: string | undefined, current: StoreSettings, setSettings: (settings: StoreSettings) => void, setError: (error: string | null) => void, setSaving: (saving: boolean) => void) {
  event.preventDefault()
  if (!accessToken) return
  const form = new FormData(event.currentTarget)
  setSaving(true)
  setError(null)
  try {
    const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/tenant-1/stores/${storeId}/settings`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        displayName: form.get('displayName'),
        timeZone: form.get('timeZone'),
        currency: form.get('currency'),
        inventoryAdjustmentsEnabled: form.get('inventoryAdjustmentsEnabled') === 'on',
        expectedVersion: current.version,
      }),
    })
    const body = await response.json().catch(() => ({})) as StoreSettings & { error?: string }
    if (!response.ok) throw new Error(body.error ?? `Store settings API returned HTTP ${response.status}`)
    setSettings(body)
  } catch (error) {
    setError(error instanceof Error ? error.message : 'Store settings could not be saved')
  } finally {
    setSaving(false)
  }
}

function getNotificationStatus(): NotificationStatus {
  if (!('Notification' in window)) return 'unsupported'
  return Notification.permission
}

async function enableNotifications(storeId: string, accessToken: string | undefined, setStatus: (status: NotificationStatus) => void, setMessage: (message: string | null) => void) {
  if (!('Notification' in window)) {
    setStatus('unsupported')
    setMessage('This browser does not support notifications.')
    return
  }

  const permission = await Notification.requestPermission()
  setStatus(permission)
  if (permission !== 'granted') {
    setMessage(permission === 'denied' ? 'Browser notifications are blocked for this site.' : 'Notification permission was not granted.')
    return
  }

  if (!accessToken) {
    setMessage('Browser notifications enabled. Push provider registration is not configured yet.')
    return
  }

  try {
    const keyResponse = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/tenant-1/stores/${storeId}/push/public-key`, { headers: { Authorization: `Bearer ${accessToken}` } })
    if (keyResponse.status === 404) {
      setMessage('Browser notifications enabled. Push provider registration is not configured yet.')
      return
    }
    if (!keyResponse.ok) throw new Error(`Push configuration API returned HTTP ${keyResponse.status}`)
    const keyBody = await keyResponse.json() as { publicKey?: string }
    if (!keyBody.publicKey) throw new Error('Push provider public key is missing')
    const registration = await navigator.serviceWorker.ready
    const subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(keyBody.publicKey) })
    const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/tenant-1/stores/${storeId}/push-subscription`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ endpoint: subscription.endpoint, p256dh: toBase64(subscription.getKey('p256dh')), auth: toBase64(subscription.getKey('auth')) }),
    })
    if (!response.ok) throw new Error(`Push subscription API returned HTTP ${response.status}`)
    setMessage('Browser notifications enabled and registered for this store.')
  } catch {
    setMessage('Browser notifications are enabled, but push registration is unavailable in this browser session.')
  }
}

function urlBase64ToUint8Array(value: string) {
  const padding = '='.repeat((4 - (value.length % 4)) % 4)
  const raw = atob((value + padding).replace(/-/g, '+').replace(/_/g, '/'))
  return Uint8Array.from(raw, (character) => character.charCodeAt(0))
}

function toBase64(value: ArrayBuffer | null) {
  if (!value) throw new Error('Push subscription key is missing')
  return btoa(String.fromCharCode(...new Uint8Array(value)))
}

async function sendTestNotification(setMessage: (message: string | null) => void) {
  try {
    const registration = await navigator.serviceWorker?.ready
    if (!registration) throw new Error('Service worker is unavailable')
    await registration.showNotification('RetailPulse test alert', { body: 'Browser notification delivery is working.', tag: 'retailpulse-test-alert', data: { url: '/#alerts' } })
    setMessage('Test alert sent.')
  } catch {
    setMessage('Browser notification delivery is unavailable in this session.')
  }
}

async function submitInventoryAdjustment(
  event: React.FormEvent<HTMLFormElement>,
  storeId: string,
  accessToken: string | undefined,
  scope: string,
  setStatus: (status: string | null) => void,
  setSubmitting: (submitting: boolean) => void,
  setPendingCount: (count: number) => void,
) {
  event.preventDefault()
  if (!accessToken) {
    setStatus('Sign in with Entra ID to submit manager commands.')
    return
  }

  const form = new FormData(event.currentTarget)
  const command: QueuedInventoryCommand = {
    tenantId: 'tenant-1',
    storeId,
    productId: String(form.get('productId') ?? ''),
    quantityDelta: Number(form.get('quantityDelta')),
    reason: String(form.get('reason') ?? ''),
    commandId: crypto.randomUUID(),
    expectedVersion: 0,
  }
  setSubmitting(true)

  setStatus(null)

  try {
    const response = await postInventoryCommand(command, accessToken)
    const body = await response.json().catch(() => ({})) as { outcome?: string; error?: string }
    if (!response.ok) {
      setStatus(body.error ?? `Inventory command failed (${response.status}).`)
      return
    }
    setStatus(`Inventory command ${body.outcome?.toLowerCase() ?? 'accepted'}.`)
  } catch {
    persistQueuedCommands(scope, [...readQueuedCommands(scope), command])
    setPendingCount(readQueuedCommands(scope).length)
    setStatus('Inventory command queued while offline.')
  } finally {
    setSubmitting(false)
  }
}

async function postInventoryCommand(command: QueuedInventoryCommand, accessToken: string): Promise<Response> {
  return fetch(`${apiBaseUrl.replace(/\/$/, '')}/api/v1/tenants/${command.tenantId}/stores/${command.storeId}/manager/inventory-adjustments`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
    body: JSON.stringify(command),
  })
}

async function flushQueuedCommands(
  storeId: string,
  accessToken: string,
  scope: string,
  setPendingCount: (count: number) => void,
  setStatus: (status: string | null) => void,
) {
  const queued = readQueuedCommands(scope)
  const remaining: QueuedInventoryCommand[] = []
  let confirmed = 0
  for (const command of queued) {
    if (command.storeId !== storeId) {
      remaining.push(command)
      continue
    }
    try {
      const response = await postInventoryCommand(command, accessToken)
      if (response.ok) {
        confirmed += 1
      } else if (response.status === 409) {
        setStatus('A queued inventory command needs review because stock changed.')
      } else {
        remaining.push(command)
      }
    } catch {
      remaining.push(command)
    }
  }
  persistQueuedCommands(scope, remaining)
  setPendingCount(remaining.length)
  if (confirmed > 0 && remaining.length === 0) setStatus(`${confirmed} queued inventory command${confirmed === 1 ? '' : 's'} confirmed.`)
}

function readQueuedCommands(scope: string): QueuedInventoryCommand[] {
  try {
    const value = JSON.parse(localStorage.getItem(`${commandQueueKey}.${scope}`) ?? '[]') as unknown
    return Array.isArray(value) ? value.filter(isQueuedInventoryCommand) : []
  } catch {
    return []
  }
}

function persistQueuedCommands(scope: string, commands: QueuedInventoryCommand[]) {
  localStorage.setItem(`${commandQueueKey}.${scope}`, JSON.stringify(commands))
}

function isQueuedInventoryCommand(value: unknown): value is QueuedInventoryCommand {
  if (!value || typeof value !== 'object') return false
  const command = value as Partial<QueuedInventoryCommand>
  return typeof command.tenantId === 'string' && typeof command.storeId === 'string' && typeof command.productId === 'string' &&
    typeof command.quantityDelta === 'number' && typeof command.reason === 'string' && typeof command.commandId === 'string' &&
    typeof command.expectedVersion === 'number'
}

async function handleSignIn(
  setAuthError: (error: string | null) => void,
  setAuthLoading: (loading: boolean) => void,
) {
  setAuthLoading(true)
  setAuthError(null)
  try {
    await withTimeout(signIn(), 30000)
  } catch (error) {
    setAuthError(error instanceof Error ? error.message : 'Unable to sign in. Allow popups for localhost:5173 and retry.')
  } finally {
    setAuthLoading(false)
  }
}

async function handleSignOut(setSession: (session: PortalSession | null) => void, setAuthError: (error: string | null) => void, scope: string) {
  try {
    clearPortalStorage(scope)
    await signOut()
    setSession(null)
  } catch (error) {
    setAuthError(error instanceof Error ? error.message : 'Unable to sign out')
  }
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

function readCachedReport(storeId: string, scope: string): SalesReport | null {
  const cached = localStorage.getItem(cacheKey(storeId, scope))
  if (!cached) return null

  try {
    const entry = JSON.parse(cached) as { cachedAt?: number; report?: SalesReport }
    if (!entry.cachedAt || !entry.report || Date.now() - entry.cachedAt > cacheMaxAgeMs) {
      localStorage.removeItem(cacheKey(storeId, scope))
      return null
    }
    return entry.report
  } catch {
    localStorage.removeItem(cacheKey(storeId, scope))
    return null
  }
}

function cacheKey(storeId: string, scope: string) {
  return `${cacheKeyPrefix}.${scope}.${storeId}`
}

function getStorageScope(session: PortalSession | null) {
  return session?.account.homeAccountId ?? 'demo'
}

function clearPortalStorage(scope: string) {
  for (let index = localStorage.length - 1; index >= 0; index -= 1) {
    const key = localStorage.key(index)
    if (key?.startsWith(`${cacheKeyPrefix}.${scope}.`) || key === `${commandQueueKey}.${scope}`) localStorage.removeItem(key)
  }
  localStorage.removeItem(storageScopeKey)
}

function removeLegacyPortalStorage() {
  localStorage.removeItem(commandQueueKey)
  for (const store of fallbackStores) localStorage.removeItem(`${cacheKeyPrefix}.${store.id}`)
}

function formatMoney(minorUnits: number, currency: string) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(minorUnits / 100)
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: displayTimeZone }).format(new Date(value))
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: displayTimeZone }).format(new Date(value))
}

function formatClock(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', second: '2-digit', timeZoneName: 'short', timeZone: displayTimeZone }).format(new Date(value))
}

function formatHour(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: displayTimeZone }).format(new Date(value))
}

export default App
