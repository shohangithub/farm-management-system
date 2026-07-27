import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RecordMortalityDialog } from './record-mortality-dialog';

describe('RecordMortalityDialog', () => {
  let component: RecordMortalityDialog;
  let fixture: ComponentFixture<RecordMortalityDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecordMortalityDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RecordMortalityDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
