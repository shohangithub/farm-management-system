import { DEFAULT_MAP_TILES } from './map-tiles';

export const environment = {
  production: true,
  // IMPORTANT: Set this to your live ASP.NET Core API URL
  apiUrl: 'https://farmapi.devance.net',
  // Map tiles: see map-tiles.ts for the licence note before a commercial launch.
  mapTiles: DEFAULT_MAP_TILES,
};
