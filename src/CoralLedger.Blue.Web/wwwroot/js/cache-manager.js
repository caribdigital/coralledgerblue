/**
 * Cache Manager - Provides cache statistics and management for PWA
 * Exposes methods for Blazor interop to view and clear cached data
 */
window.cacheManager = (function() {
    const CACHE_NAMES = {
        static: 'coralledger-static-v5',
        api: 'coralledger-api-v5',
        tiles: 'coralledger-tiles-v5'
    };
    const DB_NAME = 'coralledger-offline';

    /**
     * Get cache statistics for all caches
     * @returns {Promise<Object>} Cache statistics
     */
    async function getCacheStats() {
        const stats = {
            static: { count: 0, size: 0 },
            api: { count: 0, size: 0 },
            tiles: { count: 0, size: 0 },
            drafts: { count: 0 },
            totalSize: 0
        };

        try {
            // Get cache storage stats
            for (const [key, cacheName] of Object.entries(CACHE_NAMES)) {
                try {
                    const cache = await caches.open(cacheName);
                    const keys = await cache.keys();
                    stats[key].count = keys.length;

                    // Estimate size by sampling responses
                    let totalSize = 0;
                    const sampleSize = Math.min(keys.length, 20);
                    for (let i = 0; i < sampleSize; i++) {
                        try {
                            const response = await cache.match(keys[i]);
                            if (response) {
                                const blob = await response.clone().blob();
                                totalSize += blob.size;
                            }
                        } catch { }
                    }

                    // Extrapolate if we sampled
                    if (sampleSize > 0 && keys.length > sampleSize) {
                        totalSize = Math.round(totalSize * (keys.length / sampleSize));
                    }
                    stats[key].size = totalSize;
                } catch { }
            }

            // Get IndexedDB draft count
            stats.drafts.count = await getDraftCount();

            // Calculate total
            stats.totalSize = stats.static.size + stats.api.size + stats.tiles.size;

        } catch (err) {
            console.warn('[CacheManager] Error getting stats:', err);
        }

        return stats;
    }

    /**
     * Get count of drafts in IndexedDB
     */
    async function getDraftCount() {
        return new Promise((resolve) => {
            try {
                const request = indexedDB.open(DB_NAME, 1);
                request.onerror = () => resolve(0);
                request.onsuccess = (event) => {
                    const db = event.target.result;
                    if (!db.objectStoreNames.contains('drafts')) {
                        db.close();
                        resolve(0);
                        return;
                    }
                    const tx = db.transaction('drafts', 'readonly');
                    const store = tx.objectStore('drafts');
                    const countReq = store.count();
                    countReq.onsuccess = () => resolve(countReq.result);
                    countReq.onerror = () => resolve(0);
                    tx.oncomplete = () => db.close();
                };
            } catch {
                resolve(0);
            }
        });
    }

    /**
     * Clear a specific cache
     * @param {string} cacheType - 'static', 'api', 'tiles', or 'all'
     */
    async function clearCache(cacheType) {
        try {
            if (cacheType === 'all') {
                await Promise.all([
                    caches.delete(CACHE_NAMES.static),
                    caches.delete(CACHE_NAMES.api),
                    caches.delete(CACHE_NAMES.tiles)
                ]);
                console.log('[CacheManager] All caches cleared');
                return true;
            }

            const cacheName = CACHE_NAMES[cacheType];
            if (cacheName) {
                await caches.delete(cacheName);
                console.log(`[CacheManager] ${cacheType} cache cleared`);
                return true;
            }

            return false;
        } catch (err) {
            console.error('[CacheManager] Error clearing cache:', err);
            return false;
        }
    }

    /**
     * Clear all drafts from IndexedDB
     */
    async function clearDrafts() {
        return new Promise((resolve) => {
            try {
                const request = indexedDB.open(DB_NAME, 1);
                request.onerror = () => resolve(false);
                request.onsuccess = (event) => {
                    const db = event.target.result;
                    if (!db.objectStoreNames.contains('drafts')) {
                        db.close();
                        resolve(true);
                        return;
                    }
                    const tx = db.transaction('drafts', 'readwrite');
                    const store = tx.objectStore('drafts');
                    store.clear();
                    tx.oncomplete = () => {
                        db.close();
                        console.log('[CacheManager] Drafts cleared');
                        resolve(true);
                    };
                    tx.onerror = () => {
                        db.close();
                        resolve(false);
                    };
                };
            } catch {
                resolve(false);
            }
        });
    }

    /**
     * Format bytes to human-readable string
     * @param {number} bytes
     */
    function formatBytes(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    /**
     * Get storage estimate from browser
     */
    async function getStorageEstimate() {
        if ('storage' in navigator && 'estimate' in navigator.storage) {
            try {
                const estimate = await navigator.storage.estimate();
                return {
                    usage: estimate.usage || 0,
                    quota: estimate.quota || 0,
                    usageFormatted: formatBytes(estimate.usage || 0),
                    quotaFormatted: formatBytes(estimate.quota || 0),
                    percentUsed: estimate.quota ? Math.round((estimate.usage / estimate.quota) * 100) : 0
                };
            } catch {
                return null;
            }
        }
        return null;
    }

    /**
     * Pre-cache map tiles for a specific bounding box (Bahamas)
     * @param {Function} progressCallback - Called with progress updates
     */
    async function precacheBahamasTiles(progressCallback) {
        const BAHAMAS_BOUNDS = {
            north: 27.5,
            south: 20.9,
            east: -72.7,
            west: -80.5
        };
        const ZOOM_LEVELS = [5, 6, 7, 8]; // Low to medium zoom for overview
        const TILE_URL = 'https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png';

        let total = 0;
        let cached = 0;

        // Calculate tiles needed
        const tiles = [];
        for (const z of ZOOM_LEVELS) {
            const n = Math.pow(2, z);
            const xMin = Math.floor((BAHAMAS_BOUNDS.west + 180) / 360 * n);
            const xMax = Math.floor((BAHAMAS_BOUNDS.east + 180) / 360 * n);
            const yMin = Math.floor((1 - Math.log(Math.tan(BAHAMAS_BOUNDS.north * Math.PI / 180) + 1 / Math.cos(BAHAMAS_BOUNDS.north * Math.PI / 180)) / Math.PI) / 2 * n);
            const yMax = Math.floor((1 - Math.log(Math.tan(BAHAMAS_BOUNDS.south * Math.PI / 180) + 1 / Math.cos(BAHAMAS_BOUNDS.south * Math.PI / 180)) / Math.PI) / 2 * n);

            for (let x = xMin; x <= xMax; x++) {
                for (let y = yMin; y <= yMax; y++) {
                    tiles.push({ z, x, y });
                }
            }
        }

        total = tiles.length;
        if (progressCallback) progressCallback(0, total);

        try {
            const cache = await caches.open(CACHE_NAMES.tiles);

            // Batch fetch tiles
            const batchSize = 10;
            for (let i = 0; i < tiles.length; i += batchSize) {
                const batch = tiles.slice(i, i + batchSize);
                await Promise.all(batch.map(async ({ z, x, y }) => {
                    const url = TILE_URL.replace('{z}', z).replace('{x}', x).replace('{y}', y);
                    try {
                        const response = await fetch(url);
                        if (response.ok) {
                            await cache.put(url, response);
                            cached++;
                        }
                    } catch { }
                }));

                if (progressCallback) progressCallback(cached, total);
            }
        } catch (err) {
            console.error('[CacheManager] Error pre-caching tiles:', err);
        }

        return { cached, total };
    }

    return {
        getCacheStats,
        clearCache,
        clearDrafts,
        getStorageEstimate,
        precacheBahamasTiles,
        formatBytes
    };
})();
