import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FarmSummaryCards } from './farm-summary-cards';

describe('FarmSummaryCards', () => {
  let component: FarmSummaryCards;
  let fixture: ComponentFixture<FarmSummaryCards>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FarmSummaryCards]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FarmSummaryCards);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
