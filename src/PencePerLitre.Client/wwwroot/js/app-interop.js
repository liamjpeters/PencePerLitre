// Pence Per Litre - JavaScript Interop Module
// Handles Leaflet Maps and HTML5 Geolocation

let leafletMap = null;
let mapMarkersLayer = null;
let mapTileLayer = null;
let currentDotNetRef = null;

function applyMapTheme() {
    if (!leafletMap || typeof window.L === "undefined") return;

    if (mapTileLayer) {
        mapTileLayer.remove();
    }

    const cartoApiKey = globalThis.pencePerLitreConfig?.cartoApiKey;
    const tileStyle = document.documentElement.classList.contains('dark') ? 'dark_all' : 'voyager';
    const cartoTileUrl = `https://{s}.basemaps.cartocdn.com/rastertiles/${tileStyle}/{z}/{x}/{y}{r}.png`
        + (cartoApiKey ? `?key=${encodeURIComponent(cartoApiKey)}` : '');

    mapTileLayer = L.tileLayer(cartoTileUrl, {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
        subdomains: 'abcd',
        maxZoom: 19
    }).addTo(leafletMap);
    mapTileLayer.bringToBack();
}

window.addEventListener('ppl-theme-change', applyMapTheme);

export function getCurrentLocation() {
    return new Promise((resolve) => {
        if (!navigator.geolocation) {
            resolve({ success: false, error: "Geolocation is not supported by your browser." });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (pos) => {
                resolve({
                    success: true,
                    lat: pos.coords.latitude,
                    lon: pos.coords.longitude,
                    accuracy: pos.coords.accuracy
                });
            },
            (err) => {
                let msg = "Could not get your location.";
                if (err.code === 1) msg = "Location permission denied.";
                else if (err.code === 2) msg = "Location position unavailable.";
                else if (err.code === 3) msg = "Location request timed out.";
                resolve({ success: false, error: msg });
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 60000
            }
        );
    });
}

export function initMap(elementId, dotNetHelper, initialLat = 53.8008, initialLon = -1.5491, initialZoom = 12) {
    currentDotNetRef = dotNetHelper;

    if (typeof window.L === "undefined") {
        console.error("Leaflet library (window.L) is not loaded.");
        return;
    }

    const container = document.getElementById(elementId);
    if (!container) {
        console.warn(`Map container #${elementId} not found in DOM.`);
        return;
    }

    if (leafletMap) {
        leafletMap.remove();
        leafletMap = null;
    }

    // Configure local default icon paths
    L.Icon.Default.imagePath = 'lib/leaflet/images/';

    try {
        leafletMap = L.map(elementId, {
            zoomControl: true,
            attributionControl: true
        }).setView([initialLat, initialLon], initialZoom);

        if (!globalThis.pencePerLitreConfig?.cartoApiKey) {
            console.warn("CARTO API key is not configured; map tiles may display a watermark.");
        }

        applyMapTheme();

        mapMarkersLayer = L.layerGroup().addTo(leafletMap);

        // Force Leaflet to recalculate container dimensions
        setTimeout(() => { if (leafletMap) leafletMap.invalidateSize(); }, 50);
        setTimeout(() => { if (leafletMap) leafletMap.invalidateSize(); }, 250);
        setTimeout(() => { if (leafletMap) leafletMap.invalidateSize(); }, 750);
    } catch (err) {
        console.error("Error initializing Leaflet map:", err);
    }
}

export function invalidateMapSize() {
    if (leafletMap) {
        leafletMap.invalidateSize({ animate: false });
    }
}

export function setMapView(lat, lon, zoom) {
    if (!leafletMap) return;
    leafletMap.setView([lat, lon], zoom || leafletMap.getZoom(), { animate: true });
    setTimeout(() => { if (leafletMap) leafletMap.invalidateSize(); }, 100);
}

export function fitMapBounds(points) {
    if (!leafletMap || !points || points.length === 0) return;
    const bounds = L.latLngBounds(points.map(p => [p.lat, p.lon]));
    leafletMap.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
    setTimeout(() => { if (leafletMap) leafletMap.invalidateSize(); }, 100);
}

export function updateMapMarkers(stations, userLocation) {
    if (!leafletMap || !mapMarkersLayer) return;

    mapMarkersLayer.clearLayers();

    // 1. User location marker
    if (userLocation && userLocation.lat && userLocation.lon) {
        const userIcon = L.divIcon({
            className: 'user-location-marker',
            html: `<div class="relative flex items-center justify-center">
                    <span class="animate-ping absolute inline-flex h-6 w-6 rounded-full bg-neutral-400 opacity-75"></span>
                    <span class="relative inline-flex rounded-full h-4 w-4 bg-neutral-950 dark:bg-neutral-100 border-2 border-white dark:border-neutral-950 shadow-md"></span>
                   </div>`,
            iconSize: [24, 24],
            iconAnchor: [12, 12]
        });

        L.marker([userLocation.lat, userLocation.lon], { icon: userIcon, zIndexOffset: 1000 })
            .bindPopup("<strong>Your Location</strong>")
            .addTo(mapMarkersLayer);
    }

    // 2. Forecourt markers
    if (!stations) return;

    const validPrices = stations.filter(s => s.selectedFuelPrice != null).map(s => s.selectedFuelPrice);
    const minPrice = validPrices.length > 0 ? Math.min(...validPrices) : null;

    stations.forEach(item => {
        const s = item.station;
        const price = item.selectedFuelPrice;
        const isCheapest = minPrice != null && price === minPrice;

        const priceText = price != null ? `${price.toFixed(1)}p` : 'N/A';
        const badgeBg = isCheapest
            ? 'bg-neutral-950 dark:bg-neutral-100 text-white dark:text-neutral-950 font-bold ring-2 ring-neutral-400'
            : 'bg-neutral-800 dark:bg-neutral-200 text-white dark:text-neutral-950 font-semibold';

        const markerHtml = `
            <div class="cursor-pointer transition-transform transform hover:scale-110 shadow-lg rounded-full px-2.5 py-1 text-xs flex items-center gap-1 border border-white ${badgeBg}">
                <span>${priceText}</span>
            </div>
        `;

        const icon = L.divIcon({
            className: 'price-map-marker',
            html: markerHtml,
            iconSize: [60, 26],
            iconAnchor: [30, 13]
        });

        const marker = L.marker([s.lat, s.lon], { icon: icon });

        marker.on('click', () => {
            if (currentDotNetRef) {
                currentDotNetRef.invokeMethodAsync('OnStationMarkerClicked', s.id);
            }
        });

        marker.addTo(mapMarkersLayer);
    });

    leafletMap.invalidateSize();
}
