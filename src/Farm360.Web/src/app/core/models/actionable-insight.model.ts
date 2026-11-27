/** A live intelligence insight pushed over SignalR (growth alerts and the like). */
export interface ActionableInsight {
  id: string;
  animalId?: string;
  type: string;
  severity: string;
  title: string;
  message: string;
  createdOnUtc: string;
}
