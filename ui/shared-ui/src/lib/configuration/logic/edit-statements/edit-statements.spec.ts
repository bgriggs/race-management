import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditStatements } from './edit-statements';

describe('EditStatements', () => {
  let component: EditStatements;
  let fixture: ComponentFixture<EditStatements>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditStatements]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditStatements);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
