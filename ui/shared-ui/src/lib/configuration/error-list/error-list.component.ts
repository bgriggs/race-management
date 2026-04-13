import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ErrorListItem {
  id: string;
  nodeId: string;
  pageLabel: string;
  message: string;
}

@Component({
  selector: 'rm-error-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './error-list.component.html',
  styleUrl: './error-list.component.css'
})
export class ErrorListComponent {
  readonly items = input<ErrorListItem[]>([]);

  readonly navigateToNode = output<string>();
}
