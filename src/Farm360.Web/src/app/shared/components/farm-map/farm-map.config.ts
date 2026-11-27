import { environment } from '../../../../environments/environment';
import type { MapTilesConfig } from '../../../../environments/map-tiles';

/**
 * The tile layers every farm map uses. Defined per environment (environment*.ts → map-tiles.ts) so the URLs
 * can be swapped in one place. Licence note: the public OpenStreetMap tiles follow the OSMF Tile Usage Policy
 * (attribution required, no heavy, bulk or commercial use) and Esri World Imagery needs its attribution and
 * falls under Esri's terms. For a commercial launch use a tile provider plan (MapTiler, Stadia, Carto, Esri)
 * or self-host the tiles.
 */
export const MAP_TILES: MapTilesConfig = environment.mapTiles;
