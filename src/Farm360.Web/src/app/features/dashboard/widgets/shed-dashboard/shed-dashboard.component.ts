import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ShedService } from '../../../farms/services/shed.service';
import { ShedList } from '../../../farms/models/shed.model';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, filter } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-shed-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shed-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShedDashboardComponent {
  private shedService = inject(ShedService);
  private route = inject(ActivatedRoute);

  Math = Math;

  private routeParams = toSignal(
    this.route.parent?.paramMap.pipe(
      map(params => ({
        branchId: params.get('branchId') || '',
        farmId: params.get('farmId') || ''
      }))
    ) || of({ branchId: '', farmId: '' }),
    { initialValue: { branchId: '', farmId: '' } }
  );

  readonly branchId = computed(() => this.routeParams().branchId);
  readonly farmId = computed(() => this.routeParams().farmId);

  readonly shedsResult = toSignal(
    toObservable(this.farmId).pipe(
      filter(id => !!id),
      switchMap(id => this.shedService.getShedsByFarm(id).pipe(
        catchError(err => {
          console.error(err);
          return of([] as ShedList[]);
        })
      ))
    ),
    { initialValue: [] as ShedList[] }
  );

  readonly sheds = computed(() => this.shedsResult());
  
  readonly totalSheds = computed(() => this.sheds().length);
  readonly totalOccupancy = computed(() => this.sheds().reduce((acc, s) => acc + (s.currentOccupancy || 0), 0));
  readonly totalCapacity = computed(() => this.sheds().reduce((acc, s) => acc + (s.capacity || 0), 0));
}
