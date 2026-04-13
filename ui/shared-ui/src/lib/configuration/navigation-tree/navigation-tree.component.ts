import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface NavigationTreeNode {
  id: string;
  label: string;
  visible?: boolean;
  children?: NavigationTreeNode[];
}

@Component({
  selector: 'rm-navigation-tree',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './navigation-tree.component.html',
  styleUrl: './navigation-tree.component.css'
})
export class NavigationTreeComponent {
  readonly nodes = input<NavigationTreeNode[]>([]);
  readonly selectedNodeId = input<string>('');
  readonly errorNodeIds = input<ReadonlySet<string>>(new Set<string>());

  readonly nodeSelected = output<string>();

  private readonly expandedNodeIds = new Set<string>();

  onSelect(nodeId: string): void {
    this.nodeSelected.emit(nodeId);
  }

  onToggle(nodeId: string, event: Event): void {
    event.stopPropagation();
    if (this.expandedNodeIds.has(nodeId)) {
      this.expandedNodeIds.delete(nodeId);
      return;
    }

    this.expandedNodeIds.add(nodeId);
  }

  isExpanded(nodeId: string): boolean {
    return !this.expandedNodeIds.has(nodeId);
  }

  hasVisibleChildren(node: NavigationTreeNode): boolean {
    return (node.children ?? []).some((child) => child.visible !== false);
  }

  hasNodeOrDescendantError(node: NavigationTreeNode): boolean {
    if (this.errorNodeIds().has(node.id)) {
      return true;
    }

    for (const child of node.children ?? []) {
      if (child.visible !== false && this.hasNodeOrDescendantError(child)) {
        return true;
      }
    }

    return false;
  }
}
