// Leaflet Map Interop for CoralLedger Blue
window.leafletMap = {
    maps: {},
    mpaLayers: {},
    fishingLayers: {},
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

    // Initialize a new map with dark theme support
    initialize: function (mapId, centerLat, centerLng, zoom, useDarkTheme = true) {
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
        const tileLayer = L.tileLayer(tileConfig.url, {
            maxZoom: 19,
            attribution: tileConfig.attribution
        }).addTo(map);

        this.maps[mapId] = map;
        this.tileLayers[mapId] = { current: tileLayer, theme: useDarkTheme ? 'dark' : 'light' };
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
        const newLayer = L.tileLayer(tileConfig.url, {
            maxZoom: 19,
            attribution: tileConfig.attribution
        }).addTo(map);

        this.tileLayers[mapId] = { current: newLayer, theme: theme };
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

        const style = (feature) => ({
            fillColor: getColor(feature.properties.ProtectionLevel),
            weight: 2,
            opacity: 1,
            color: getColor(feature.properties.ProtectionLevel),
            fillOpacity: 0.4
        });

        const highlightStyle = {
            weight: 4,
            color: '#ffc107',
            fillOpacity: 0.6
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

    // Add fishing events layer
    addFishingEventsLayer: function (mapId, fishingEvents, dotNetHelper) {
        const map = this.maps[mapId];
        if (!map) return false;

        // Remove existing fishing layer
        if (this.fishingLayers[mapId]) {
            map.removeLayer(this.fishingLayers[mapId]);
        }

        const markers = L.layerGroup();

        fishingEvents.forEach(evt => {
            const daysAgo = (Date.now() - new Date(evt.startTime).getTime()) / (1000 * 60 * 60 * 24);
            let color;
            if (daysAgo < 7) color = '#dc3545';
            else if (daysAgo < 14) color = '#fd7e14';
            else if (daysAgo < 30) color = '#ffc107';
            else color = '#6c757d';

            const isViolation = evt.isInMpa === true;
            const radius = isViolation ? 8 : 6;
            const borderColor = isViolation ? '#ff0000' : '#ffffff';
            const borderWeight = isViolation ? 3 : 2;

            const marker = L.circleMarker([evt.latitude, evt.longitude], {
                radius: radius,
                fillColor: color,
                color: borderColor,
                weight: borderWeight,
                opacity: 1,
                fillOpacity: 0.8
            });

            let popupContent = `
                <strong><i class="bi bi-water"></i> Fishing Event</strong>
                ${isViolation ? '<span class="badge bg-danger ms-2">MPA Violation</span>' : ''}
                <hr style="margin: 4px 0;"/>
                <small><strong>Vessel:</strong> ${evt.vesselName || evt.vesselId}</small><br/>
                <small><strong>Date:</strong> ${new Date(evt.startTime).toLocaleDateString()}</small>
            `;
            if (evt.durationHours) {
                popupContent += `<br/><small><strong>Duration:</strong> ${evt.durationHours.toFixed(1)} hours</small>`;
            }
            if (isViolation && evt.mpaName) {
                popupContent += `<br/><small class="text-danger"><strong>Inside:</strong> ${evt.mpaName}</small>`;
            }

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

        return true;
    },

    // Remove fishing events layer
    removeFishingEventsLayer: function (mapId) {
        const map = this.maps[mapId];
        if (!map) return false;

        if (this.fishingLayers[mapId]) {
            map.removeLayer(this.fishingLayers[mapId]);
            delete this.fishingLayers[mapId];
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

    // Dispose map
    dispose: function (mapId) {
        if (this.maps[mapId]) {
            this.maps[mapId].remove();
            delete this.maps[mapId];
            delete this.mpaLayers[mapId];
            delete this.fishingLayers[mapId];
            delete this.tileLayers[mapId];
            delete this.legendControls[mapId];
            delete this.hoverInfoControls[mapId];
        }
    }
};
