import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FeedCostTrendChart } from './feed-cost-trend-chart';

describe('FeedCostTrendChart', () => {
  let component: FeedCostTrendChart;
  let fixture: ComponentFixture<FeedCostTrendChart>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeedCostTrendChart]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FeedCostTrendChart);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
