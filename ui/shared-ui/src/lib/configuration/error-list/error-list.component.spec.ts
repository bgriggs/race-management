import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { ErrorListComponent } from './error-list.component';

try {
  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Test environment may already be initialized by the runner.
}

describe('ErrorListComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ErrorListComponent]
    }).compileComponents();
  });

  it('shows empty-state message when no errors exist', () => {
    const fixture = TestBed.createComponent(ErrorListComponent);

    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No validation errors.');
  });

  it('renders errors and emits node id when clicked', () => {
    const fixture = TestBed.createComponent(ErrorListComponent);
    const component = fixture.componentInstance;
    const emitSpy = vi.spyOn(component.navigateToNode, 'emit');

    fixture.componentRef.setInput('items', [
      {
        id: 'err-1',
        nodeId: 'communications',
        pageLabel: 'Communications',
        message: 'Invalid value'
      }
    ]);
    fixture.detectChanges();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button');
    expect(button).toBeTruthy();

    button?.dispatchEvent(new Event('click'));

    expect(emitSpy).toHaveBeenCalledWith('communications');
  });
});
