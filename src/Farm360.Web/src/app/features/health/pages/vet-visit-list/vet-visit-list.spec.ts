import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VetVisitList } from './vet-visit-list';

describe('VetVisitList', () => {
  let component: VetVisitList;
  let fixture: ComponentFixture<VetVisitList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VetVisitList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VetVisitList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
