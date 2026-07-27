import { Injectable, inject, signal } from '@angular/core';
import { BehaviorSubject, Observable, of, combineLatest, filter } from 'rxjs';
import { catchError, map, switchMap, tap, distinctUntilChanged } from 'rxjs/operators';
import { AuthService, UserProfile } from './auth.service';
import { LookupService } from './lookup.service';
import { LookupDto } from '../../shared/models/lookup.model';

const STORAGE_ORG_KEY = 'farm360_active_org';
const STORAGE_BRANCH_KEY = 'farm360_active_branch';
const STORAGE_FARM_KEY = 'farm360_active_farm';

@Injectable({
  providedIn: 'root'
})
export class WorkingContextService {
  private authService = inject(AuthService);
  private lookupService = inject(LookupService);

  // Available lists
  private organizationsSubject = new BehaviorSubject<LookupDto[]>([]);
  public organizations$ = this.organizationsSubject.asObservable();

  private branchesSubject = new BehaviorSubject<LookupDto[]>([]);
  public branches$ = this.branchesSubject.asObservable();

  private farmsSubject = new BehaviorSubject<LookupDto[]>([]);
  public farms$ = this.farmsSubject.asObservable();

  // Active selections
  private currentOrgSubject = new BehaviorSubject<LookupDto | null>(null);
  public currentOrg$ = this.currentOrgSubject.asObservable().pipe(distinctUntilChanged((a, b) => a?.id === b?.id));

  private currentBranchSubject = new BehaviorSubject<LookupDto | null>(null);
  public currentBranch$ = this.currentBranchSubject.asObservable().pipe(distinctUntilChanged((a, b) => a?.id === b?.id));

  private currentFarmSubject = new BehaviorSubject<LookupDto | null>(null);
  public currentFarm$ = this.currentFarmSubject.asObservable().pipe(distinctUntilChanged((a, b) => a?.id === b?.id));

  public get currentOrgValue(): LookupDto | null { return this.currentOrgSubject.value; }
  public get currentBranchValue(): LookupDto | null { return this.currentBranchSubject.value; }
  public get currentFarmValue(): LookupDto | null { return this.currentFarmSubject.value; }

  constructor() {
    // Listen to user login/logout to initialize/clear context
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.initializeContext(user);
      } else {
        this.clearContext();
      }
    });
  }

  private initializeContext(user: UserProfile) {
    // Fetch available organizations
    this.lookupService.getOrganizations().subscribe({
      next: (res) => {
        const orgs = res || [];
        this.organizationsSubject.next(orgs);
        
        let targetOrgId = localStorage.getItem(STORAGE_ORG_KEY);
        
        // Auto-select if only 1 organization
        if (orgs.length === 1) {
          targetOrgId = orgs[0].id;
        }
        
        // Validate saved targetOrgId
        const validOrg = orgs.find(o => o.id === targetOrgId);
        
        if (validOrg) {
          this.setOrganization(validOrg);
        } else if (orgs.length > 0) {
          // Fallback to first if saved is invalid
          this.setOrganization(orgs[0]);
        }
      },
      error: (err) => {
        console.error('Failed to load organizations for context', err);
      }
    });
  }

  public setOrganization(org: LookupDto | null) {
    this.currentOrgSubject.next(org);
    if (org) {
      localStorage.setItem(STORAGE_ORG_KEY, org.id);
      this.loadBranches(org.id);
    } else {
      localStorage.removeItem(STORAGE_ORG_KEY);
      this.branchesSubject.next([]);
      this.setBranch(null);
    }
  }

  private loadBranches(orgId: string) {
    this.lookupService.getBranches(orgId).subscribe({
      next: (res) => {
        const branches = res || [];
        this.branchesSubject.next(branches);

        let targetBranchId = localStorage.getItem(STORAGE_BRANCH_KEY);

        // Auto-select if only 1 branch
        if (branches.length === 1) {
          targetBranchId = branches[0].id;
        }

        const validBranch = branches.find(b => b.id === targetBranchId);
        
        if (validBranch) {
          this.setBranch(validBranch);
        } else if (branches.length > 0) {
          this.setBranch(branches[0]);
        } else {
          this.setBranch(null);
        }
      }
    });
  }

  public setBranch(branch: LookupDto | null) {
    this.currentBranchSubject.next(branch);
    if (branch) {
      localStorage.setItem(STORAGE_BRANCH_KEY, branch.id);
      this.loadFarms(branch.id);
    } else {
      localStorage.removeItem(STORAGE_BRANCH_KEY);
      this.farmsSubject.next([]);
      this.setFarm(null);
    }
  }

  private loadFarms(branchId: string) {
    this.lookupService.getFarms(branchId).subscribe({
      next: (farms) => {
        this.farmsSubject.next(farms || []);

        let targetFarmId = localStorage.getItem(STORAGE_FARM_KEY);

        // Auto-select if only 1 farm
        if (farms.length === 1) {
          targetFarmId = farms[0].id;
        }

        const validFarm = farms.find(f => f.id === targetFarmId);
        
        if (validFarm) {
          this.setFarm(validFarm);
        } else if (farms.length > 0) {
          this.setFarm(farms[0]);
        } else {
          this.setFarm(null);
        }
      }
    });
  }

  public setFarm(farm: LookupDto | null) {
    this.currentFarmSubject.next(farm);
    if (farm) {
      localStorage.setItem(STORAGE_FARM_KEY, farm.id);
    } else {
      localStorage.removeItem(STORAGE_FARM_KEY);
    }
  }

  private clearContext() {
    this.organizationsSubject.next([]);
    this.branchesSubject.next([]);
    this.farmsSubject.next([]);
    
    this.currentOrgSubject.next(null);
    this.currentBranchSubject.next(null);
    this.currentFarmSubject.next(null);
    
    // Do NOT clear localStorage here, so it persists across sessions if the user logs in again.
    // Invalid stored IDs will be naturally purged during initializeContext() fallback logic.
  }
}
