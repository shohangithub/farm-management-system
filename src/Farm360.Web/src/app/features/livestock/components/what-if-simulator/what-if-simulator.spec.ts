import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WhatIfSimulator } from './what-if-simulator';

describe('WhatIfSimulator', () => {
  let component: WhatIfSimulator;
  let fixture: ComponentFixture<WhatIfSimulator>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WhatIfSimulator]
    })
    .compileComponents();

    fixture = TestBed.createComponent(WhatIfSimulator);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
