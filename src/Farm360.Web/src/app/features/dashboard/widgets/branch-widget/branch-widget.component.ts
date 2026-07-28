import { Component, inject, signal, Input, ChangeDetectionStrategy, OnChanges, SimpleChanges, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BranchService } from '../../../organizations/services/branch.service';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, map, tap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-branch-widget',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './branch-widget.html',
  styleUrls: ['./branch-widget.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BranchWidgetComponent implements OnChanges {
  private readonly branchService = inject(BranchService);

  @Input() orgId: string = '';
  
  private orgIdSignal = signal<string>('');

  readonly isLoading = signal<boolean>(false);

  private branchStatsResult = toSignal(
    toObservable(this.orgIdSignal).pipe(
      filter(id => !!id),
      tap(() => this.isLoading.set(true)),
      switchMap(id => this.branchService.getBranchesByOrganization(id, '', undefined, 1, 1000).pipe(
        map(res => ({
          total: res.totalCount,
          active: res.items.filter(b => b.status === 1).length
        })),
        catchError(err => {
          console.error(err);
          return of({ total: 0, active: 0 });
        })
      )),
      tap(() => this.isLoading.set(false))
    ),
    { initialValue: { total: 0, active: 0 } }
  );

  readonly totalBranches = computed(() => this.branchStatsResult().total);
  readonly activeBranches = computed(() => this.branchStatsResult().active);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['orgId']) {
      this.orgIdSignal.set(this.orgId);
    }
  }
}
