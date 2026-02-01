# Offline Map Tile Caching

## Overview
CoralLedger Blue now supports offline map tile caching, allowing field workers to access maps in remote areas without internet connectivity.

## Features

### 1. Automatic Tile Caching
- When online, tiles are automatically cached as you browse the map
- Cached tiles are stored in the browser's IndexedDB for persistence
- Cache works transparently - no user action required for basic caching

### 2. Region Download
- **Select Region**: Users can download tiles for the current map view
- **Zoom Levels**: Choose zoom levels to download (recommended: 8-13 for Bahamas)
- **Storage Estimation**: See estimated tile count and storage size before downloading
- **Progress Tracking**: Real-time progress display during download

### 3. Offline Indicator
- A visual indicator appears on the map when offline
- Located in the top-right corner of the map
- Shows "⚠️ Offline Mode" when no internet connection

### 4. Cache Management
- **View Statistics**: See total tiles cached and storage used
- **Cached Regions**: Browse downloaded regions by theme and zoom level
- **Clear Old Tiles**: Remove tiles older than 30 days
- **Clear All**: Remove all cached tiles

## User Guide

### Downloading Tiles for Offline Use

1. **Open the Map Page**
   - Navigate to `/map` in the application

2. **Open Offline Maps Panel**
   - In the right sidebar, click "Offline Maps" button
   - The panel will expand to show offline map options

3. **Configure Download**
   - Select map theme (Dark, Light, or Satellite)
   - Set zoom range (Min: 8, Max: 13 recommended for Bahamas MPAs)
   - Click "Estimate Current View" to see storage requirements

4. **Download Tiles**
   - Click "Download Current View"
   - Progress bar shows download status
   - Wait for completion notification

5. **Use Offline**
   - Tiles are now available when offline
   - Map will automatically use cached tiles when no connection

### Managing Cache Storage

**View Cache Statistics**
- Total tiles cached
- Storage space used (in MB)
- Last update timestamp

**Clear Old Tiles**
- Click "Clear Tiles Older Than 30 Days"
- Automatically removes expired tiles

**Clear All Cache**
- Click "Clear All Cache"
- Confirmation dialog prevents accidental deletion
- Removes all cached tiles

## Technical Details

### Storage Technology
- **IndexedDB**: Browser-native persistent storage
- **Automatic Cleanup**: Tiles older than 30 days can be removed
- **Cross-session**: Cache persists across browser sessions

### Tile Sources
Supports all three map themes:
1. **Dark (CartoDB)**: `https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png`
2. **Light (OpenStreetMap)**: `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`
3. **Satellite (Esri)**: `https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}`

### Cache Strategy
1. **Check Cache First**: When loading a tile, check IndexedDB first
2. **Fallback to Network**: If not cached and online, fetch from network
3. **Store on Success**: Automatically cache successfully fetched tiles
4. **Offline Error**: Show error message when offline and tile not cached

### Storage Estimates
- **Zoom 8-13** for Bahamas: ~2,000-5,000 tiles (~30-75 MB)
- **Average tile size**: 15 KB
- Actual size varies by map theme and region

## Development

### JavaScript Files
- `/wwwroot/js/tile-cache.js`: IndexedDB cache manager
- `/wwwroot/js/leaflet-map.js`: Leaflet integration with offline support

### Blazor Components
- `/Components/Shared/OfflineMapManager.razor`: UI for cache management
- `/Components/Pages/Map.razor`: Map page with offline manager integration
- `/Components/LeafletMapComponent.razor`: Map component with offline indicator

### Key Functions

**JavaScript API** (`window.tileCache`):
- `initialize()`: Initialize IndexedDB
- `storeTile(theme, z, x, y, blob)`: Store a tile
- `getTile(theme, z, x, y)`: Retrieve a tile
- `downloadRegion(theme, bounds, minZoom, maxZoom, tileUrl, callback)`: Download tiles for region
- `estimateRegionSize(bounds, minZoom, maxZoom)`: Estimate storage requirements
- `clearAll()`: Clear all cached tiles
- `clearOldTiles(maxAge)`: Clear tiles older than specified age
- `getStats()`: Get cache statistics

**Leaflet Integration** (`window.leafletMap`):
- `downloadCurrentView(mapId, minZoom, maxZoom, dotNetHelper)`: Download tiles for current view
- `estimateCurrentViewSize(mapId, minZoom, maxZoom)`: Estimate current view storage
- `getCacheStats()`: Get cache statistics
- `clearAllCache()`: Clear all cached tiles
- `addOfflineIndicator(mapId)`: Add offline indicator to map

## Browser Compatibility
- Chrome/Edge: ✅ Full support
- Firefox: ✅ Full support
- Safari: ✅ Full support
- Mobile browsers: ✅ Full support (iOS Safari, Chrome Mobile)

## Limitations
- Storage is limited by browser quotas (typically 50-100 MB or more)
- Downloading large regions can take time
- Network usage during download should be considered
- Cache persists until manually cleared or browser data cleared

## Best Practices

1. **Download Before Going Offline**
   - Plan ahead and download needed regions while online
   - Test offline access before heading to remote areas

2. **Manage Storage**
   - Regularly clear old tiles
   - Download only necessary zoom levels
   - Monitor cache size

3. **Zoom Level Selection**
   - **8-10**: Overview of entire region
   - **11-13**: Detailed view of MPAs
   - **14+**: Very detailed, but large storage

4. **Update Cache Regularly**
   - Clear and re-download tiles periodically
   - Ensures latest map data

## Troubleshooting

**Tiles Not Loading Offline**
- Check if tiles were downloaded for current zoom level
- Verify cache is not full
- Check browser storage permissions

**Download Fails**
- Check internet connection
- Verify sufficient storage space
- Try smaller region or fewer zoom levels

**Cache Not Persisting**
- Check browser private/incognito mode (cache may not persist)
- Verify browser storage not cleared automatically
- Check browser storage settings

## Future Enhancements
- Automatic background sync when online
- MBTiles format support for better compression
- Selective region download (draw on map)
- Service worker integration for better offline support
- Automatic cache optimization
