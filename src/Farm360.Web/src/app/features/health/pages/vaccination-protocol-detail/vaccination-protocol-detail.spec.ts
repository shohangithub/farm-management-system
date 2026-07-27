import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VaccinationProtocolDetail } from './vaccination-protocol-detail';

describe('VaccinationProtocolDetail', () => {
  let component: VaccinationProtocolDetail;
  let fixture: ComponentFixture<VaccinationProtocolDetail>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VaccinationProtocolDetail]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VaccinationProtocolDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
