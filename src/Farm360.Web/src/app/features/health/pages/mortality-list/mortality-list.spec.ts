import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MortalityList } from './mortality-list';

describe('MortalityList', () => {
  let component: MortalityList;
  let fixture: ComponentFixture<MortalityList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MortalityList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MortalityList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
