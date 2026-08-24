import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdgTrendChart } from './adg-trend-chart';

describe('AdgTrendChart', () => {
  let component: AdgTrendChart;
  let fixture: ComponentFixture<AdgTrendChart>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdgTrendChart]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AdgTrendChart);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
