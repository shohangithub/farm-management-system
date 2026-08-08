import { Injectable, signal } from '@angular/core';
import { AnimalListItemDto } from '../models/animal.models';

const STORAGE_KEY = 'farm360_recently_viewed_animals';
const MAX_ITEMS = 5;

@Injectable({
  providedIn: 'root'
})
export class RecentlyViewedService {
  private readonly items = signal<AnimalListItemDto[]>(this.loadFromStorage());

  readonly recentAnimals = this.items.asReadonly();

  private loadFromStorage(): AnimalListItemDto[] {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored ? JSON.parse(stored) : [];
    } catch {
      return [];
    }
  }

  private saveToStorage(animals: AnimalListItemDto[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(animals));
    } catch (e) {
      console.error('Failed to save recently viewed animals', e);
    }
  }

  add(animal: AnimalListItemDto): void {
    const current = this.items();
    // Remove if already exists
    const filtered = current.filter(a => a.id !== animal.id);
    
    // Add to beginning
    filtered.unshift(animal);
    
    // Keep only latest MAX_ITEMS
    if (filtered.length > MAX_ITEMS) {
      filtered.pop();
    }
    
    this.items.set(filtered);
    this.saveToStorage(filtered);
  }

  clear(): void {
    this.items.set([]);
    localStorage.removeItem(STORAGE_KEY);
  }
}
