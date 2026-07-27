import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ScheduleVaccinationDialog } from './schedule-vaccination-dialog';

describe('ScheduleVaccinationDialog', () => {
  let component: ScheduleVaccinationDialog;
  let fixture: ComponentFixture<ScheduleVaccinationDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScheduleVaccinationDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ScheduleVaccinationDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
