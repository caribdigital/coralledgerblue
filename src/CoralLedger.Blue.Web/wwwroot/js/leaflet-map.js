// Leaflet Map Interop for CoralLedger Blue

// Helper function to check if leafletMap is ready (called from Blazor)
window.isLeafletMapReady = function() {
    return typeof window.leafletMap !== 'undefined' &&
           typeof window.leafletMap.initialize === 'function' &&
           typeof L !== 'undefined';
};

window.leafletMap = {
    maps: {},
    mpaLayers: {},
    mpaPulseLayers: {},  // Animated pulse layers for NoTake zones
    fishingLayers: {},
    trajectoryLayers: {},  // Vessel trajectory lines
    tileLayers: {},
    legendControls: {},
    hoverInfoControls: {},

    // Tile layer definitions (US-2.2.1: Dark Map Base Layer)
    tileOptions: {
        dark: {
            url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
            name: 'Dark (CartoDB)'
        },
        light: {
            url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            name: 'Light (OpenStreetMap)'
        },
        satellite: {
            url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
            attribution: '&copy; <a href="https://www.esri.com/">Esri</a>',
            name: 'Satellite'
        }
    },

    // Check if Leaflet is loaded
    isLeafletReady: function() {
        return typeof L !== 'undefined';
    },

    // Create a tile layer with offline support
    createOfflineTileLayer: function(urlTemplate, theme, attribution) {
        const self = this;
        
        // Custom tile layer that checks cache first
        return L.tileLayer(urlTemplate, {
            maxZoom: 19,
            attribution: attribution,
            
            // Override createTile to check cache
            createTile: function(coords, done) {
                const tile = document.createElement('img');
                const z = coords.z;
                const x = coords.x;
                const y = coords.y;
                
                // Try to load from cache first
                if (window.tileCache && window.tileCache.db) {
                    window.tileCache.getTile(theme, z, x, y)
                        .then(cachedTile => {
                            if (cachedTile && cachedTile.blob) {
                                // Use cached tile
                                const url = URL.createObjectURL(cachedTile.blob);
                                tile.src = url;
                                tile.onload = () => {
                                    URL.revokeObjectURL(url);
                                    done(null, tile);
                                };
                                tile.onerror = () => {
                                    URL.revokeObjectURL(url);
                                    // Fallback to network
                                    self.loadTileFromNetwork(tile, urlTemplate, z, x, y, theme, done);
                                };
                            } else {
                                // Not in cache, load from network
                                self.loadTileFromNetwork(tile, urlTemplate, z, x, y, theme, done);
                            }
                        })
                        .catch(() => {
                            // Error accessing cache, load from network
                            self.loadTileFromNetwork(tile, urlTemplate, z, x, y, theme, done);
                        });
                } else {
                    // Cache not available, load from network
                    self.loadTileFromNetwork(tile, urlTemplate, z, x, y, theme, done);
                }
                
                return tile;
            }
        });
    },

    // Load tile from network and optionally cache it
    loadTileFromNetwork: function(tile, urlTemplate, z, x, y, theme, done) {
        // Only proceed if online
        if (!navigator.onLine) {
            tile.alt = 'Offline - tile not cached';
            done(new Error('Offline'), tile);
            return;
        }

        const url = this.buildTileUrl(urlTemplate, z, x, y);
        
        fetch(url)
            .then(response => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.blob();
            })
            .then(blob => {
                // Cache the tile if cache is available
                if (window.tileCache && window.tileCache.db) {
                    window.tileCache.storeTile(theme, z, x, y, blob)
                        .catch(err => console.warn('[leaflet-map] Failed to cache tile:', err));
                }
                
                // Display the tile
                const objectUrl = URL.createObjectURL(blob);
                tile.src = objectUrl;
                tile.onload = () => {
                    URL.revokeObjectURL(objectUrl);
                    done(null, tile);
                };
            })
            .catch(error => {
                console.error(`[leaflet-map] Failed to load tile ${z}/${x}/${y}:`, error);
                done(error, tile);
            });
    },

    // Build tile URL from template
    buildTileUrl: function(template, z, x, y) {
        // Handle {s} subdomain - use 'a' by default
        let url = template.replace('{s}', 'a');
        url = url.replace('{z}', z);
        url = url.replace('{x}', x);
        url = url.replace('{y}', y);
        url = url.replace('{r}', ''); // retina placeholder
        return url;
    },

    // Initialize a new map with dark theme support and offline capability
    initialize: function (mapId, centerLat, centerLng, zoom, useDarkTheme = true, enableOffline = true) {
        // Check if Leaflet is loaded
        if (!this.isLeafletReady()) {
            console.error('Leaflet library (L) is not loaded. Make sure leaflet.js is included before leaflet-map.js');
            throw new Error('Leaflet library not loaded');
        }
        if (this.maps[mapId]) {
            this.maps[mapId].remove();
        }

        const map = L.map(mapId).setView([centerLat, centerLng], zoom);

        // US-2.2.1: Use CartoDB Dark Matter by default for dark theme
        const tileConfig = useDarkTheme ? this.tileOptions.dark : this.tileOptions.light;
        const theme = useDarkTheme ? 'dark' : 'light';
        
        // Create tile layer with offline support if enabled
        const tileLayer = enableOffline && window.tileCache 
            ? this.createOfflineTileLayer(tileConfig.url, theme, tileConfig.attribution)
            : L.tileLayer(tileConfig.url, {
                maxZoom: 19,
                attribution: tileConfig.attribution
            });
        
        tileLayer.addTo(map);

        this.maps[mapId] = map;
        this.tileLayers[mapId] = { current: tileLayer, theme: theme, offlineEnabled: enableOffline };
        return true;
    },

    // Switch tile layer theme (dark/light/satellite)
    setTileTheme: function(mapId, theme) {
        console.log('[leaflet-map.js] setTileTheme called:', mapId, theme);
        const map = this.maps[mapId];
        if (!map) {
            console.log('[leaflet-map.js] Map not found:', mapId);
            return false;
        }
        if (!this.tileOptions[theme]) {
            console.log('[leaflet-map.js] Invalid theme:', theme, 'Available:', Object.keys(this.tileOptions));
            return false;
        }

        // Remove current tile layer
        if (this.tileLayers[mapId]?.current) {
            console.log('[leaflet-map.js] Removing current tile layer');
            map.removeLayer(this.tileLayers[mapId].current);
        }

        // Add new tile layer
        const tileConfig = this.tileOptions[theme];
        console.log('[leaflet-map.js] Adding tile layer:', tileConfig.name);
        
        const offlineEnabled = this.tileLayers[mapId]?.offlineEnabled !== false;
        const newLayer = offlineEnabled && window.tileCache
            ? this.createOfflineTileLayer(tileConfig.url, theme, tileConfig.attribution)
            : L.tileLayer(tileConfig.url, {
                maxZoom: 19,
                attribution: tileConfig.attribution
            });
        
        newLayer.addTo(map);

        this.tileLayers[mapId] = { current: newLayer, theme: theme, offlineEnabled: offlineEnabled };
        console.log('[leaflet-map.js] Tile theme switched to:', theme);
        return true;
    },

    // Add GeoJSON MPA layer
    addMpaLayer: function (mapId, geojsonData, dotNetHelper) {
        console.log('[leaflet-map.js] addMpaLayer called with mapId:', mapId);
        console.log('[leaflet-map.js] geojsonData type:', typeof geojsonData);
        console.log('[leaflet-map.js] geojsonData:', JSON.stringify(geojsonData).substring(0, 500) + '...');

        const map = this.maps[mapId];
        if (!map) {
            console.error('[leaflet-map.js] Map not found for mapId:', mapId);
            console.error('[leaflet-map.js] Available mapIds:', Object.keys(this.maps));
            return false;
        }
        console.log('[leaflet-map.js] Map found, container:', map.getContainer()?.id);

        // Remove existing MPA layer
        if (this.mpaLayers[mapId]) {
            console.log('[leaflet-map.js] Removing existing MPA layer');
            map.removeLayer(this.mpaLayers[mapId]);
        }

        // Validate GeoJSON structure
        if (!geojsonData || !geojsonData.type) {
            console.error('[leaflet-map.js] Invalid GeoJSON: missing type');
            return false;
        }
        if (geojsonData.type !== 'FeatureCollection') {
            console.error('[leaflet-map.js] Invalid GeoJSON: type is not FeatureCollection, got:', geojsonData.type);
            return false;
        }
        if (!geojsonData.features || !Array.isArray(geojsonData.features)) {
            console.error('[leaflet-map.js] Invalid GeoJSON: missing features array');
            return false;
        }
        console.log('[leaflet-map.js] GeoJSON has', geojsonData.features.length, 'features');

        // Log first feature for debugging
        if (geojsonData.features.length > 0) {
            const firstFeature = geojsonData.features[0];
            console.log('[leaflet-map.js] First feature id:', firstFeature.id);
            console.log('[leaflet-map.js] First feature properties:', JSON.stringify(firstFeature.properties));
            console.log('[leaflet-map.js] First feature geometry type:', firstFeature.geometry?.type);
        }

        const getColor = (protectionLevel) => {
            switch (protectionLevel) {
                case 'NoTake': return '#dc3545';
                case 'HighlyProtected': return '#fd7e14';
                case 'LightlyProtected': return '#0dcaf0';
                default: return '#6c757d';
            }
        };

        // Enhanced styling with different border patterns
        const style = (feature) => {
            const level = feature.properties.ProtectionLevel;
            const color = getColor(level);
            const isNoTake = level === 'NoTake';
            const isActive = feature.properties.Status !== 'Inactive';

            return {
                fillColor: color,
                weight: isNoTake ? 3 : 2,
                opacity: 1,
                color: color,
                fillOpacity: isNoTake ? 0.5 : 0.35,
                dashArray: isActive ? null : '8, 4',  // Dashed for inactive
                lineCap: 'round',
                lineJoin: 'round'
            };
        };

        const highlightStyle = {
            weight: 5,
            color: '#ffc107',
            fillOpacity: 0.65,
            dashArray: null
        };

        // Create pulsing effect for NoTake zones
        const createPulseLayer = (geojsonData) => {
            const noTakeFeatures = {
                type: 'FeatureCollection',
                features: geojsonData.features.filter(f => f.properties.ProtectionLevel === 'NoTake')
            };

            if (noTakeFeatures.features.length === 0) return null;

            return L.geoJSON(noTakeFeatures, {
                style: {
                    fillColor: 'transparent',
                    fillOpacity: 0,
                    weight: 4,
                    color: '#dc3545',
                    opacity: 0.8,
                    className: 'mpa-pulse-border'  // CSS animation class
                },
                interactive: false  // Don't interfere with main layer events
            });
        };

        try {
        const layer = L.geoJSON(geojsonData, {
            style: style,
            onEachFeature: (feature, layer) => {
                // Popup with MPA info
                const props = feature.properties;
                layer.bindPopup(`
                    <strong>${props.Name}</strong><br/>
                    <small>${props.IslandGroup}</small><br/>
                    <span class="badge" style="background-color: ${getColor(props.ProtectionLevel)}; color: white;">
                        ${props.ProtectionLevel}
                    </span><br/>
                    <small>Area: ${props.AreaSquareKm.toFixed(1)} km²</small>
                `);

                // Add permanent tooltip label for MPA name (visible on dark tiles)
                layer.bindTooltip(props.Name, {
                    permanent: true,
                    direction: 'center',
                    className: 'mpa-label',
                    opacity: 0.9
                });

                layer.on({
                    mouseover: (e) => {
                        e.target.setStyle(highlightStyle);
                        e.target.bringToFront();
                        // Show hover info box
                        this.showHoverInfo(mapId, props);
                    },
                    mouseout: (e) => {
                        this.mpaLayers[mapId].resetStyle(e.target);
                        // Hide hover info box
                        this.hideHoverInfo(mapId);
                    },
                    click: (e) => {
                        map.fitBounds(e.target.getBounds(), { padding: [50, 50] });
                        if (dotNetHelper) {
                            dotNetHelper.invokeMethodAsync('OnMpaClicked', feature.id);
                        }
                    }
                });
            }
        }).addTo(map);

        this.mpaLayers[mapId] = layer;

        // Add pulsing border layer for NoTake zones
        if (this.mpaPulseLayers[mapId]) {
            map.removeLayer(this.mpaPulseLayers[mapId]);
        }
        const pulseLayer = createPulseLayer(geojsonData);
        if (pulseLayer) {
            pulseLayer.addTo(map);
            this.mpaPulseLayers[mapId] = pulseLayer;
            console.log('[leaflet-map.js] Added pulse layer for NoTake zones');
        }

        // Fit map to show all MPAs
        if (layer.getBounds().isValid()) {
            map.fitBounds(layer.getBounds(), { padding: [50, 50] });
            console.log('[leaflet-map.js] Map fitted to MPA bounds');
        } else {
            console.warn('[leaflet-map.js] Layer bounds are not valid');
        }

        console.log('[leaflet-map.js] MPA layer added successfully');
        return true;
        } catch (error) {
            console.error('[leaflet-map.js] Error adding MPA layer:', error);
            console.error('[leaflet-map.js] Error message:', error.message);
            console.error('[leaflet-map.js] Error stack:', error.stack);
            // Try to log what feature caused the issue
            if (geojsonData.features && geojsonData.features.length > 0) {
                console.error('[leaflet-map.js] First feature geometry:', JSON.stringify(geojsonData.features[0].geometry));
            }
            return false;
        }
    },

    // Add fishing events layer with trajectory lines and enhanced markers
    addFishingEventsLayer: function (mapId, fishingEvents, dotNetHelper) {
        const map = this.maps[mapId];
        if (!map) return false;

        // Remove existing layers
        if (this.fishingLayers[mapId]) {
            map.removeLayer(this.fishingLayers[mapId]);
        }
        if (this.trajectoryLayers[mapId]) {
            map.removeLayer(this.trajectoryLayers[mapId]);
        }

        const markers = L.layerGroup();
        const trajectories = L.layerGroup();

        // Track used coordinates to offset overlapping markers
        const usedCoords = {};
        const getOffsetCoords = (lat, lon) => {
            const key = `${lat.toFixed(3)},${lon.toFixed(3)}`;
            if (!usedCoords[key]) {
                usedCoords[key] = 0;
            }
            const count = usedCoords[key]++;
            if (count === 0) return { lat, lon };
            // Spiral offset pattern for overlapping markers (~500m offsets)
            const angle = count * 2.4; // Golden angle in radians
            const radius = 0.005 * Math.sqrt(count); // ~500m per step
            return {
                lat: lat + radius * Math.sin(angle),
                lon: lon + radius * Math.cos(angle)
            };
        };

        // Group events by vessel for trajectory lines
        const vesselEvents = {};
        fishingEvents.forEach(evt => {
            const vesselKey = evt.vesselId || 'unknown';
            if (!vesselEvents[vesselKey]) {
                vesselEvents[vesselKey] = [];
            }
            vesselEvents[vesselKey].push(evt);
        });

        // Create trajectory lines for each vessel
        Object.entries(vesselEvents).forEach(([vesselId, events]) => {
            if (events.length > 1) {
                // Sort by time
                events.sort((a, b) => new Date(a.startTime) - new Date(b.startTime));

                // Create polyline coordinates
                const coords = events.map(e => [e.latitude, e.longitude]);

                // Gradient line segments based on time
                for (let i = 0; i < coords.length - 1; i++) {
                    const daysAgo = (Date.now() - new Date(events[i].startTime).getTime()) / (1000 * 60 * 60 * 24);
                    let lineColor;
                    if (daysAgo < 7) lineColor = 'rgba(220, 53, 69, 0.6)';
                    else if (daysAgo < 14) lineColor = 'rgba(253, 126, 20, 0.5)';
                    else if (daysAgo < 30) lineColor = 'rgba(255, 193, 7, 0.4)';
                    else lineColor = 'rgba(108, 117, 125, 0.3)';

                    const segment = L.polyline([coords[i], coords[i + 1]], {
                        color: lineColor,
                        weight: 2,
                        opacity: 0.8,
                        dashArray: '6, 4',
                        className: 'fishing-trajectory'
                    });

                    segment.bindTooltip(`Vessel: ${events[i].vesselName || vesselId}`, {
                        permanent: false,
                        direction: 'center'
                    });

                    trajectories.addLayer(segment);
                }
            }
        });

        // Add trajectory layer first (under markers)
        trajectories.addTo(map);
        this.trajectoryLayers[mapId] = trajectories;

        // Create markers for each event
        fishingEvents.forEach(evt => {
            const daysAgo = (Date.now() - new Date(evt.startTime).getTime()) / (1000 * 60 * 60 * 24);
            let color;
            if (daysAgo < 7) color = '#dc3545';
            else if (daysAgo < 14) color = '#fd7e14';
            else if (daysAgo < 30) color = '#ffc107';
            else color = '#6c757d';

            const isViolation = evt.isInMpa === true;
            const radius = isViolation ? 9 : 6;
            const borderColor = isViolation ? '#ff0000' : '#ffffff';
            const borderWeight = isViolation ? 3 : 2;

            // Get offset coordinates for overlapping markers
            const coords = getOffsetCoords(evt.latitude, evt.longitude);

            // Use div icon for violation markers to enable CSS animation
            let marker;
            if (isViolation) {
                const violationIcon = L.divIcon({
                    className: 'fishing-violation-marker',
                    html: `<div class="violation-pulse" style="background-color: ${color};"></div>`,
                    iconSize: [18, 18],
                    iconAnchor: [9, 9]
                });
                marker = L.marker([coords.lat, coords.lon], { icon: violationIcon });
            } else {
                marker = L.circleMarker([coords.lat, coords.lon], {
                    radius: radius,
                    fillColor: color,
                    color: borderColor,
                    weight: borderWeight,
                    opacity: 1,
                    fillOpacity: 0.85
                });
            }

            // Enhanced popup with more info
            let popupContent = `
                <div class="fishing-popup">
                    <div class="popup-header">
                        <strong>Fishing Event</strong>
                        ${isViolation ? '<span class="badge bg-danger ms-2">MPA Violation</span>' : ''}
                    </div>
                    <hr style="margin: 6px 0;"/>
                    <div class="popup-body">
                        <div><strong>Vessel:</strong> ${evt.vesselName || evt.vesselId}</div>
                        <div><strong>Date:</strong> ${new Date(evt.startTime).toLocaleDateString()}</div>
                        <div><strong>Time:</strong> ${new Date(evt.startTime).toLocaleTimeString()}</div>
            `;
            if (evt.durationHours) {
                popupContent += `<div><strong>Duration:</strong> ${evt.durationHours.toFixed(1)} hours</div>`;
            }
            if (evt.distanceKm) {
                popupContent += `<div><strong>Distance:</strong> ${evt.distanceKm.toFixed(1)} km</div>`;
            }
            if (evt.eventType) {
                popupContent += `<div><strong>Type:</strong> ${evt.eventType}</div>`;
            }
            if (isViolation && evt.mpaName) {
                popupContent += `<div class="text-danger"><strong>Inside MPA:</strong> ${evt.mpaName}</div>`;
            }
            popupContent += '</div></div>';

            marker.bindPopup(popupContent);

            marker.on('click', () => {
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnFishingEventClicked', evt.eventId);
                }
            });

            markers.addLayer(marker);
        });

        markers.addTo(map);
        this.fishingLayers[mapId] = markers;

        // Debug: analyze coordinate distribution
        const uniqueCoords = new Set(fishingEvents.map(e => `${e.latitude?.toFixed(4)},${e.longitude?.toFixed(4)}`));
        console.log(`[leaflet-map.js] Added ${fishingEvents.length} fishing markers and ${Object.keys(vesselEvents).length} vessel trajectories`);
        console.log(`[leaflet-map.js] Unique coordinates (4 decimal precision): ${uniqueCoords.size}`);

        // Log coordinate bounds
        const lats = fishingEvents.map(e => e.latitude).filter(l => l != null);
        const lons = fishingEvents.map(e => e.longitude).filter(l => l != null);
        if (lats.length > 0 && lons.length > 0) {
            console.log(`[leaflet-map.js] Coordinate bounds: lat [${Math.min(...lats).toFixed(4)}, ${Math.max(...lats).toFixed(4)}], lon [${Math.min(...lons).toFixed(4)}, ${Math.max(...lons).toFixed(4)}]`);
        }

        // Check for invalid coordinates
        const invalidEvents = fishingEvents.filter(e => e.latitude == null || e.longitude == null || isNaN(e.latitude) || isNaN(e.longitude));
        if (invalidEvents.length > 0) {
            console.warn(`[leaflet-map.js] ${invalidEvents.length} events have invalid coordinates`);
        }

        return true;
    },

    // Remove fishing events layer and trajectories
    removeFishingEventsLayer: function (mapId) {
        const map = this.maps[mapId];
        if (!map) return false;

        if (this.fishingLayers[mapId]) {
            map.removeLayer(this.fishingLayers[mapId]);
            delete this.fishingLayers[mapId];
        }

        if (this.trajectoryLayers[mapId]) {
            map.removeLayer(this.trajectoryLayers[mapId]);
            delete this.trajectoryLayers[mapId];
        }

        return true;
    },

    // Zoom to specific MPA by ID
    zoomToMpa: function (mapId, mpaId) {
        const map = this.maps[mapId];
        const mpaLayer = this.mpaLayers[mapId];
        if (!map || !mpaLayer) return false;

        let found = false;
        mpaLayer.eachLayer((layer) => {
            if (layer.feature && layer.feature.id === mpaId) {
                map.fitBounds(layer.getBounds(), { padding: [50, 50] });
                layer.openPopup();
                found = true;
            }
        });

        return found;
    },

    // Highlight specific MPA
    highlightMpa: function (mapId, mpaId) {
        const mpaLayer = this.mpaLayers[mapId];
        if (!mpaLayer) return false;

        mpaLayer.eachLayer((layer) => {
            if (layer.feature && layer.feature.id === mpaId) {
                layer.setStyle({
                    weight: 4,
                    color: '#ffc107',
                    fillOpacity: 0.6
                });
                layer.bringToFront();
            } else if (layer.feature) {
                mpaLayer.resetStyle(layer);
            }
        });

        return true;
    },

    // US-2.2.6: Add interactive map legend with WCAG patterns
    addLegend: function(mapId, showMpa = true, showFishing = false, showAlerts = false) {
        const map = this.maps[mapId];
        if (!map) return false;

        // Remove existing legend
        if (this.legendControls[mapId]) {
            map.removeControl(this.legendControls[mapId]);
        }

        const legend = L.control({ position: 'bottomright' });

        legend.onAdd = function() {
            const div = L.DomUtil.create('div', 'map-legend');
            div.setAttribute('role', 'region');
            div.setAttribute('aria-label', 'Map Legend');
            div.setAttribute('tabindex', '0');

            let html = '<div class="legend-header"><strong>Legend</strong></div>';

            if (showMpa) {
                html += `
                    <div class="legend-section" role="list" aria-label="Protection Levels">
                        <div class="legend-title" id="protection-levels-title">Protection Levels</div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-color legend-pattern-diagonal" style="background: #dc3545;" aria-hidden="true"></span>
                            <span class="legend-label">No-Take Zone</span>
                            <span class="legend-icon" aria-hidden="true">🚫</span>
                        </div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-color legend-pattern-dots" style="background: #fd7e14;" aria-hidden="true"></span>
                            <span class="legend-label">Highly Protected</span>
                            <span class="legend-icon" aria-hidden="true">🛡️</span>
                        </div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-color legend-pattern-waves" style="background: #0dcaf0;" aria-hidden="true"></span>
                            <span class="legend-label">Lightly Protected</span>
                            <span class="legend-icon" aria-hidden="true">🌊</span>
                        </div>
                    </div>`;
            }

            if (showFishing) {
                html += `
                    <div class="legend-section" role="list" aria-label="Fishing Activity">
                        <div class="legend-title" id="fishing-activity-title">Fishing Activity</div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-dot legend-dot-recent" style="background: #dc3545;" aria-hidden="true"></span>
                            <span class="legend-label">Last 7 days</span>
                        </div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-dot legend-dot-medium" style="background: #fd7e14;" aria-hidden="true"></span>
                            <span class="legend-label">8-14 days</span>
                        </div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-dot legend-dot-old" style="background: #ffc107;" aria-hidden="true"></span>
                            <span class="legend-label">15-30 days</span>
                        </div>
                        <div class="legend-item legend-item-warning" role="listitem">
                            <span class="legend-dot violation" aria-hidden="true"></span>
                            <span class="legend-label">MPA Violation</span>
                            <span class="legend-icon" aria-hidden="true">⚠️</span>
                        </div>
                    </div>`;
            }

            if (showAlerts) {
                html += `
                    <div class="legend-section" role="list" aria-label="Alert Levels">
                        <div class="legend-title">Alert Levels</div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-pulse legend-pulse-critical" aria-hidden="true"></span>
                            <span class="legend-label">Critical</span>
                        </div>
                        <div class="legend-item" role="listitem">
                            <span class="legend-pulse legend-pulse-warning" aria-hidden="true"></span>
                            <span class="legend-label">Warning</span>
                        </div>
                    </div>`;
            }

            div.innerHTML = html;

            // Prevent map interactions when clicking legend
            L.DomEvent.disableClickPropagation(div);
            L.DomEvent.disableScrollPropagation(div);

            return div;
        };

        legend.addTo(map);
        this.legendControls[mapId] = legend;
        return true;
    },

    // Update legend visibility based on active layers
    updateLegend: function(mapId, showMpa, showFishing) {
        return this.addLegend(mapId, showMpa, showFishing);
    },

    // Create hover info control for MPA details
    createHoverInfoControl: function(mapId) {
        const map = this.maps[mapId];
        if (!map || this.hoverInfoControls[mapId]) return;

        const info = L.control({ position: 'topright' });

        info.onAdd = function() {
            const div = L.DomUtil.create('div', 'mpa-hover-info');
            div.setAttribute('role', 'status');
            div.setAttribute('aria-live', 'polite');
            div.setAttribute('aria-label', 'Marine Protected Area Information');
            div.style.display = 'none';
            return div;
        };

        info.addTo(map);
        this.hoverInfoControls[mapId] = info;
    },

    // Show hover info box with MPA details
    showHoverInfo: function(mapId, props) {
        if (!this.hoverInfoControls[mapId]) {
            this.createHoverInfoControl(mapId);
        }

        const container = this.hoverInfoControls[mapId]?.getContainer();
        if (!container) return;

        const getColorClass = (level) => {
            switch (level) {
                case 'NoTake': return 'protection-no-take';
                case 'HighlyProtected': return 'protection-highly';
                case 'LightlyProtected': return 'protection-lightly';
                default: return 'protection-unknown';
            }
        };

        const getIcon = (level) => {
            switch (level) {
                case 'NoTake': return '🚫';
                case 'HighlyProtected': return '🛡️';
                case 'LightlyProtected': return '🌊';
                default: return '📍';
            }
        };

        container.innerHTML = `
            <div class="hover-info-header">
                <span class="hover-info-icon">${getIcon(props.ProtectionLevel)}</span>
                <span class="hover-info-title">${props.Name}</span>
            </div>
            <div class="hover-info-body">
                <div class="hover-info-row">
                    <span class="hover-info-label">Island Group</span>
                    <span class="hover-info-value">${props.IslandGroup}</span>
                </div>
                <div class="hover-info-row">
                    <span class="hover-info-label">Protection</span>
                    <span class="hover-info-value ${getColorClass(props.ProtectionLevel)}">${props.ProtectionLevel.replace(/([A-Z])/g, ' $1').trim()}</span>
                </div>
                <div class="hover-info-row">
                    <span class="hover-info-label">Area</span>
                    <span class="hover-info-value">${props.AreaSquareKm.toFixed(1)} km²</span>
                </div>
            </div>
            <div class="hover-info-footer">
                <small>Click for more details</small>
            </div>
        `;
        container.style.display = 'block';
    },

    // Hide hover info box
    hideHoverInfo: function(mapId) {
        const container = this.hoverInfoControls[mapId]?.getContainer();
        if (container) {
            container.style.display = 'none';
        }
    },

    // Dispose map and all layers
    dispose: function (mapId) {
        if (this.maps[mapId]) {
            this.maps[mapId].remove();
            delete this.maps[mapId];
            delete this.mpaLayers[mapId];
            delete this.mpaPulseLayers[mapId];
            delete this.fishingLayers[mapId];
            delete this.trajectoryLayers[mapId];
            delete this.tileLayers[mapId];
            delete this.legendControls[mapId];
            delete this.hoverInfoControls[mapId];
        }
    },

    // Offline tile caching methods

    // Download tiles for current map view
    downloadCurrentView: async function(mapId, minZoom, maxZoom, dotNetHelper) {
        const map = this.maps[mapId];
        if (!map) {
            console.error('[leaflet-map] Map not found:', mapId);
            return null;
        }

        if (!window.tileCache) {
            console.error('[leaflet-map] Tile cache not available');
            return null;
        }

        const bounds = map.getBounds();
        const theme = this.tileLayers[mapId]?.theme || 'dark';
        const tileConfig = this.tileOptions[theme];

        const boundsObj = {
            north: bounds.getNorth(),
            south: bounds.getSouth(),
            east: bounds.getEast(),
            west: bounds.getWest()
        };

        console.log(`[leaflet-map] Downloading tiles for view, zoom ${minZoom}-${maxZoom}`);

        try {
            // Use the stored abort signal from the controller
            const abortSignal = this.getAbortSignal();
            
            const result = await window.tileCache.downloadRegion(
                theme,
                boundsObj,
                minZoom,
                maxZoom,
                tileConfig.url,
                (progress) => {
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnDownloadProgress', progress);
                    }
                },
                abortSignal
            );

            console.log('[leaflet-map] Download complete:', result);
            return result;
        } catch (error) {
            console.error('[leaflet-map] Download failed:', error);
            throw error;
        }
    },

    // Download tiles for a custom region
    downloadRegion: async function(bounds, minZoom, maxZoom, theme, dotNetHelper) {
        if (!window.tileCache) {
            console.error('[leaflet-map] Tile cache not available');
            return null;
        }

        const tileConfig = this.tileOptions[theme];
        if (!tileConfig) {
            console.error('[leaflet-map] Invalid theme:', theme);
            return null;
        }

        console.log(`[leaflet-map] Downloading region tiles, zoom ${minZoom}-${maxZoom}`);

        try {
            // Use the stored abort signal from the controller
            const abortSignal = this.getAbortSignal();
            
            const result = await window.tileCache.downloadRegion(
                theme,
                bounds,
                minZoom,
                maxZoom,
                tileConfig.url,
                (progress) => {
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnDownloadProgress', progress);
                    }
                },
                abortSignal
            );

            console.log('[leaflet-map] Region download complete:', result);
            return result;
        } catch (error) {
            console.error('[leaflet-map] Region download failed:', error);
            throw error;
        }
    },

    // Estimate storage size for current view
    estimateCurrentViewSize: function(mapId, minZoom, maxZoom) {
        const map = this.maps[mapId];
        if (!map || !window.tileCache) {
            return null;
        }

        const bounds = map.getBounds();
        const boundsObj = {
            north: bounds.getNorth(),
            south: bounds.getSouth(),
            east: bounds.getEast(),
            west: bounds.getWest()
        };

        return window.tileCache.estimateRegionSize(boundsObj, minZoom, maxZoom);
    },

    // Estimate storage size for a custom region
    estimateRegionSize: function(bounds, minZoom, maxZoom) {
        if (!window.tileCache) {
            return null;
        }

        return window.tileCache.estimateRegionSize(bounds, minZoom, maxZoom);
    },

    // Get cache statistics
    getCacheStats: async function() {
        if (!window.tileCache) {
            return null;
        }

        return await window.tileCache.getStats();
    },

    // Clear all cached tiles
    clearAllCache: async function() {
        if (!window.tileCache) {
            return false;
        }

        await window.tileCache.clearAll();
        return true;
    },

    // Clear cache by theme
    clearCacheByTheme: async function(theme) {
        if (!window.tileCache) {
            return 0;
        }

        return await window.tileCache.clearByTheme(theme);
    },

    // Clear old tiles
    clearOldTiles: async function(maxAgeDays) {
        if (!window.tileCache) {
            return 0;
        }

        const maxAgeMs = maxAgeDays * 24 * 60 * 60 * 1000;
        return await window.tileCache.clearOldTiles(maxAgeMs);
    },

    // Get cached regions
    getCachedRegions: async function() {
        if (!window.tileCache) {
            return [];
        }

        return await window.tileCache.getCachedRegions();
    },

    // Check online status
    isOnline: function() {
        return navigator.onLine;
    },

    // Add offline indicator to map
    addOfflineIndicator: function(mapId) {
        const map = this.maps[mapId];
        if (!map) {
            return false;
        }

        // Create a custom control for offline indicator
        const OfflineControl = L.Control.extend({
            options: {
                position: 'topright'
            },

            onAdd: function() {
                const div = L.DomUtil.create('div', 'leaflet-control-offline');
                div.innerHTML = `
                    <div class="offline-indicator" style="
                        background: rgba(220, 53, 69, 0.9);
                        color: white;
                        padding: 8px 12px;
                        border-radius: 4px;
                        font-size: 14px;
                        font-weight: 500;
                        box-shadow: 0 2px 4px rgba(0,0,0,0.2);
                        display: none;
                    ">
                        <span style="margin-right: 6px;">⚠️</span>
                        <span>Offline Mode</span>
                    </div>
                `;

                // Update visibility based on online status
                const updateStatus = () => {
                    const indicator = div.querySelector('.offline-indicator');
                    if (indicator) {
                        indicator.style.display = navigator.onLine ? 'none' : 'flex';
                    }
                };

                updateStatus();
                window.addEventListener('online', updateStatus);
                window.addEventListener('offline', updateStatus);

                L.DomEvent.disableClickPropagation(div);
                return div;
            }
        });

        const offlineControl = new OfflineControl();
        offlineControl.addTo(map);
        
        return true;
    },

    // Abort controller management for download cancellation
    _downloadAbortController: null,

    createAbortController: function() {
        // Clean up any existing controller first
        if (this._downloadAbortController) {
            this.cleanupAbortController();
        }
        this._downloadAbortController = new AbortController();
        console.log('[leaflet-map] AbortController created');
    },

    getAbortSignal: function() {
        return this._downloadAbortController ? this._downloadAbortController.signal : null;
    },

    cancelDownload: function() {
        if (this._downloadAbortController) {
            console.log('[leaflet-map] Aborting download');
            this._downloadAbortController.abort();
        }
    },

    cleanupAbortController: function() {
        this._downloadAbortController = null;
        console.log('[leaflet-map] AbortController cleaned up');
    }
};
