import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditMath } from './edit-math';
import { MathType } from '../../../../models/math-type';
import { SimpleOperationType } from '../../../../models/simple-operation-type';

describe('EditMath', () => {
  let component: EditMath;
  let fixture: ComponentFixture<EditMath>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditMath]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditMath);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channels', []);
    fixture.componentRef.setInput('math', {
      id: '',
      name: '',
      type: MathType.SimpleOperation,
      a: 0,
      b: 0,
      channel1Id: '',
      channel2Id: null,
      outputChannelId: '',
      simpleOperationType: SimpleOperationType.Add,
    });
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
