/**
 * Map tile layers for FarmMapComponent (src/app/shared/components/farm-map).
 *
 * LICENCE / USAGE NOTE
 * - Street: the public OpenStreetMap tile servers (tile.openstreetmap.org) are run by volunteers under the
 *   OSMF Tile Usage Policy (https://operations.osmfoundation.org/policies/tiles/): visible attribution is
 *   required, and heavy, bulk or commercial use is not allowed. Fine for development and a small pilot.
 * - Satellite: Esri World Imagery requires the attribution below; production/commercial use falls under the
 *   Esri / ArcGIS terms of use (an ArcGIS account or plan).
 * - For a commercial launch, switch to a paid tile plan (e.g. MapTiler, Stadia Maps, Carto, Esri) or
 *   self-host tiles: change the URLs (and any API key) here or per environment in environment*.ts.
 */
export interface MapTileLayerConfig {
  url: string;
  attribution: string;
  maxZoom: number;
}

export interface MapTilesConfig {
  street: MapTileLayerConfig;
  satellite: MapTileLayerConfig;
}

export const DEFAULT_MAP_TILES: MapTilesConfig = {
  street: {
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a> contributors',
    maxZoom: 19,
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Tiles &copy; <a href="https://www.esri.com" target="_blank" rel="noopener">Esri</a> &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
    maxZoom: 19,
  },
};
