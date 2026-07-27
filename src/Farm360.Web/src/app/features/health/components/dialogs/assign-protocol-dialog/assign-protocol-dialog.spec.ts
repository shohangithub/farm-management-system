import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignProtocolDialog } from './assign-protocol-dialog';

describe('AssignProtocolDialog', () => {
  let component: AssignProtocolDialog;
  let fixture: ComponentFixture<AssignProtocolDialog>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignProtocolDialog]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssignProtocolDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
