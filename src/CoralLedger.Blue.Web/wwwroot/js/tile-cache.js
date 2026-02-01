// Offline Map Tile Cache Manager for CoralLedger Blue
// Uses IndexedDB for persistent tile storage

window.tileCache = {
    dbName: 'CoralLedgerTileCache',
    dbVersion: 1,
    storeName: 'tiles',
    db: null,
    
    // Cache statistics
    stats: {
        totalTiles: 0,
        totalBytes: 0,
        lastUpdated: null
    },

    // Initialize IndexedDB
    async initialize() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.dbVersion);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                this.db = request.result;
                console.log('[tile-cache] IndexedDB initialized');
                this.updateStats().then(() => resolve(true));
            };
            
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                
                // Create object store for tiles
                if (!db.objectStoreNames.contains(this.storeName)) {
                    const objectStore = db.createObjectStore(this.storeName, { keyPath: 'key' });
                    
                    // Indexes for efficient querying
                    objectStore.createIndex('theme', 'theme', { unique: false });
                    objectStore.createIndex('z', 'z', { unique: false });
                    objectStore.createIndex('timestamp', 'timestamp', { unique: false });
                    
                    console.log('[tile-cache] Object store created');
                }
            };
        });
    },

    // Generate unique key for a tile
    getTileKey(theme, z, x, y) {
        return `${theme}_${z}_${x}_${y}`;
    },

    // Store a tile in the cache
    async storeTile(theme, z, x, y, blob) {
        if (!this.db) {
            await this.initialize();
        }

        const key = this.getTileKey(theme, z, x, y);
        const tile = {
            key: key,
            theme: theme,
            z: z,
            x: x,
            y: y,
            blob: blob,
            timestamp: Date.now(),
            size: blob.size
        };

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const objectStore = transaction.objectStore(this.storeName);
            const request = objectStore.put(tile);
            
            request.onsuccess = () => {
                this.stats.totalTiles++;
                this.stats.totalBytes += blob.size;
                resolve(true);
            };
            request.onerror = () => reject(request.error);
        });
    },

    // Retrieve a tile from the cache
    async getTile(theme, z, x, y) {
        if (!this.db) {
            await this.initialize();
        }

        const key = this.getTileKey(theme, z, x, y);
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readonly');
            const objectStore = transaction.objectStore(this.storeName);
            const request = objectStore.get(key);
            
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    },

    // Check if a tile exists in cache
    async hasTile(theme, z, x, y) {
        const tile = await this.getTile(theme, z, x, y);
        return !!tile;
    },

    // Download a single tile from the server
    async downloadTile(theme, z, x, y, tileUrl) {
        try {
            // Check if already cached
            if (await this.hasTile(theme, z, x, y)) {
                return { cached: true };
            }

            // Download the tile
            const url = this.buildTileUrl(tileUrl, z, x, y);
            const response = await fetch(url);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const blob = await response.blob();
            await this.storeTile(theme, z, x, y, blob);
            
            return { cached: false, size: blob.size };
        } catch (error) {
            console.error(`[tile-cache] Error downloading tile ${z}/${x}/${y}:`, error);
            throw error;
        }
    },

    // Build tile URL from template
    buildTileUrl(template, z, x, y) {
        // Handle {s} subdomain - use 'a' by default
        let url = template.replace('{s}', 'a');
        url = url.replace('{z}', z);
        url = url.replace('{x}', x);
        url = url.replace('{y}', y);
        url = url.replace('{r}', ''); // retina placeholder
        return url;
    },

    // Calculate tiles needed for a bounding box at specific zoom levels
    getTilesForRegion(bounds, minZoom, maxZoom) {
        const tiles = [];
        
        for (let z = minZoom; z <= maxZoom; z++) {
            const nwTile = this.latLngToTile(bounds.north, bounds.west, z);
            const seTile = this.latLngToTile(bounds.south, bounds.east, z);
            
            for (let x = Math.min(nwTile.x, seTile.x); x <= Math.max(nwTile.x, seTile.x); x++) {
                for (let y = Math.min(nwTile.y, seTile.y); y <= Math.max(nwTile.y, seTile.y); y++) {
                    tiles.push({ z, x, y });
                }
            }
        }
        
        return tiles;
    },

    // Convert lat/lng to tile coordinates
    latLngToTile(lat, lng, zoom) {
        const n = Math.pow(2, zoom);
        const x = Math.floor((lng + 180) / 360 * n);
        const latRad = lat * Math.PI / 180;
        const y = Math.floor((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2 * n);
        return { x, y };
    },

    // Download tiles for a region
    async downloadRegion(theme, bounds, minZoom, maxZoom, tileUrl, progressCallback) {
        const tiles = this.getTilesForRegion(bounds, minZoom, maxZoom);
        const total = tiles.length;
        let downloaded = 0;
        let cached = 0;
        let failed = 0;
        let totalBytes = 0;

        console.log(`[tile-cache] Downloading ${total} tiles for region`);

        for (let i = 0; i < tiles.length; i++) {
            const { z, x, y } = tiles[i];
            
            try {
                const result = await this.downloadTile(theme, z, x, y, tileUrl);
                
                if (result.cached) {
                    cached++;
                } else {
                    downloaded++;
                    totalBytes += result.size;
                }

                // Report progress
                if (progressCallback) {
                    progressCallback({
                        current: i + 1,
                        total: total,
                        downloaded: downloaded,
                        cached: cached,
                        failed: failed,
                        percentComplete: Math.round((i + 1) / total * 100),
                        totalBytes: totalBytes
                    });
                }
            } catch (error) {
                failed++;
                console.error(`[tile-cache] Failed to download tile ${z}/${x}/${y}:`, error);
            }
        }

        await this.updateStats();

        return {
            total: total,
            downloaded: downloaded,
            cached: cached,
            failed: failed,
            totalBytes: totalBytes
        };
    },

    // Estimate storage size for a region
    estimateRegionSize(bounds, minZoom, maxZoom, avgTileSize = 15000) {
        const tiles = this.getTilesForRegion(bounds, minZoom, maxZoom);
        return {
            tileCount: tiles.length,
            estimatedBytes: tiles.length * avgTileSize,
            estimatedMB: Math.round(tiles.length * avgTileSize / 1024 / 1024 * 10) / 10
        };
    },

    // Update cache statistics
    async updateStats() {
        if (!this.db) return;

        return new Promise((resolve) => {
            const transaction = this.db.transaction([this.storeName], 'readonly');
            const objectStore = transaction.objectStore(this.storeName);
            
            let totalBytes = 0;
            let totalTiles = 0;
            
            const request = objectStore.openCursor();
            
            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    totalTiles++;
                    totalBytes += cursor.value.size || 0;
                    cursor.continue();
                } else {
                    this.stats.totalTiles = totalTiles;
                    this.stats.totalBytes = totalBytes;
                    this.stats.lastUpdated = Date.now();
                    resolve(this.stats);
                }
            };
        });
    },

    // Get cache statistics
    async getStats() {
        await this.updateStats();
        return {
            ...this.stats,
            totalMB: Math.round(this.stats.totalBytes / 1024 / 1024 * 10) / 10
        };
    },

    // Clear all cached tiles
    async clearAll() {
        if (!this.db) {
            await this.initialize();
        }

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const objectStore = transaction.objectStore(this.storeName);
            const request = objectStore.clear();
            
            request.onsuccess = () => {
                this.stats.totalTiles = 0;
                this.stats.totalBytes = 0;
                console.log('[tile-cache] All tiles cleared');
                resolve(true);
            };
            request.onerror = () => reject(request.error);
        });
    },

    // Clear tiles by theme
    async clearByTheme(theme) {
        if (!this.db) {
            await this.initialize();
        }

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const objectStore = transaction.objectStore(this.storeName);
            const index = objectStore.index('theme');
            const request = index.openCursor(IDBKeyRange.only(theme));
            
            let deletedCount = 0;
            
            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    cursor.delete();
                    deletedCount++;
                    cursor.continue();
                } else {
                    console.log(`[tile-cache] Deleted ${deletedCount} tiles for theme ${theme}`);
                    this.updateStats().then(() => resolve(deletedCount));
                }
            };
            request.onerror = () => reject(request.error);
        });
    },

    // Clear old tiles based on age (milliseconds)
    async clearOldTiles(maxAge) {
        if (!this.db) {
            await this.initialize();
        }

        const cutoffTime = Date.now() - maxAge;
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const objectStore = transaction.objectStore(this.storeName);
            const index = objectStore.index('timestamp');
            const request = index.openCursor(IDBKeyRange.upperBound(cutoffTime));
            
            let deletedCount = 0;
            
            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    cursor.delete();
                    deletedCount++;
                    cursor.continue();
                } else {
                    console.log(`[tile-cache] Deleted ${deletedCount} old tiles`);
                    this.updateStats().then(() => resolve(deletedCount));
                }
            };
            request.onerror = () => reject(request.error);
        });
    },

    // Check if browser is online
    isOnline() {
        return navigator.onLine;
    },

    // Get cached regions summary
    async getCachedRegions() {
        if (!this.db) {
            await this.initialize();
        }

        return new Promise((resolve) => {
            const transaction = this.db.transaction([this.storeName], 'readonly');
            const objectStore = transaction.objectStore(this.storeName);
            
            const regions = {};
            
            const request = objectStore.openCursor();
            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    const tile = cursor.value;
                    const key = `${tile.theme}_z${tile.z}`;
                    
                    if (!regions[key]) {
                        regions[key] = {
                            theme: tile.theme,
                            zoom: tile.z,
                            tileCount: 0,
                            bytes: 0
                        };
                    }
                    
                    regions[key].tileCount++;
                    regions[key].bytes += tile.size || 0;
                    
                    cursor.continue();
                } else {
                    resolve(Object.values(regions));
                }
            };
        });
    }
};

// Auto-initialize on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.tileCache.initialize().catch(console.error);
    });
} else {
    window.tileCache.initialize().catch(console.error);
}
