import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CompetitorAnalysis } from './competitor-analysis';

describe('CompetitorAnalysis', () => {
  let component: CompetitorAnalysis;
  let fixture: ComponentFixture<CompetitorAnalysis>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompetitorAnalysis]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CompetitorAnalysis);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
