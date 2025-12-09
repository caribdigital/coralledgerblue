// CoralLedger Blue Service Worker
// Version: 4.0.0 - Sprint 4.1: PWA Offline Support & Geometry Pre-caching
const CACHE_VERSION = 'v4';
const STATIC_CACHE = `coralledger-static-${CACHE_VERSION}`;
const API_CACHE = `coralledger-api-${CACHE_VERSION}`;
const IMAGE_CACHE = `coralledger-images-${CACHE_VERSION}`;
const GEOMETRY_CACHE = `coralledger-geometry-${CACHE_VERSION}`;
const OFFLINE_URL = '/offline.html';

// Static assets to cache immediately on install
const STATIC_ASSETS = [
    '/',
    '/manifest.json',
    '/favicon.png',
    '/app.css',
    '/CoralLedger.Web.styles.css',
    '/css/mobile.css',
    '/js/mobile.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/offline.html'
];

// Geometry endpoints to pre-cache for offline map rendering (Sprint 4.1 US-4.1.3)
const GEOMETRY_ENDPOINTS = [
    '/api/mpas/geojson?resolution=low',      // Low resolution for offline
    '/api/mpas/geojson?resolution=medium',   // Medium for better connectivity
    '/api/mpas',                              // MPA list
    '/api/mpas/stats'                         // Statistics
];

// API routes with caching configuration
const API_CACHE_CONFIG = {
    '/api/mpas': { maxAge: 3600000, strategy: 'stale-while-revalidate' },          // 1 hour
    '/api/mpas/geojson': { maxAge: 3600000, strategy: 'stale-while-revalidate' },  // 1 hour
    '/api/mpas/stats': { maxAge: 300000, strategy: 'network-first' },               // 5 min
    '/api/alerts': { maxAge: 60000, strategy: 'network-first' },                    // 1 min
    '/api/bleaching': { maxAge: 1800000, strategy: 'stale-while-revalidate' },     // 30 min
    '/api/ais/vessels': { maxAge: 30000, strategy: 'network-first' }                // 30 sec
};

// IndexedDB for offline data
const DB_NAME = 'coralledger-offline';
const DB_VERSION = 1;
let db = null;

// ==========================================================================
// IndexedDB Setup
// ==========================================================================
async function openDB() {
    if (db) return db;

    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            db = request.result;
            resolve(db);
        };

        request.onupgradeneeded = (event) => {
            const database = event.target.result;

            // Store for pending observations
            if (!database.objectStoreNames.contains('pendingObservations')) {
                const store = database.createObjectStore('pendingObservations', { keyPath: 'id', autoIncrement: true });
                store.createIndex('createdAt', 'createdAt');
            }

            // Store for cached API responses with metadata
            if (!database.objectStoreNames.contains('apiCache')) {
                const apiStore = database.createObjectStore('apiCache', { keyPath: 'url' });
                apiStore.createIndex('cachedAt', 'cachedAt');
            }

            // Store for user preferences
            if (!database.objectStoreNames.contains('preferences')) {
                database.createObjectStore('preferences', { keyPath: 'key' });
            }
        };
    });
}

// ==========================================================================
// Service Worker Events
// ==========================================================================
self.addEventListener('install', (event) => {
    console.log('[SW] Installing service worker v4...');
    event.waitUntil(
        Promise.all([
            caches.open(STATIC_CACHE).then((cache) => {
                console.log('[SW] Caching static assets');
                return cache.addAll(STATIC_ASSETS);
            }),
            openDB(),
            // Pre-cache geometry for offline map support (Sprint 4.1 US-4.1.3)
            preCacheGeometry()
        ]).then(() => self.skipWaiting())
    );
});

// Pre-cache geometry endpoints for offline map rendering
async function preCacheGeometry() {
    console.log('[SW] Pre-caching geometry for offline maps...');
    const cache = await caches.open(GEOMETRY_CACHE);

    for (const endpoint of GEOMETRY_ENDPOINTS) {
        try {
            const response = await fetch(endpoint);
            if (response.ok) {
                // Add cache metadata
                const headers = new Headers(response.headers);
                headers.set('sw-cached-at', Date.now().toString());
                headers.set('sw-cache-type', 'geometry');

                await cache.put(endpoint, new Response(await response.blob(), {
                    status: response.status,
                    statusText: response.statusText,
                    headers: headers
                }));
                console.log(`[SW] Pre-cached: ${endpoint}`);
            }
        } catch (error) {
            console.log(`[SW] Failed to pre-cache ${endpoint}:`, error.message);
            // Don't fail install if geometry pre-caching fails
        }
    }
}

self.addEventListener('activate', (event) => {
    console.log('[SW] Activating service worker v4...');
    event.waitUntil(
        Promise.all([
            // Clean up old caches
            caches.keys().then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => {
                            return name.startsWith('coralledger-') &&
                                   name !== STATIC_CACHE &&
                                   name !== API_CACHE &&
                                   name !== IMAGE_CACHE &&
                                   name !== GEOMETRY_CACHE;
                        })
                        .map((name) => {
                            console.log('[SW] Deleting old cache:', name);
                            return caches.delete(name);
                        })
                );
            }),
            // Claim all clients
            self.clients.claim()
        ])
    );
});

