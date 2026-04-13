import { TestBed, getTestBed } from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting
} from '@angular/platform-browser/testing';
import { NavigationTreeComponent } from './navigation-tree.component';

try {
  getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
} catch {
  // Test environment may already be initialized by the runner.
}

describe('NavigationTreeComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavigationTreeComponent]
    }).compileComponents();
  });

  it('detects visible children correctly', () => {
    const fixture = TestBed.createComponent(NavigationTreeComponent);
    const component = fixture.componentInstance;

    expect(
      component.hasVisibleChildren({
        id: 'parent',
        label: 'Parent',
        children: [
          { id: 'hidden', label: 'Hidden', visible: false },
          { id: 'visible', label: 'Visible', visible: true }
        ]
      })
    ).toBe(true);

    expect(
      component.hasVisibleChildren({
        id: 'parent-2',
        label: 'Parent 2',
        children: [{ id: 'hidden-2', label: 'Hidden 2', visible: false }]
      })
    ).toBe(false);
  });

  it('detects errors from descendants and ignores hidden descendants', () => {
    const fixture = TestBed.createComponent(NavigationTreeComponent);
    const component = fixture.componentInstance;

    fixture.componentRef.setInput('errorNodeIds', new Set(['grandchild']));
    fixture.detectChanges();

    const withVisibleChild = {
      id: 'parent',
      label: 'Parent',
      children: [{ id: 'grandchild', label: 'Grandchild', visible: true }]
    };
    expect(component.hasNodeOrDescendantError(withVisibleChild)).toBe(true);

    const withHiddenChild = {
      id: 'parent-2',
      label: 'Parent 2',
      children: [{ id: 'grandchild', label: 'Grandchild', visible: false }]
    };
    expect(component.hasNodeOrDescendantError(withHiddenChild)).toBe(false);
  });

  it('toggles expansion state when chevron is toggled', () => {
    const fixture = TestBed.createComponent(NavigationTreeComponent);
    const component = fixture.componentInstance;

    const event = {
      stopPropagation: vi.fn()
    } as unknown as Event;

    expect(component.isExpanded('communications')).toBe(true);

    component.onToggle('communications', event);
    expect(component.isExpanded('communications')).toBe(false);

    component.onToggle('communications', event);
    expect(component.isExpanded('communications')).toBe(true);
  });

  it('emits selected node id when a row is clicked', () => {
    const fixture = TestBed.createComponent(NavigationTreeComponent);
    const component = fixture.componentInstance;
    const emitSpy = vi.spyOn(component.nodeSelected, 'emit');

    fixture.componentRef.setInput('nodes', [{ id: 'general-settings', label: 'General Settings' }]);
    fixture.detectChanges();

    const row = (fixture.nativeElement as HTMLElement).querySelector('.node-row');
    expect(row).toBeTruthy();

    row?.dispatchEvent(new Event('click'));

    expect(emitSpy).toHaveBeenCalledWith('general-settings');
  });
});
