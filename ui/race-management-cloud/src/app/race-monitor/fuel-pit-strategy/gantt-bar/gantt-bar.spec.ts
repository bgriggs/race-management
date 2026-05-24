import { TestBed } from '@angular/core/testing';

import { GanttBar } from './gantt-bar';

describe('GanttBar', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GanttBar],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(GanttBar);
    fixture.componentRef.setInput('xPct', 10);
    fixture.componentRef.setInput('widthPct', 25);
    fixture.componentRef.setInput('boundary', 'first');
    expect(fixture.componentInstance).toBeTruthy();
  });
});
