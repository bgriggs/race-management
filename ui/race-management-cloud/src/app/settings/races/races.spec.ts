import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Races } from './races';

describe('Races', () => {
  let component: Races;
  let fixture: ComponentFixture<Races>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Races]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Races);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
