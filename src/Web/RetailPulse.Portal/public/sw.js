const CACHE_NAME = 'retailpulse-portal-v2'
const OFFLINE_URL = '/offline.html'

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll([OFFLINE_URL])))
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))),
  )
  self.clients.claim()
})

self.addEventListener('fetch', (event) => {
  if (event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(() => caches.match(OFFLINE_URL)))
  }
})

self.addEventListener('push', (event) => {
  const payload = event.data?.json() ?? {
    title: 'RetailPulse alert',
    body: 'A new operational alert is available.',
    url: '/#alerts',
  }
  event.waitUntil(self.registration.showNotification(payload.title, {
    body: payload.body,
    tag: payload.tag ?? 'retailpulse-operational-alert',
    data: { url: payload.url ?? '/#alerts' },
  }))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = new URL(event.notification.data?.url ?? '/#alerts', self.location.origin).href
  event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
    const existing = clients.find((client) => 'focus' in client)
    if (existing) {
      existing.navigate(url)
      return existing.focus()
    }
    return self.clients.openWindow(url)
  }))
})