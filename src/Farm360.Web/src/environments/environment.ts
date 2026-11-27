import { DEFAULT_MAP_TILES } from './map-tiles';

export const environment = {
  production: false,
  // Leave empty for local development so proxy.conf.json handles /api requests
  apiUrl: '',
  // Map tiles: see map-tiles.ts for the licence note before a commercial launch.
  mapTiles: DEFAULT_MAP_TILES,
};
