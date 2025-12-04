// CoralLedger Blue Service Worker
// Version: 1.0.0
const CACHE_NAME = 'coralledger-blue-v1';
const OFFLINE_URL = '/offline.html';

// Static assets to cache immediately on install
const STATIC_ASSETS = [
    '/',
    '/manifest.json',
    '/favicon.png',
    '/app.css',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/offline.html'
];

// API routes to cache with network-first strategy
const API_CACHE_ROUTES = [
    '/api/mpas',
    '/api/mpas/geojson'
];

// Install event - cache static assets
self.addEventListener('install', (event) => {
    console.log('[SW] Installing service worker...');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('[SW] Caching static assets');
                return cache.addAll(STATIC_ASSETS);
            })
            .then(() => self.skipWaiting())
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    console.log('[SW] Activating service worker...');
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => name !== CACHE_NAME)
                        .map((name) => {
                            console.log('[SW] Deleting old cache:', name);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => self.clients.claim())
    );
});

// Fetch event - network-first for API, cache-first for static
self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') {
        return;
    }

    // Skip Blazor framework files (let Blazor handle these)
    if (url.pathname.startsWith('/_framework/') ||
        url.pathname.startsWith('/_blazor')) {
        return;
    }

    // API requests - network first, fall back to cache
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(networkFirstStrategy(request));
        return;
    }

    // Static assets - cache first, fall back to network
    event.respondWith(cacheFirstStrategy(request));
});

// Network-first strategy for API calls
async function networkFirstStrategy(request) {
    try {
        const networkResponse = await fetch(request);

        // Cache successful API responses
        if (networkResponse.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }

        return networkResponse;
    } catch (error) {
        console.log('[SW] Network failed, checking cache:', request.url);
        const cachedResponse = await caches.match(request);

        if (cachedResponse) {
            return cachedResponse;
        }

        // Return offline JSON response for API calls
        return new Response(
            JSON.stringify({ error: 'Offline', message: 'No cached data available' }),
            {
                status: 503,
                headers: { 'Content-Type': 'application/json' }
            }
        );
    }
}

// Cache-first strategy for static assets
async function cacheFirstStrategy(request) {
    const cachedResponse = await caches.match(request);

    if (cachedResponse) {
        // Return cached response and update cache in background
        fetchAndCache(request);
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(request);

        if (networkResponse.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }

        return networkResponse;
    } catch (error) {
        console.log('[SW] Offline and no cache for:', request.url);

        // Return offline page for navigation requests
        if (request.mode === 'navigate') {
            return caches.match(OFFLINE_URL);
        }

        return new Response('Offline', { status: 503 });
    }
}

// Background fetch and cache update
async function fetchAndCache(request) {
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }
    } catch (error) {
        // Silently fail background updates
    }
}

// Handle background sync for offline observations
self.addEventListener('sync', (event) => {
    if (event.tag === 'sync-observations') {
        console.log('[SW] Syncing offline observations...');
        event.waitUntil(syncOfflineObservations());
    }
});

async function syncOfflineObservations() {
    // This will be implemented when IndexedDB storage is added
    // For now, just log the sync attempt
    console.log('[SW] Background sync triggered for observations');
}

// Handle push notifications (for future bleaching alerts)
self.addEventListener('push', (event) => {
    if (!event.data) return;

    const data = event.data.json();
    const options = {
        body: data.body || 'New alert from CoralLedger Blue',
        icon: '/images/icons/icon-192x192.png',
        badge: '/images/icons/icon-72x72.png',
        vibrate: [100, 50, 100],
        data: {
            url: data.url || '/'
        }
    };

    event.waitUntil(
        self.registration.showNotification(data.title || 'CoralLedger Blue', options)
    );
});

// Handle notification clicks
self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const url = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window' })
            .then((windowClients) => {
                // Focus existing window if open
                for (const client of windowClients) {
                    if (client.url === url && 'focus' in client) {
                        return client.focus();
                    }
                }
                // Otherwise open new window
                if (clients.openWindow) {
                    return clients.openWindow(url);
                }
            })
    );
});
