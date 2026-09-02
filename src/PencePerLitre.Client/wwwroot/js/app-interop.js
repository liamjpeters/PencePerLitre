// Pence Per Litre - JavaScript Interop Module
// Handles MapLibre maps and HTML5 geolocation

import * as maplibregl from '../lib/maplibre/maplibre-gl.mjs';

let map = null;
let mapMarkers = [];
let currentDotNetRef = null;

const mapAttribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>';

function getMapStyleUrl() {
    const cartoApiKey = globalThis.pencePerLitreConfig?.cartoApiKey;
    const tileStyle = document.documentElement.classList.contains('dark')
        ? 'dark-matter-gl-style'
        : 'voyager-gl-style';
    const baseUrl = `https://basemaps.cartocdn.com/gl/${tileStyle}/style.json`;
    return cartoApiKey ? `${baseUrl}?key=${encodeURIComponent(cartoApiKey)}` : baseUrl;
}

function applyMapTheme() {
    if (!map) return;
    map.setStyle(getMapStyleUrl());
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

    const container = document.getElementById(elementId);
    if (!container) {
        console.warn(`Map container #${elementId} not found in DOM.`);
        return;
    }

    if (map) {
        map.remove();
        map = null;
        mapMarkers = [];
    }

    if (!globalThis.pencePerLitreConfig?.cartoApiKey) {
        console.warn("CARTO API key is not configured; vector map requests may be limited.");
    }

    try {
        map = new maplibregl.Map({
            container: elementId,
            style: getMapStyleUrl(),
            center: [initialLon, initialLat],
            zoom: initialZoom,
            attributionControl: false
        });
        map.addControl(new maplibregl.NavigationControl(), 'top-right');
        map.addControl(new maplibregl.AttributionControl({
            compact: true,
            customAttribution: mapAttribution
        }), 'bottom-right');

        setTimeout(() => { if (map) map.resize(); }, 50);
        setTimeout(() => { if (map) map.resize(); }, 250);
        setTimeout(() => { if (map) map.resize(); }, 750);
    } catch (err) {
        console.error("Error initializing MapLibre map:", err);
    }
}

export function invalidateMapSize() {
    if (map) {
        map.resize();
    }
}

export function setMapView(lat, lon, zoom) {
    if (!map) return;
    map.easeTo({
        center: [lon, lat],
        zoom: zoom || map.getZoom(),
        duration: 500
    });
    setTimeout(() => { if (map) map.resize(); }, 100);
}

export function fitMapBounds(points) {
    if (!map || !points || points.length === 0) return;

    const bounds = new maplibregl.LngLatBounds();
    points.forEach(point => bounds.extend([point.lon, point.lat]));
    map.fitBounds(bounds, { padding: 40, maxZoom: 14 });
    setTimeout(() => { if (map) map.resize(); }, 100);
}

function removeMarkers() {
    mapMarkers.forEach(marker => marker.remove());
    mapMarkers = [];
}

function addMarker(element, lon, lat) {
    const marker = new maplibregl.Marker({ element, anchor: 'center' })
        .setLngLat([lon, lat])
        .addTo(map);
    mapMarkers.push(marker);
}

export function updateMapMarkers(stations, userLocation) {
    if (!map) return;

    removeMarkers();

    if (userLocation && userLocation.lat && userLocation.lon) {
        const userElement = document.createElement('div');
        userElement.className = 'user-location-marker';
        userElement.innerHTML = `<div class="relative flex items-center justify-center">
                <span class="animate-ping absolute inline-flex h-6 w-6 rounded-full bg-neutral-400 opacity-75"></span>
                <span class="relative inline-flex rounded-full h-4 w-4 bg-neutral-950 dark:bg-neutral-100 border-2 border-white dark:border-neutral-950 shadow-md"></span>
               </div>`;
        addMarker(userElement, userLocation.lon, userLocation.lat);
    }

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

        const markerElement = document.createElement('div');
        markerElement.className = 'price-map-marker';
        markerElement.innerHTML = `
            <div class="cursor-pointer transition-transform transform hover:scale-110 shadow-lg rounded-full px-2.5 py-1 text-xs flex items-center gap-1 border border-white ${badgeBg}">
                <span>${priceText}</span>
            </div>
        `;
        markerElement.addEventListener('click', () => {
            if (currentDotNetRef) {
                currentDotNetRef.invokeMethodAsync('OnStationMarkerClicked', s.id);
            }
        });

        addMarker(markerElement, s.lon, s.lat);
    });

    map.resize();
}
