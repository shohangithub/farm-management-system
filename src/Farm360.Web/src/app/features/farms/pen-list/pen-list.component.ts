import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { PenService } from '../services/pen.service';
import { PenList } from '../models/pen.model';

@Component({
  selector: 'app-pen-list',
  standalone: true,
  imports: [CommonModule, RouterModule, DragDropModule],
  templateUrl: './pen-list.component.html'
})
export class PenListComponent implements OnInit {
  private penService = inject(PenService);
  private route = inject(ActivatedRoute);

  pens: PenList[] = [];
  isLoading = true;
  shedId: string = '';
  farmId: string = '';
  branchId: string = '';
  Math = Math;

  ngOnInit(): void {
    // Expected to be a child route of shed
    this.route.parent?.paramMap.subscribe(params => {
      this.branchId = params.get('branchId') || '';
      this.farmId = params.get('farmId') || '';
      this.shedId = params.get('shedId') || '';
      if (this.shedId) {
        this.loadPens();
      }
    });
  }

  loadPens(): void {
    this.isLoading = true;
    this.penService.getPensByShed(this.shedId).subscribe({
      next: (data) => {
        this.pens = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load pens', err);
        this.isLoading = false;
      }
    });
  }

  // Future integration for Animal assignment via D&D
  drop(event: CdkDragDrop<PenList[]>) {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      // Logic for handling animal drops into a pen will go here
    }
  }

  getStatusName(status: number): string {
    switch (status) {
      case 1: return 'Active';
      case 2: return 'Inactive';
      case 3: return 'Maintenance';
      default: return 'Unknown';
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1: return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
      case 2: return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300';
      case 3: return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}