self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') {
        return;
    }

    // Skip Blazor framework files
    if (url.pathname.startsWith('/_framework/') ||
        url.pathname.startsWith('/_blazor') ||
        url.pathname.includes('.dll') ||
        url.pathname.includes('.wasm')) {
        return;
    }

    // Handle different resource types
    if (isGeometryRequest(url)) {
        event.respondWith(handleGeometryRequest(request, url));
    } else if (url.pathname.startsWith('/api/')) {
        event.respondWith(handleApiRequest(request, url));
    } else if (isImageRequest(request)) {
        event.respondWith(handleImageRequest(request));
    } else {
        event.respondWith(handleStaticRequest(request));
    }
});

// Check if request is for geometry data (Sprint 4.1 US-4.1.3)
function isGeometryRequest(url) {
    return url.pathname.includes('/api/mpas/geojson') ||
           (url.pathname === '/api/mpas' && url.search === '');
}

// Handle geometry requests with offline-first strategy
async function handleGeometryRequest(request, url) {
    // Try geometry cache first for offline support
    const geometryCache = await caches.open(GEOMETRY_CACHE);
    const cachedResponse = await geometryCache.match(request);

    if (cachedResponse) {
        // Refresh cache in background
        fetch(request)
            .then(async (networkResponse) => {
                if (networkResponse.ok) {
                    const headers = new Headers(networkResponse.headers);
                    headers.set('sw-cached-at', Date.now().toString());
                    headers.set('sw-cache-type', 'geometry');
                    await geometryCache.put(request, new Response(await networkResponse.blob(), {
                        status: networkResponse.status,
                        statusText: networkResponse.statusText,
                        headers: headers
                    }));
                }
            })
            .catch(() => { /* Ignore background fetch errors */ });

        return cachedResponse;
    }

    // No cache, try network
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            const headers = new Headers(networkResponse.headers);
            headers.set('sw-cached-at', Date.now().toString());
            headers.set('sw-cache-type', 'geometry');
            await geometryCache.put(request, new Response(await networkResponse.clone().blob(), {
                status: networkResponse.status,
                statusText: networkResponse.statusText,
                headers: headers
            }));
        }
        return networkResponse;
    } catch (error) {
        // Return offline response for geometry
        return new Response(
            JSON.stringify({
                error: 'Offline',
                message: 'Geometry data not available offline',
                offline: true
            }),
            {
                status: 503,
                headers: { 'Content-Type': 'application/json' }
            }
        );
    }
}

// ==========================================================================
// Request Handlers
// ==========================================================================
function isImageRequest(request) {
    const url = new URL(request.url);
    return /\.(jpg|jpeg|png|gif|webp|svg|ico)$/i.test(url.pathname) ||
           request.destination === 'image';
}

async function handleApiRequest(request, url) {
    const config = getApiConfig(url.pathname);

    if (config.strategy === 'stale-while-revalidate') {
        return staleWhileRevalidate(request, config);
    } else {
        return networkFirst(request, config);
    }
}

function getApiConfig(pathname) {
    for (const [route, config] of Object.entries(API_CACHE_CONFIG)) {
        if (pathname.startsWith(route)) {
            return config;
        }
    }
    return { maxAge: 60000, strategy: 'network-first' };
}

async function networkFirst(request, config) {
    try {
        const networkResponse = await fetch(request);

        if (networkResponse.ok) {
            const cache = await caches.open(API_CACHE);
            const responseToCache = networkResponse.clone();

            // Add cache metadata
            const headers = new Headers(responseToCache.headers);
            headers.set('sw-cached-at', Date.now().toString());

            cache.put(request, new Response(await responseToCache.blob(), {
                status: responseToCache.status,
                statusText: responseToCache.statusText,
                headers: headers
            }));
        }

        return networkResponse;
    } catch (error) {
        console.log('[SW] Network failed, checking cache:', request.url);
        return getCachedResponse(request, config.maxAge);
    }
}

async function staleWhileRevalidate(request, config) {
    const cache = await caches.open(API_CACHE);
    const cachedResponse = await cache.match(request);

    // Return cached response immediately if available
    const responsePromise = fetch(request)
        .then((networkResponse) => {
            if (networkResponse.ok) {
                const headers = new Headers(networkResponse.headers);
                headers.set('sw-cached-at', Date.now().toString());

                cache.put(request, new Response(networkResponse.clone().body, {
                    status: networkResponse.status,
                    statusText: networkResponse.statusText,
                    headers: headers
                }));
            }
            return networkResponse;
        })
        .catch(() => null);

    if (cachedResponse) {
        // Check if cache is still valid
        const cachedAt = parseInt(cachedResponse.headers.get('sw-cached-at') || '0');
        if (Date.now() - cachedAt < config.maxAge) {
            // Update in background
            responsePromise;
            return cachedResponse;
        }
    }

    // Wait for network if no valid cache
    const networkResponse = await responsePromise;
    return networkResponse || cachedResponse || offlineApiResponse();
}

