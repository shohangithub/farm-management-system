import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CurrentStock } from './current-stock';

describe('CurrentStock', () => {
  let component: CurrentStock;
  let fixture: ComponentFixture<CurrentStock>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CurrentStock]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CurrentStock);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
