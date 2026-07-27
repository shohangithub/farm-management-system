import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LogTreatmentDialog } from './log-treatment-dialog';

describe('LogTreatmentDialog', () => {
  let component: LogTreatmentDialog;
  let fixture: ComponentFixture<LogTreatmentDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LogTreatmentDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LogTreatmentDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