async function getCachedResponse(request, maxAge) {
    const cache = await caches.open(API_CACHE);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        const cachedAt = parseInt(cachedResponse.headers.get('sw-cached-at') || '0');
        const isStale = Date.now() - cachedAt > maxAge;

        // Return stale data with warning header
        if (isStale) {
            const headers = new Headers(cachedResponse.headers);
            headers.set('sw-stale', 'true');
            return new Response(cachedResponse.body, {
                status: cachedResponse.status,
                headers: headers
            });
        }

        return cachedResponse;
    }

    return offlineApiResponse();
}

function offlineApiResponse() {
    return new Response(
        JSON.stringify({
            error: 'Offline',
            message: 'No cached data available',
            offline: true
        }),
        {
            status: 503,
            headers: { 'Content-Type': 'application/json' }
        }
    );
}

async function handleImageRequest(request) {
    const cache = await caches.open(IMAGE_CACHE);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            cache.put(request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        // Return placeholder for failed images
        return new Response('', { status: 404 });
    }
}

async function handleStaticRequest(request) {
    const cache = await caches.open(STATIC_CACHE);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            cache.put(request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        if (request.mode === 'navigate') {
            return cache.match(OFFLINE_URL);
        }
        return new Response('Offline', { status: 503 });
    }
}

// ==========================================================================
// Background Sync
// ==========================================================================
self.addEventListener('sync', (event) => {
    console.log('[SW] Sync event:', event.tag);

    if (event.tag === 'sync-observations') {
        event.waitUntil(syncPendingObservations());
    }
});

async function syncPendingObservations() {
    try {
        const database = await openDB();
        const tx = database.transaction('pendingObservations', 'readonly');
        const store = tx.objectStore('pendingObservations');

        return new Promise((resolve, reject) => {
            const request = store.getAll();

            request.onsuccess = async () => {
                const observations = request.result;
                console.log(`[SW] Found ${observations.length} pending observations to sync`);

                for (const obs of observations) {
                    try {
                        const response = await fetch('/api/observations', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(obs.data)
                        });

                        if (response.ok) {
                            // Remove synced observation
                            const deleteTx = database.transaction('pendingObservations', 'readwrite');
                            deleteTx.objectStore('pendingObservations').delete(obs.id);
                            console.log(`[SW] Synced observation ${obs.id}`);
                        }
                    } catch (error) {
                        console.log(`[SW] Failed to sync observation ${obs.id}:`, error);
                    }
                }

                resolve();
            };

            request.onerror = () => reject(request.error);
        });
    } catch (error) {
        console.error('[SW] Sync failed:', error);
    }
}

// ==========================================================================
// Push Notifications
// ==========================================================================
self.addEventListener('push', (event) => {
    if (!event.data) return;

    const data = event.data.json();
    const options = {
        body: data.body || 'New alert from CoralLedger Blue',
        icon: '/images/icons/icon-192x192.png',
        badge: '/images/icons/icon-72x72.png',
        vibrate: [100, 50, 100],
        tag: data.tag || 'default',
        renotify: true,
        requireInteraction: data.severity === 'high',
        data: {
            url: data.url || '/',
            alertId: data.alertId
        },
        actions: data.actions || [
            { action: 'view', title: 'View Details' },
            { action: 'dismiss', title: 'Dismiss' }
        ]
    };

    event.waitUntil(
        self.registration.showNotification(data.title || 'CoralLedger Blue Alert', options)
    );
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    if (event.action === 'dismiss') {
        return;
    }

    const url = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((windowClients) => {
                // Focus existing window if available
                for (const client of windowClients) {
                    if (new URL(client.url).pathname === url && 'focus' in client) {
                        return client.focus();
                    }
                }
                // Open new window
                if (clients.openWindow) {
                    return clients.openWindow(url);
                }
            })
    );
});

// ==========================================================================
// Message Handling (for Blazor communication)
// ==========================================================================
self.addEventListener('message', (event) => {
    if (event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }

    if (event.data.type === 'CACHE_URLS') {
        event.waitUntil(
            caches.open(STATIC_CACHE)
                .then((cache) => cache.addAll(event.data.urls))
        );
    }

    if (event.data.type === 'CLEAR_CACHE') {
        event.waitUntil(
            caches.keys().then((cacheNames) => {
                return Promise.all(
                    cacheNames.map((name) => caches.delete(name))
                );
            })
        );
    }

    if (event.data.type === 'STORE_OBSERVATION') {
        event.waitUntil(storeOfflineObservation(event.data.observation));
    }
});

async function storeOfflineObservation(observation) {
    const database = await openDB();
    const tx = database.transaction('pendingObservations', 'readwrite');
    const store = tx.objectStore('pendingObservations');

    store.add({
        data: observation,
        createdAt: Date.now()
    });

    // Request background sync
    if ('sync' in self.registration) {
        await self.registration.sync.register('sync-observations');
    }
}

console.log('[SW] CoralLedger Blue Service Worker v4 loaded - Sprint 4.1 PWA Offline Support');
