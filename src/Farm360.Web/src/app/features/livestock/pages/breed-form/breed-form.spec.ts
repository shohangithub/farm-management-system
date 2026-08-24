import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BreedForm } from './breed-form';

describe('BreedForm', () => {
  let component: BreedForm;
  let fixture: ComponentFixture<BreedForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BreedForm]
    })
    .compileComponents();

    fixture = TestBed.createComponent(BreedForm);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
