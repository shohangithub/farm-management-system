import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VaccinationProtocolList } from './vaccination-protocol-list';

describe('VaccinationProtocolList', () => {
  let component: VaccinationProtocolList;
  let fixture: ComponentFixture<VaccinationProtocolList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VaccinationProtocolList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VaccinationProtocolList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
