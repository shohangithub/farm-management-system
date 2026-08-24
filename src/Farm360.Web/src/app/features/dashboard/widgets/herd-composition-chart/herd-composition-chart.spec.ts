import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HerdCompositionChart } from './herd-composition-chart';

describe('HerdCompositionChart', () => {
  let component: HerdCompositionChart;
  let fixture: ComponentFixture<HerdCompositionChart>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HerdCompositionChart]
    })
    .compileComponents();

    fixture = TestBed.createComponent(HerdCompositionChart);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
