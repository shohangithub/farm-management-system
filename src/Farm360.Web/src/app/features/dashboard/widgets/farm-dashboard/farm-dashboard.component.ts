import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { FarmService } from '../../../farms/services/farm.service';
import { FarmList } from '../../../farms/models/farm.model';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, filter } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-farm-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './farm-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmDashboardComponent {
  private farmService = inject(FarmService);
  private route = inject(ActivatedRoute);

  readonly branchId = toSignal(
    this.route.parent?.paramMap.pipe(map(params => params.get('branchId') || '')) || of(''),
    { initialValue: '' }
  );

  readonly farmsResult = toSignal(
    toObservable(this.branchId).pipe(
      filter(id => !!id),
      switchMap(id => this.farmService.getFarmsByBranch(id).pipe(
        catchError(err => {
          console.error(err);
          return of([] as FarmList[]);
        })
      ))
    ),
    { initialValue: [] as FarmList[] }
  );

  readonly farms = computed(() => this.farmsResult());
  
  readonly totalFarms = computed(() => this.farms().length);
  readonly totalAnimals = computed(() => this.farms().reduce((acc, f) => acc + (f.currentAnimalCount || 0), 0));
  readonly totalCapacity = computed(() => this.farms().reduce((acc, f) => acc + (f.capacity || 0), 0));
}
