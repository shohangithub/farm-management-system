import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HealthDashboard } from './health-dashboard';

describe('HealthDashboard', () => {
  let component: HealthDashboard;
  let fixture: ComponentFixture<HealthDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HealthDashboard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(HealthDashboard);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
