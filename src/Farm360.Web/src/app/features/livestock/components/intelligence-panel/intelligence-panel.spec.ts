import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IntelligencePanel } from './intelligence-panel';

describe('IntelligencePanel', () => {
  let component: IntelligencePanel;
  let fixture: ComponentFixture<IntelligencePanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntelligencePanel]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IntelligencePanel);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
