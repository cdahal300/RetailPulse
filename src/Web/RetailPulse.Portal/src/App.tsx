import heroImg from './assets/hero.png'
import './App.css'

function App() {
  return (
    <main className="portal-shell">
      <header className="portal-header">
        <span className="brand-mark">RP</span>
        <span>RetailPulse</span>
        <span className="status-pill">Pilot workspace</span>
      </header>
      <section className="hero-panel">
        <div className="hero-copy">
          <p className="eyebrow">Retail operations cockpit</p>
          <h1>See what needs attention across every store.</h1>
          <p className="hero-description">
            Sales, inventory, sync health, and practical insights in one calm
            workspace for growing retailers.
          </p>
          <div className="hero-actions">
            <button type="button">Open dashboard</button>
            <a href="https://github.com/cdahal300/RetailPulse">View project</a>
          </div>
        </div>
        <img src={heroImg} className="hero-image" alt="Retail store operations" />
      </section>
      <section className="signal-grid" aria-label="Workspace areas">
        <article><span>01</span><h2>Sales pulse</h2><p>Spot changes before they become surprises.</p></article>
        <article><span>02</span><h2>Stock clarity</h2><p>Understand movement, gaps, and replenishment.</p></article>
        <article><span>03</span><h2>Store health</h2><p>Know when sync or operations need a hand.</p></article>
      </section>
    </main>
  )
}

export default App
