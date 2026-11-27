import {
  ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, output, signal, viewChild,
} from '@angular/core';
import type * as Leaflet from 'leaflet';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { translate } from '../../../core/i18n/i18n.service';
import { MAP_TILES } from './farm-map.config';

export interface PickedLocation { lat: number; lon: number; }
export type MapLayerKind = 'street' | 'satellite';

/** Where the map opens when there is no point yet: Bangladesh. */
const EMPTY_CENTER: [number, number] = [23.7, 90.4];
const EMPTY_ZOOM = 7;

/** A violet map pin (brand colour) drawn as an SVG divIcon, so no marker image assets are needed. */
const PIN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="42" viewBox="0 0 32 42" aria-hidden="true">
  <path d="M16 1C7.7 1 1 7.6 1 15.8 1 27 16 41 16 41s15-14 15-25.2C31 7.6 24.3 1 16 1z" fill="#6d28d9" stroke="#fff" stroke-width="2"/>
  <circle cx="16" cy="15.5" r="5.5" fill="#fff"/>
</svg>`;

const round6 = (v: number) => Math.round(v * 1e6) / 1e6;
const isValidPoint = (lat: unknown, lon: unknown): boolean =>
  typeof lat === 'number' && typeof lon === 'number' && Number.isFinite(lat) && Number.isFinite(lon)
  && lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;

/**
 * A farm location map (Leaflet, loaded lazily on first use so it never weighs on the main bundle).
 * Street (OpenStreetMap) and Satellite (Esri World Imagery) layers; a violet pin with the farm name.
 * `editable`: a click on the map, dragging the pin or Enter on the focused map (pin at the centre) moves the
 * pin and emits `locationPicked` with coordinates rounded to 6 decimals.
 */
@Component({
  selector: 'app-farm-map',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './farm-map.component.html',
})
export class FarmMapComponent {
  readonly lat = input<number | null | undefined>(null);
  readonly lon = input<number | null | undefined>(null);
  readonly label = input<string>('');
  readonly zoom = input<number>(13);
  readonly editable = input<boolean>(false);
  /** CSS height, e.g. '280px' or 280. */
  readonly height = input<string | number>('280px');

  readonly locationPicked = output<PickedLocation>();

  private readonly mapEl = viewChild.required<ElementRef<HTMLDivElement>>('mapEl');
  private readonly destroyRef = inject(DestroyRef);

  readonly layer = signal<MapLayerKind>('street');
  readonly ready = signal(false);
  readonly failed = signal(false);

  readonly cssHeight = computed(() => { const h = this.height(); return typeof h === 'number' ? `${h}px` : h; });
  readonly hasPoint = computed(() => isValidPoint(this.lat(), this.lon()));
  readonly googleMapsUrl = computed(() => this.hasPoint() ? `https://www.google.com/maps?q=${this.lat()},${this.lon()}` : '');
  readonly ariaLabel = computed(() => {
    const name = this.label();
    const base = name ? translate('Map of {name}', { name }) : translate('Farm location map');
    return this.editable() ? `${base}. ${translate('Click the map or drag the pin to set the location. Press Enter to put the pin at the map centre.')}` : base;
  });

  private L: typeof Leaflet | null = null;
  private map: Leaflet.Map | null = null;
  private marker: Leaflet.Marker | null = null;
  private tiles: Record<MapLayerKind, Leaflet.TileLayer> | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private destroyed = false;
  /** The last point this map emitted, so the echo back through the inputs does not re-centre the view. */
  private lastPicked: PickedLocation | null = null;

  constructor() {
    afterNextRender(() => { void this.init(); });

    // Keep the pin, popup and view in step with the inputs.
    effect(() => {
      const lat = this.lat(), lon = this.lon(), label = this.label(), editable = this.editable();
      if (!this.ready()) return;
      this.sync(lat, lon, label, editable);
    });

    effect(() => {
      const kind = this.layer();
      if (!this.ready() || !this.map || !this.tiles) return;
      const other: MapLayerKind = kind === 'street' ? 'satellite' : 'street';
      if (this.map.hasLayer(this.tiles[other])) this.map.removeLayer(this.tiles[other]);
      if (!this.map.hasLayer(this.tiles[kind])) this.tiles[kind].addTo(this.map);
    });

    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.resizeObserver?.disconnect();
      this.map?.remove();
      this.map = null;
      this.marker = null;
    });
  }

  setLayer(kind: MapLayerKind): void { this.layer.set(kind); }

  private async init(): Promise<void> {
    try {
      // The ES module build, so the bundler can optimise it (the package main is CommonJS).
      const L = await import('leaflet/dist/leaflet-src.esm.js');
      if (this.destroyed) return;
      this.L = L;
      const el = this.mapEl().nativeElement;
      const start = this.hasPoint();
      const map = L.map(el, {
        center: start ? [this.lat()!, this.lon()!] : EMPTY_CENTER,
        zoom: start ? this.zoom() : EMPTY_ZOOM,
        scrollWheelZoom: false, // the page scrolls past the map; zoom with the buttons, pinch or double-click
        keyboard: true,
      });
      this.map = map;
      this.tiles = {
        street: L.tileLayer(MAP_TILES.street.url, { attribution: MAP_TILES.street.attribution, maxZoom: MAP_TILES.street.maxZoom }),
        satellite: L.tileLayer(MAP_TILES.satellite.url, { attribution: MAP_TILES.satellite.attribution, maxZoom: MAP_TILES.satellite.maxZoom }),
      };
      this.tiles[this.layer()].addTo(map);
      map.attributionControl.setPrefix('<a href="https://leafletjs.com" target="_blank" rel="noopener">Leaflet</a>');

      map.on('click', (e: Leaflet.LeafletMouseEvent) => { if (this.editable()) this.pick(e.latlng); });
      el.addEventListener('keydown', (e: KeyboardEvent) => {
        if (e.key === 'Enter' && e.target === el && this.editable()) { e.preventDefault(); this.pick(map.getCenter()); }
      });

      // Recalculate the size once laid out and whenever the container is resized or becomes visible.
      if (typeof ResizeObserver !== 'undefined') {
        this.resizeObserver = new ResizeObserver(() => this.map?.invalidateSize());
        this.resizeObserver.observe(el);
      }
      setTimeout(() => this.map?.invalidateSize(), 0);

      this.ready.set(true);
    } catch (err) {
      console.error('Map failed to load', err);
      this.failed.set(true);
    }
  }

  private pick(latlng: Leaflet.LatLng): void {
    const w = latlng.wrap();
    const point = { lat: round6(w.lat), lon: round6(w.lng) };
    this.lastPicked = point;
    this.placeMarker(point.lat, point.lon, this.label(), this.editable());
    this.locationPicked.emit(point);
  }

  private sync(lat: number | null | undefined, lon: number | null | undefined, label: string, editable: boolean): void {
    if (!this.map) return;
    if (!isValidPoint(lat, lon)) {
      this.marker?.remove();
      this.marker = null;
      return;
    }
    const hadMarker = !!this.marker;
    this.placeMarker(lat!, lon!, label, editable);
    const echo = this.lastPicked && this.lastPicked.lat === lat && this.lastPicked.lon === lon;
    if (echo) return;
    this.lastPicked = null;
    if (hadMarker) this.map.panTo([lat!, lon!]);
    else this.map.setView([lat!, lon!], Math.max(this.map.getZoom(), this.zoom()));
  }

  private placeMarker(lat: number, lon: number, label: string, editable: boolean): void {
    const L = this.L, map = this.map;
    if (!L || !map) return;
    if (!this.marker) {
      this.marker = L.marker([lat, lon], {
        icon: L.divIcon({ className: 'farm-map-pin', html: PIN_SVG, iconSize: [32, 42], iconAnchor: [16, 42], popupAnchor: [0, -38] }),
        draggable: editable,
        keyboard: true,
        title: label || translate('Farm location'),
        alt: label || translate('Farm location'),
        riseOnHover: true,
      }).addTo(map);
      this.marker.on('dragend', () => { if (this.marker) this.pick(this.marker.getLatLng()); });
    } else {
      this.marker.setLatLng([lat, lon]);
      if (editable) this.marker.dragging?.enable(); else this.marker.dragging?.disable();
    }
    this.marker.unbindPopup();
    if (label) {
      const content = document.createElement('div');
      content.className = 'font-semibold text-sm';
      content.textContent = label;
      this.marker.bindPopup(content);
    }
  }
}
