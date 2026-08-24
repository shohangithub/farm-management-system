import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VaccinationComplianceChart } from './vaccination-compliance-chart';

describe('VaccinationComplianceChart', () => {
  let component: VaccinationComplianceChart;
  let fixture: ComponentFixture<VaccinationComplianceChart>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VaccinationComplianceChart]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VaccinationComplianceChart);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
