import { Component, ElementRef, afterNextRender, input, output, signal, viewChild } from '@angular/core';

@Component({
  selector: 'app-rename-column-dialog',
  templateUrl: './rename-column-dialog.html',
  styleUrl: './rename-column-dialog.css',
})
export class RenameColumnDialog {
  readonly defaultName = input.required<string>();
  readonly initialValue = input<string | null>(null);
  readonly closed = output<void>();
  readonly confirmed = output<string | null>();

  protected readonly value = signal('');
  private readonly inputRef = viewChild<ElementRef<HTMLInputElement>>('nameInput');

  constructor() {
    afterNextRender(() => {
      this.value.set(this.initialValue() ?? '');
      const input = this.inputRef()?.nativeElement;
      if (input) {
        input.focus();
        input.select();
      }
    });
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }

  protected close(): void {
    this.closed.emit();
  }

  protected confirm(): void {
    const trimmed = this.value().trim();
    this.confirmed.emit(trimmed.length === 0 ? null : trimmed);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.confirm();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    }
  }
}
